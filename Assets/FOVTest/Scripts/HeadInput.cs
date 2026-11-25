using UnityEngine;

namespace FOVTest
{
    /// <summary>Head pose, eyes and buttons. The test only ever sees this, so it can run without a headset.</summary>
    public abstract class HeadInput : MonoBehaviour
    {
        public abstract Vector3 HeadPosition { get; }
        public abstract Quaternion HeadRotation { get; }
        public abstract Vector3 EyePosition(EyeMask eye);
        /// <summary>False while the headset is off the head or a system menu is in front.</summary>
        public abstract bool Active { get; }
        public abstract bool TriggerDown { get; }
        public abstract bool ConfirmDown { get; }
        public abstract bool AbortHeld { get; }
        /// <summary>-1/0/1 steps from either thumbstick, one step per flick.</summary>
        public abstract Vector2Int StickStep { get; }
        /// <summary>Goes up each time the user recenters, which turns the whole tracking space.</summary>
        public virtual int RecenterCount => 0;
        public Vector3 HeadForward => HeadRotation * Vector3.forward;
        public Vector3 HeadUp => HeadRotation * Vector3.up;
    }

    [DefaultExecutionOrder(-50)]   // after OVRManager, before the test
    public class QuestInput : HeadInput
    {
        OVRCameraRig rig;

        void Awake() => rig = FindAnyObjectByType<OVRCameraRig>();

        int recenters;
        OVRDisplay watched;
        public override int RecenterCount => recenters;

        void OnRecentered() => recenters++;

        void WatchRecentering()
        {
            if (watched == OVRManager.display) return;
            if (watched != null) watched.RecenteredPose -= OnRecentered;
            watched = OVRManager.display;
            if (watched != null) watched.RecenteredPose += OnRecentered;
        }

        void OnDestroy()
        {
            if (watched != null) watched.RecenteredPose -= OnRecentered;
        }

        public override Vector3 HeadPosition => rig.centerEyeAnchor.position;
        public override Quaternion HeadRotation => rig.centerEyeAnchor.rotation;

        public override Vector3 EyePosition(EyeMask eye) =>
            eye == EyeMask.Left ? rig.leftEyeAnchor.position :
            eye == EyeMask.Right ? rig.rightEyeAnchor.position : rig.centerEyeAnchor.position;

        public override bool Active => OVRManager.instance == null ||
            (OVRManager.instance.isUserPresent && OVRManager.hasInputFocus);

        public override bool TriggerDown =>
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) ||
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);

        // A or X
        public override bool ConfirmDown =>
            OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
            OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch);

        // B or Y
        public override bool AbortHeld =>
            OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
            OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.LTouch);

        public override Vector2Int StickStep => stick;
        Vector2Int stick;
        bool stickHeld;

        void Update()
        {
            WatchRecentering();
            var r = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            var l = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            var v = r.sqrMagnitude > l.sqrMagnitude ? r : l;
            stick = Vector2Int.zero;
            float m = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));
            if (m < 0.3f) stickHeld = false;
            else if (m > 0.6f && !stickHeld)
            {
                stickHeld = true;
                stick = Mathf.Abs(v.x) > Mathf.Abs(v.y)
                    ? new Vector2Int(v.x > 0 ? 1 : -1, 0)
                    : new Vector2Int(0, v.y > 0 ? 1 : -1);
            }
        }
    }
}
