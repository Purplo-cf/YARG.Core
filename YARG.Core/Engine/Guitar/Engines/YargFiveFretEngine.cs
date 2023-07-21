﻿using System;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {
        public YargFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters) : base(chart, syncTrack, engineParameters)
        {
        }

        public override void UpdateBot(double songTime)
        {
            base.UpdateBot(songTime);

            if (State.NoteIndex >= Notes.Count)
            {
                return;
            }

            var note = Notes[State.NoteIndex];

            bool updateToSongTime = true;
            while (note is not null && songTime >= note.Time)
            {
                updateToSongTime = false;

                State.ButtonMask = (byte) note.NoteMask;
                State.StrummedThisUpdate = true;
                State.FrontEndStartTime = note.Time;
                if (!UpdateHitLogic(note.Time))
                {
                    break;
                }

                note = note.NextNote;
            }

            State.StrummedThisUpdate = false;

            if (updateToSongTime)
            {
                UpdateHitLogic(songTime);
            }
        }

        protected override bool UpdateHitLogic(double time)
        {
            UpdateTimeVariables(time);
            UpdateTimers();

            State.StrummedThisUpdate = (IsInputUpdate && IsStrumInput(CurrentInput) && CurrentInput.Button)
                || (State.StrummedThisUpdate && IsBotUpdate);
            if (IsInputUpdate && IsFretInput(CurrentInput))
            {
                ToggleFret(CurrentInput.Action, CurrentInput.Button);
                State.FrontEndStartTime = CurrentTime;
            }

            if (State.ButtonMask != State.TapButtonMask)
            {
                State.TapButtonMask = 0;
            }

            if (State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            // Update strum leniency if strummed this update
            if (State.StrummedThisUpdate)
            {
                if (IsTimerActive(CurrentTime, State.StrumLeniencyStartTime, EngineParameters.StrumLeniency))
                {
                    Overstrum();
                }

                State.StrumLeniencyStartTime = CurrentTime;
            }

            var note = Notes[State.NoteIndex];

            if (note.WasHit || note.WasMissed)
            {
                return false;
            }

            if (CurrentTime < note.Time + EngineParameters.FrontEnd)
            {
                return false;
            }

            if (CurrentTime > note.Time + EngineParameters.BackEnd && !note.WasHit)
            {
                MissNote(note);
                return true;
            }

            // Note skipping, useful for combo regain
            if (!CanNoteBeHit(note))
            {
                if (EngineStats.Combo != 0)
                {
                    return false;
                }

                var next = note.NextNote;
                while (next is not null)
                {
                    if (CurrentTime < next.Time + EngineParameters.FrontEnd)
                    {
                        return false;
                    }

                    // Don't need to check back end because if we're here then the previous note was not out of time

                    if (CanNoteBeHit(next) &&
                        (State.StrummedThisUpdate ||
                            IsTimerActive(CurrentTime, State.StrumLeniencyStartTime, EngineParameters.StrumLeniency) ||
                            note.IsTap) && State.TapButtonMask == 0)
                    {
                        if (HitNote(next))
                        {
                            YargTrace.LogInfo($"Skipping to hit next note as it is hittable ({State.TapButtonMask})");
                            return true;
                        }
                    }

                    next = next.NextNote;
                }

                return false;
            }

            // Handles hitting a hopo/tap notes
            if (State.TapButtonMask == 0 && note.IsTap || (note.IsHopo && EngineStats.Combo > 0))
            {
                return HitNote(note);
            }

            // If hopo/tap checks failed then the note can be hit if it was strummed
            if (State.StrummedThisUpdate ||
                IsTimerActive(CurrentTime, State.StrumLeniencyStartTime, EngineParameters.StrumLeniency))
            {
                return HitNote(note);
            }

            return false;
        }

        protected void UpdateTimers()
        {
            // We need to check if the strum leniency was active prior to this update
            // Then further down, we check if it expires on THIS update (if it does, we overstrum)
            if (IsTimerActive(LastUpdateTime, State.StrumLeniencyStartTime, EngineParameters.StrumLeniency))
            {
                // A hopo was strummed recently
                if (IsTimerActive(CurrentTime, State.HopoLeniencyStartTime, EngineParameters.HopoLeniency))
                {
                    // Hopo was double strummed, overstrum
                    if (State.WasHopoStrummed)
                    {
                        YargTrace.LogInfo("Hopo was double strummed. Overstrumming.");
                        Overstrum();
                        State.WasHopoStrummed = false;
                    }
                    else
                    {
                        YargTrace.LogInfo("Hopo/tap was strummed");
                        State.WasHopoStrummed = true;
                        ResetTimer(ref State.HopoLeniencyStartTime);
                    }

                    // This eats the strum input
                    ResetTimer(ref State.StrumLeniencyStartTime);
                }
                else
                {
                    if (HasTimerExpired(CurrentTime, State.StrumLeniencyStartTime, EngineParameters.StrumLeniency))
                    {
                        YargTrace.LogInfo("Strum leniency ran out, overstrumming");
                        Overstrum();
                    }
                }
            }
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            // If open, must not hold any frets
            // If not open, must be holding at least 1 fret
            if (note.NoteMask == 0 && State.ButtonMask != 0 || note.NoteMask != 0 && State.ButtonMask == 0)
            {
                return false;
            }

            // If holding exact note mask, can hit
            if (State.ButtonMask == note.NoteMask)
            {
                return true;
            }

            // Anchoring

            // XORing the two masks will give the anchor around the note.
            int anchorButtons = State.ButtonMask ^ note.NoteMask;

            // Strum chord (cannot anchor)
            if (note.IsChord && note.IsStrum)
            {
                // Buttons must match note mask exactly for strum chords
                return State.ButtonMask == note.NoteMask;
            }

            // Anchoring single notes or hopo/tap chords

            // Anchor buttons held are lower than the note mask
            return anchorButtons < note.NoteMask;
        }

        protected override bool HitNote(GuitarNote note)
        {
            State.TapButtonMask = State.ButtonMask;

            if (note.IsHopo || note.IsTap)
            {
                // Disallow hitting if front end timer is not in range of note time and didn't strum
                // (tried to hit as a hammeron/pulloff)
                if (!EngineParameters.InfiniteFrontEnd &&
                    HasTimerExpired(note.Time, State.FrontEndStartTime, Math.Abs(EngineParameters.FrontEnd)) &&
                    !IsTimerActive(CurrentTime, State.StrumLeniencyStartTime, EngineParameters.StrumLeniency))
                {
                    YargTrace.LogInfo("Front end timer not in range");
                    return false;
                }

                // Strummed a tap, or hopo while in combo
                if (((note.IsHopo && EngineStats.Combo > 0) || note.IsTap) && State.StrumLeniencyStartTime > 0)
                {
                }

                State.HopoLeniencyStartTime = CurrentTime;
            }
            else
            {
                // This line allows for hopos/taps to be hit using infinite front end after strumming
                State.TapButtonMask = 0;

                // Does the same thing but ensures it still works when infinite front end is disabled
                State.FrontEndStartTime = double.MinValue;

                State.WasHopoStrummed = false;
            }

            ResetTimer(ref State.StrumLeniencyStartTime);

            return base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            State.TapButtonMask = State.ButtonMask;
            base.MissNote(note);
        }

        protected bool IsFretInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.Green or
                    GuitarAction.Red or
                    GuitarAction.Yellow or
                    GuitarAction.Blue or
                    GuitarAction.Orange => true,
                _ => false,
            };
        }

        protected bool IsStrumInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.StrumUp or
                    GuitarAction.StrumDown => true,
                _ => false,
            };
        }
    }
}