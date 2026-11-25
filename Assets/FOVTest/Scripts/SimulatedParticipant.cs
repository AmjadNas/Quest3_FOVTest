using UnityEngine;

namespace FOVTest
{
    /// <summary>A simulated participant: turns to the target, waits, then responds to each stimulus.
    /// Used by the automated tests and by the on-device self-test.</summary>
    public class SimulatedParticipant : HeadInput
    {
        public Stage Stage;
        public PerimetryTest Test;
        public HeadInput Body;                  // real headset for position and eyes; null in the editor tests
        public float ReactionSec = 0.3f, DegPerSec = 90f;
        public System.Func<SpotDef, string> Strategy = s => s.IsBlindspot ? "trigger" : "seen";
        public bool Present = true;

        Quaternion rot = Quaternion.identity;
        Vector3 responseDir;
        float onset = -1f;
        bool trigger;

        public override Vector3 HeadPosition => Body != null ? Body.HeadPosition : Vector3.zero;
        public override Quaternion HeadRotation => rot;
        public override Vector3 EyePosition(EyeMask e) => Body != null ? Body.EyePosition(e)
            : new Vector3(e == EyeMask.Left ? -0.032f : 0.032f, 0, 0);
        public override bool Active => Present;
        public override bool TriggerDown => trigger;
        public override bool ConfirmDown => false;
        public override bool AbortHeld => false;
        public override Vector2Int StickStep => Vector2Int.zero;

        public void Face(Vector3 dir) => rot = Quaternion.LookRotation(dir, Vector3.up);

        void Update()
        {
            trigger = false;
            if (Test == null) return;
            if (!Test.AwaitingResponse)
            {
                onset = -1f;
                Turn(Stage.FixationDir);
                return;
            }
            var s = Test.CurrentSpot;
            if (onset < 0f)
            {
                onset = Time.unscaledTime;
                float sign = Strategy(s) == "wrong" ? -1f : 1f;
                responseDir = VisualAngle.Direction(Stage.FixationDir, HeadUp, s.X * sign, s.Y * sign);
            }
            if (Time.unscaledTime - onset < ReactionSec) return;
            switch (Strategy(s))
            {
                case "trigger": trigger = true; break;
                case "none": break;
                default: Turn(responseDir); break;
            }
        }

        void Turn(Vector3 dir) =>
            rot = Quaternion.RotateTowards(rot, Quaternion.LookRotation(dir, Vector3.up), DegPerSec * Time.unscaledDeltaTime);
    }
}
