using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FOVTest
{
    /// <summary>Runs trials: connect the head pointer to the fixation target, 0.1 s later a stimulus flashes,
    /// the participant moves the pointer towards it or pulls the trigger if nothing was seen.</summary>
    public class PerimetryTest
    {
        readonly Stage stage;
        readonly HeadInput input;
        readonly AppSettings settings;
        readonly TestLayout layout;
        readonly TestParameters prm;
        readonly System.Random rng = new System.Random();
        /// <summary>No head turns this far in one frame, so a larger step means the tracking space was turned.</summary>
        const float MaxHeadStepDeg = 20f;
        StimulusScheduler scheduler;
        Vector3 primary;        // straight ahead, set when the test starts
        float abortHeld, pausedSec, headSpeed;
        Vector3 lastForward = Vector3.forward;
        float startTime, timeLimit;
        bool timing, recentered;
        int recentersSeen;
        string interruption = "stopped";

        public readonly List<TrialLog> History = new List<TrialLog>();
        public readonly ExtraLog Extra = new ExtraLog();
        public int TotalLocations { get; private set; }
        public float DurationSec { get; private set; }
        /// <summary>Practice or test has begun, so there is something to record even if no trial is completed.</summary>
        public bool Started { get; private set; }
        public bool Aborted { get; private set; }
        public bool TimedOut { get; private set; }
        public Outcome LastOutcome { get; private set; }
        /// <summary>Called after each trial, with the trial's log entry.</summary>
        public Action<TrialLog> TrialFinished;
        /// <summary>Called when the pointer locks onto the target, and when the trial has been answered.</summary>
        public Action TargetLocked;
        public Action<Outcome> Answered;
        /// <summary>The spot being tested, and whether its stimulus has been presented yet.</summary>
        public SpotDef CurrentSpot { get; private set; }
        public bool AwaitingResponse { get; private set; }

        public PerimetryTest(Stage stage, HeadInput input, AppSettings settings, TestLayout layout)
        {
            this.stage = stage;
            this.input = input;
            this.settings = settings;
            this.layout = layout;
            prm = settings.Test;
        }

        static float Now => (float)Time.unscaledTimeAsDouble;
        float FrameSec => 1f / prm.DisplayFrequencyHz;

        public void SetPrimaryToHead(string reason = "start")
        {
            primary = input.HeadForward;
            lastForward = primary;
            recentersSeen = input.RecenterCount;
            recentered = false;
            Extra.StraightAhead.Add(new StraightAheadEvent
            {
                TimeSec = Now, Reason = reason, YawDeg = HeadAngles.Yaw(primary), PitchDeg = HeadAngles.Pitch(primary),
            });
        }

        /// <summary>How many locations the test will have, so that a test stopped during practice counts them as untested.</summary>
        public void Plan(int totalLocations) => TotalLocations = totalLocations;

        float ElapsedSec => Now - startTime - pausedSec;
        bool OutOfTime => timing && ElapsedSec >= timeLimit;

        /// <summary>Full test. Results are in History.</summary>
        public IEnumerator Run(List<SpotDef> spots, int? seed = null)
        {
            scheduler = new StimulusScheduler(spots, seed);
            TotalLocations = scheduler.TotalLocations;
            Started = true;
            lastForward = input.HeadForward;
            startTime = Now;
            pausedSec = 0f;
            timeLimit = layout.Meta.TimeLimitMinutes * 60f;
            timing = true;
            while (scheduler.HasNext && !Aborted && !TimedOut)
            {
                if (OutOfTime) { TimedOut = true; break; }
                yield return Trial(scheduler.Next(), true, (float)scheduler.LocationsDone / scheduler.TotalLocations);
                DurationSec = ElapsedSec;
            }
            timing = false;
            stage.HideAll();
        }

        /// <summary>Unlogged practice trials.</summary>
        public IEnumerator Practice(List<SpotDef> spots, int count)
        {
            Started = true;
            lastForward = input.HeadForward;
            for (int i = 0; i < count && !Aborted; i++)
                yield return Trial(spots[rng.Next(spots.Count)], false, (float)i / count);
            stage.HideAll();
        }

        IEnumerator Trial(SpotDef spot, bool log, float progress)
        {
            var stimEye = spot.Eye == "Left" ? EyeMask.Left : EyeMask.Right;
            var fixEye = settings.Activity.FixationTargetLaterality == "Monocular" ? stimEye : EyeMask.Both;
            CurrentSpot = spot;
            AwaitingResponse = false;
            if (recentered) SetPrimaryToHead("recentered");     // recentered between two trials
            var target = NextFixation();
            float fixationStart = Now;
            stage.ShowFixation(target, primary, fixEye, settings.Activity.FixationRingVisible, progress);

            // Fixation: the target locks when the pointer touches it at low speed. From then on it stays locked:
            // the stimulus follows 0.1 s later whatever the pointer does.
            float connected = -1f;
            var up = input.HeadUp;
            while (true)
            {
                yield return null;
                if (Interrupted()) { stage.HideTarget(); if (!Aborted) yield return WaitUntilActive(); Finish(null, Outcome.Invalid, log); yield break; }
                stage.UpdatePointer(input.HeadForward, input.HeadUp, true);
                if (connected < 0f)
                {
                    // The time limit also ends the wait for a target the participant cannot find.
                    if (OutOfTime) { TimedOut = true; stage.HideTarget(); yield break; }
                    if (Vector3.Angle(input.HeadForward, target) > stage.ConnectRadiusDeg ||
                        headSpeed > prm.LockMaxSpeedDegPerSec) continue;
                    connected = Now;
                    up = input.HeadUp;      // vertical for the areas, the stimulus and the answer
                    stage.ShowResponsePhase(spot.PolarDeg, up);
                    TargetLocked?.Invoke();
                }
                if (Now - connected + FrameSec > prm.PreStimulusDelaySec) break;
            }

            // Stimulus and response.
            stage.Follow(input.HeadPosition);
            float brightness = stage.ShowStimulus(spot, input.EyePosition(stimEye), up);
            float onset = Now;
            AwaitingResponse = true;
            var headAtFlash = input.HeadRotation;
            var headAtResponse = headAtFlash;
            float speedAtFlash = headSpeed;
            int frames = 1;
            float flash = -1f, responseAt = -1f;
            int choice = -1;
            bool responded = false, trigger = false;
            stage.UpdatePointer(input.HeadForward, input.HeadUp, prm.ShowPointerWhileResponding);
            while (true)
            {
                yield return null;
                if (Interrupted()) { stage.HideTarget(); if (!Aborted) yield return WaitUntilActive(); Finish(null, Outcome.Invalid, log); yield break; }
                float t = Now - onset;
                if (flash < 0f)
                {
                    // Off before the frame that would overrun the nominal duration (0.3 s at 72 Hz = 21 frames).
                    if (t + FrameSec > spot.DurationSec) { stage.HideStimulus(); flash = t; }
                    else frames++;
                }
                if (responseAt < 0f)
                {
                    VisualAngle.Measure(target, up, input.HeadForward, out var ecc, out var polar);
                    if (ecc >= prm.ResponseThresholdDeg)
                    {
                        responseAt = t;
                        responded = true;
                        choice = VisualAngle.ChoiceSector(spot.PolarDeg, polar);
                    }
                    else if (layout.Meta.AllowSkipUnseen && input.TriggerDown)
                    {
                        responseAt = t;
                        trigger = true;
                    }
                    else if (t >= layout.Meta.ResponseTimeoutSec) responseAt = t;
                    if (responseAt >= 0f) headAtResponse = input.HeadRotation;
                }
                stage.UpdatePointer(input.HeadForward, input.HeadUp, prm.ShowPointerWhileResponding);
                if (responseAt >= 0f && flash >= 0f) break;
            }

            bool seen = responded && choice == 0;
            int expected = Mathf.FloorToInt(spot.DurationSec * prm.DisplayFrequencyHz + 0.01f);
            bool anticipated = responded && responseAt < prm.MinResponseSec;
            var outcome = expected - frames >= prm.MaxDroppedFrames || anticipated ? Outcome.Invalid :
                          seen ? Outcome.Seen : responded ? Outcome.Incorrect : Outcome.NoResponse;
            float elapsed = onset + responseAt;
            var entry = new TrialLog
            {
                Spot = new SpotLog
                {
                    XDegrees = spot.X, YDegrees = spot.Y,
                    Color = new Color(brightness, brightness, brightness, 1f).ToString(),
                    ContrastEst = spot.Contrast, BgLuminanceCdEst = spot.BackgroundCd, Eye = spot.Eye,
                    StimDiamDeg = spot.DiameterDeg, StimDurationSec = spot.DurationSec,
                    Index = spot.Id, SpotId = spot.Id, GroupId = spot.GroupId,
                    LocationID = spot.LocationId, Note = spot.Note,
                },
                ElapsedTimeSec = elapsed,
                TimePresented = onset,
                ResponseTimeSec = responseAt,
                FixationStart = fixationStart,
                FixationDurationSec = connected - fixationStart,
                TotalTrialDurationSec = elapsed - fixationStart,
                ChoiceIndex = choice,
                Seen = seen,
                Responded = responded,
                FramesPresented = frames,
                MeasuredFlashDurationMs = (int)(flash * 1000f),
                TriggerPull = trigger,
                Invalid = outcome == Outcome.Invalid,
            };
            AwaitingResponse = false;
            stage.HideTarget();
            // What the participant did, even if the trial is repeated because frames were dropped.
            var answer = seen ? Outcome.Seen : responded ? Outcome.Incorrect : Outcome.NoResponse;
            if (prm.Feedback) stage.ShowFeedback(answer, input.HeadForward);
            Answered?.Invoke(answer);
            Finish(entry, outcome, log);
            if (log) AddExtra(entry, target, headAtFlash, headAtResponse, speedAtFlash);

            float iti = Now;
            while (Now - iti < prm.InterTrialSec && !Interrupted())
            {
                yield return null;
                stage.UpdatePointer(input.HeadForward, input.HeadUp, !prm.Feedback);   // the mark takes its place
            }
        }

        void AddExtra(TrialLog entry, Vector3 target, Quaternion flash, Quaternion response, float speed)
        {
            Vector3 f = flash * Vector3.forward, r = response * Vector3.forward;
            Extra.Flashes.Add(new FlashExtra
            {
                HistoryIndex = History.Count - 1, TrialIndex = entry.TrialIndex, Counted = !entry.Invalid,
                LocationID = entry.Spot.LocationID, Eye = entry.Spot.Eye, TimePresented = entry.TimePresented,
                StraightAheadYawDeg = HeadAngles.Yaw(primary), StraightAheadPitchDeg = HeadAngles.Pitch(primary),
                TargetYawDeg = HeadAngles.Yaw(target), TargetPitchDeg = HeadAngles.Pitch(target),
                TargetFromStraightAheadDeg = Vector3.Angle(primary, target),
                HeadYawDeg = HeadAngles.Yaw(f), HeadPitchDeg = HeadAngles.Pitch(f), HeadRollDeg = HeadAngles.Roll(flash),
                HeadFromTargetDeg = Vector3.Angle(f, target), HeadSpeedDegPerSec = speed,
                ResponseHeadYawDeg = HeadAngles.Yaw(r), ResponseHeadPitchDeg = HeadAngles.Pitch(r),
                ResponseHeadRollDeg = HeadAngles.Roll(response), ResponseHeadFromTargetDeg = Vector3.Angle(r, target),
            });
        }

        void Finish(TrialLog entry, Outcome outcome, bool log)
        {
            LastOutcome = outcome;
            if (entry == null && log)
                Extra.Discarded.Add(new DiscardedTrial
                {
                    TimeSec = Now, Reason = Aborted ? "stopped" : interruption, LocationID = CurrentSpot.LocationId,
                });
            if (log)
            {
                int index = scheduler.CountedTrials;
                scheduler.Report(outcome);
                if (entry != null)
                {
                    entry.TrialIndex = index;
                    entry.PercentComplete = 100f * scheduler.LocationsDone / scheduler.TotalLocations;
                    History.Add(entry);
                }
            }
            if (entry != null) TrialFinished?.Invoke(entry);
        }

        /// <summary>Random target within the fixation spread, far enough from the pointer to need a head movement.</summary>
        Vector3 NextFixation()
        {
            for (int i = 0; i < 50; i++)
            {
                float r = prm.FixationSpreadDeg * Mathf.Sqrt((float)rng.NextDouble());
                float a = 2f * Mathf.PI * (float)rng.NextDouble();
                var dir = VisualAngle.Direction(primary, Vector3.up, r * Mathf.Cos(a), r * Mathf.Sin(a));
                if (Vector3.Angle(dir, input.HeadForward) >= prm.MinFixationStepDeg) return dir;
            }
            return primary;
        }

        // Called once per frame by every loop of a trial.
        bool Interrupted()
        {
            float dt = Time.unscaledDeltaTime;
            float step = Vector3.Angle(lastForward, input.HeadForward);
            if (dt > 0f) headSpeed = Mathf.Lerp(headSpeed, step / dt, 0.5f);
            if (input.RecenterCount != recentersSeen || step > MaxHeadStepDeg) recentered = true;
            lastForward = input.HeadForward;
            if (timing) DurationSec = ElapsedSec;
            abortHeld = input.AbortHeld ? abortHeld + Time.unscaledDeltaTime : 0f;
            if (abortHeld >= 2f) Aborted = true;
            return Aborted || !input.Active || recentered;
        }

        IEnumerator WaitUntilActive()
        {
            AwaitingResponse = false;
            float t0 = Now;
            interruption = input.Active ? "recentered" : "headset back";
            while (!input.Active) yield return null;
            pausedSec += Now - t0;
            // Directions from before are no longer valid: start again from where the head points now.
            SetPrimaryToHead(interruption);
        }
    }
}
