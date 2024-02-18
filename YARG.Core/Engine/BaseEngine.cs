﻿using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine
{
    public abstract class BaseEngine
    {
        public bool IsInputQueued => InputQueue.Count > 0;

        public int BaseScore { get; protected set; }

        public EngineEventLogger EventLogger { get; }

        public abstract BaseEngineState      BaseState      { get; }
        public abstract BaseEngineParameters BaseParameters { get; }
        public abstract BaseStats            BaseStats      { get; }

        protected readonly SyncTrack SyncTrack;
        protected readonly uint Resolution;

        protected List<SoloSection> Solos = new();

        protected readonly Queue<GameInput> InputQueue;

        private readonly List<double> _consistencyAnchors = new();

        /// <summary>
        /// Whether or not the specified engine should treat a note as a chord, or separately.
        /// For example, guitars would treat each note as a chord, where as drums would treat them
        /// as singular pieces.
        /// </summary>
        protected readonly bool TreatChordAsSeparate;

        protected BaseEngine(SyncTrack syncTrack, bool isChordSeparate)
        {
            SyncTrack = syncTrack;
            Resolution = syncTrack.Resolution;
            TreatChordAsSeparate = isChordSeparate;

            EventLogger = new EngineEventLogger();
            InputQueue = new Queue<GameInput>();
        }

        /// <summary>
        /// Gets the number of notes the engine recognizes in a specific note parent.
        /// This number is determined by <see cref="TreatChordAsSeparate"/>.
        /// </summary>
        public int GetNumberOfNotes<T>(T type) where T : Note<T>
        {
            return TreatChordAsSeparate ? type.ChildNotes.Count + 1 : 1;
        }

        protected uint GetCurrentTick(double time)
        {
            return SyncTrack.TimeToTick(time);
        }

        /// <summary>
        /// Queue an input to be processed by the engine.
        /// </summary>
        /// <param name="input">The input to queue into the engine.</param>
        public void QueueInput(ref GameInput input)
        {
            // If the game attempts to queue an input that goes backwards in time, the engine
            // can't handle it and it will cause inconsistencies! In these rare cases, the
            // engine will be forced to move these times forwards a *tiny* bit to prevent
            // issues.

            // In the case that the queue is not in order...
            if (input.Time < BaseState.LastQueuedInputTime)
            {
                YargLogger.LogFormatWarning(
                    "Engine was forced to move an input time! Previous queued input: {0}, input being queued: {1}",
                    BaseState.LastQueuedInputTime, input.Time);

                input = new GameInput(BaseState.LastQueuedInputTime, input.Action, input.Integer);
            }

            // In the case that the input is before the current time...
            if (input.Time < BaseState.CurrentTime)
            {
                YargLogger.LogFormatWarning(
                    "Engine was forced to move an input time! Current time: {0}, input being queued: {1}",
                    BaseState.CurrentTime, input.Time);

                input = new GameInput(BaseState.CurrentTime, input.Action, input.Integer);
            }

            InputQueue.Enqueue(input);
            BaseState.LastQueuedInputTime = input.Time;
        }

        /// <summary>
        /// Updates the engine and processes all inputs currently queued.
        /// </summary>
        public void UpdateEngineInputs()
        {
            if (!IsInputQueued)
            {
                return;
            }

            ProcessInputs();
        }

        /// <summary>
        /// Loops through the input queue and processes each input. Invokes engine logic for each input.
        /// </summary>
        protected void ProcessInputs()
        {
            // Start to process inputs in queue.
            while (InputQueue.TryDequeue(out var input))
            {
                // This will update the engine to the time of the input.
                // However, it does not use the input for the update.
                UpdateUpToTime(input.Time);

                // Process the input and run hit logic for it.
                MutateStateWithInput(input);
                RunHitLogic(input.Time);
            }
        }

        /// <summary>
        /// Updates the engine with no input processing.
        /// </summary>
        /// <param name="time">The time to simulate hit logic at.</param>
        public void UpdateEngineToTime(double time)
        {
            UpdateUpToTime(time);
        }

        protected void RunHitLogic(double time)
        {
            bool noteUpdated;
            do
            {
                noteUpdated = UpdateEngineLogic(time);
            } while (noteUpdated);
        }

        public abstract void Reset(bool keepCurrentButtons = false);

        protected abstract void MutateStateWithInput(GameInput gameInput);

        protected void UpdateUpToTime(double time)
        {
            if (time < BaseState.CurrentTime)
            {
                YargTrace.LogError($"Engine could not update up to time {time} as it is before the current time!");
                return;
            }

            // Get consistency anchors
            _consistencyAnchors.Clear();
            AddConsistencyAnchors(_consistencyAnchors, BaseState.CurrentTime);
            _consistencyAnchors.Sort();

            // Run consistency anchors
            foreach (var anchor in _consistencyAnchors)
            {
                // Skip until we reach the point that the anchor is ahead of the current time.
                if (anchor < BaseState.CurrentTime)
                {
                    continue;
                }

                // Break when we reach a time that is ahead of the target time.
                // We can safely break here since it's sorted.
                if (anchor > time)
                {
                    break;
                }

                RunHitLogic(anchor);
            }

            // Run at the actual time
            RunHitLogic(time);
        }

        protected virtual void AddConsistencyAnchors(List<double> anchors, double originalTime)
        {
        }

        /// <summary>
        /// Executes engine logic with respect to the given time.
        /// </summary>
        /// <param name="time">The time in which to simulate hit logic at.</param>
        /// <returns>True if a note was updated (hit or missed). False if no changes.</returns>
        protected abstract bool UpdateEngineLogic(double time);

        /// <summary>
        /// Resets the engine's state back to default and then processes the list of inputs up to the given time.
        /// </summary>
        /// <param name="time">Time to process up to.</param>
        /// <param name="inputs">List of inputs to execute against.</param>
        /// <returns>The input index that was processed up to.</returns>
        public int ProcessUpToTime(double time, IEnumerable<GameInput> inputs)
        {
            Reset();

            var inputIndex = 0;
            double lastInputTime = 0;
            foreach (var input in inputs)
            {
                lastInputTime = input.Time;
                if (input.Time > time)
                {
                    break;
                }

                InputQueue.Enqueue(input);
                inputIndex++;
            }

            ProcessInputs();

            if (lastInputTime < time)
            {
                UpdateEngineToTime(time);
            }

            return inputIndex;
        }

        public abstract void UpdateBot(double songTime);

        public abstract (double FrontEnd, double BackEnd) CalculateHitWindow();
    }
}