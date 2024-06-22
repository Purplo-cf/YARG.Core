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

        public EngineEventLogger EventLogger { get; } = new();

        public abstract BaseEngineState      BaseState      { get; }
        public abstract BaseEngineParameters BaseParameters { get; }
        public abstract BaseStats            BaseStats      { get; }

        protected readonly SyncTrack SyncTrack;
        protected readonly uint Resolution;

        protected List<SoloSection> Solos = new();

        protected readonly Queue<GameInput> InputQueue = new();

        private readonly List<double> _scheduledUpdates = new();

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

        public void Update(double time)
        {
            while (InputQueue.TryDequeue(out var input))
            {
                // Skip inputs that are in the past
                if (input.Time < BaseState.CurrentTime)
                {
                    YargTrace.Fail(
                        $"Queued input is in the past! Current time: {BaseState.CurrentTime}, input time: {input.Time}");
                    continue;
                }

                // Skip inputs that are in the future
                if (input.Time > time)
                {
                    YargLogger.LogFormatWarning(
                        "Queued input is in the future! Time being updated to: {0}, input time: {1}", time, input.Time);
                    break;
                }

                RunQueuedUpdates(input.Time);
                MutateStateWithInput(input);
                YargLogger.LogFormatTrace("Processing input {0} ({1}) at {2}", input.GetAction<GuitarAction>(), input.Button, input.Time);
                UpdateHitLogic(input.Time);

                // Skip non-input update if possible
                if (input.Time == time)
                {
                    if (InputQueue.Count > 0)
                    {
                        YargLogger.LogWarning("Input queue was not fully cleared! Remaining inputs are possibly in the future");
                    }
                    return;
                }
            }

            // Update to the given time
            if (InputQueue.Count > 0)
            {
                YargLogger.LogWarning("Input queue was not fully cleared!");
            }
            RunQueuedUpdates(time);
            UpdateHitLogic(time);
        }

        private void RunQueuedUpdates(double time)
        {
            // 'for' is used here to prevent enumeration exceptions,
            // the list of scheduled updates will be modified by the updates we're running

            GenerateQueuedUpdates(time);
            _scheduledUpdates.Sort();

            if (_scheduledUpdates.Count > 0)
            {
                YargLogger.LogFormatTrace("{0} updates ready to be simulated", _scheduledUpdates.Count);
            }
            int i = 0;
            for (; i < _scheduledUpdates.Count; i++)
            {
                double updateTime = _scheduledUpdates[i];

                // Skip updates that are in the past
                if (updateTime < BaseState.CurrentTime)
                {
                    YargTrace.Fail(
                        $"Scheduled update is in the past! Current time: {BaseState.CurrentTime}, update time: {updateTime}");
                    continue;
                }

                // There should be no scheduled updates for times beyond the one we want to update to
                if (updateTime >= time)
                {
                    YargLogger.FailFormat("Update time is >= than the given time! Update time: {0}, given time: {1}", updateTime, time);
                    continue;
                }

                YargLogger.LogFormatTrace("Running scheduled update at {0}", updateTime);
                UpdateHitLogic(updateTime);
            }

            // Remove all processed updates
            _scheduledUpdates.RemoveRange(0, i);
        }

        protected virtual void GenerateQueuedUpdates(double nextTime)
        {

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
                YargTrace.LogWarning("Engine was forced to move an out-of-order input time! " +
                    $"Previous queued input: {BaseState.LastQueuedInputTime}, input being queued: {input.Time}");

                input = new GameInput(BaseState.LastQueuedInputTime, input.Action, input.Integer);
            }

            // In the case that the input is before the current time...
            if (input.Time < BaseState.CurrentTime)
            {
                YargTrace.LogWarning("Engine was forced to move an input time from the past! " +
                    $"Current time: {BaseState.CurrentTime}, input being queued: {input.Time}");

                input = new GameInput(BaseState.CurrentTime, input.Action, input.Integer);
            }

            InputQueue.Enqueue(input);
            BaseState.LastQueuedInputTime = input.Time;
        }

        public void QueueUpdateTime(double time)
        {
            // Ignore updates for the current time
            if (time == BaseState.CurrentTime)
            {
                return;
            }

            // Disallow updates in the past
            if (time < BaseState.CurrentTime)
            {
                YargTrace.Fail(
                    $"Cannot queue update in the past! Current time: {BaseState.CurrentTime}, time being queued: {time}");
                return;
            }

            // Ignore duplicate updates
            if (_scheduledUpdates.Contains(time))
            {
                return;
            }

            _scheduledUpdates.Add(time);
        }

        internal void QueueManyUpdateTimesNoChecks(IEnumerable<double> times)
        {
            _scheduledUpdates.AddRange(times);
            _scheduledUpdates.Sort();
        }

        public abstract void Reset(bool keepCurrentButtons = false);

        protected abstract void MutateStateWithInput(GameInput gameInput);

        /// <summary>
        /// Executes engine logic with respect to the given time.
        /// </summary>
        /// <param name="time">The time in which to simulate hit logic at.</param>
        /// <returns>True if a note was updated (hit or missed). False if no changes.</returns>
        protected abstract void UpdateHitLogic(double time);

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
            foreach (var input in inputs)
            {
                if (input.Time > time)
                {
                    break;
                }

                InputQueue.Enqueue(input);
                inputIndex++;
            }

            Update(time);

            return inputIndex;
        }

        public abstract void UpdateBot(double songTime);

        public abstract (double FrontEnd, double BackEnd) CalculateHitWindow();

        protected static void StartTimer(ref EngineTimer timer, double startTime, double offset = 0)
        {
            if (offset > 0)
            {
                timer.StartWithOffset(startTime, offset);
            }
            else
            {
                timer.Start(startTime);
            }
        }

        protected static bool IsTimeBetween(double time, double prevTime, double nextTime)
        {
            return time > prevTime && time < nextTime;
        }
    }
}