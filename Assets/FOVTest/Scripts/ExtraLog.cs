using System.Collections.Generic;
using UnityEngine;

namespace FOVTest
{
    /// <summary>Where the target, the head and "straight ahead" were at each flash. Saved next to the log, in its own
    /// folder, because the log keeps the reference format. Times are on the same clock as the log's TimePresented.</summary>
    public class ExtraLog
    {
        public int FormatVersion = 1;
        public string Log;
        public string Units = "Degrees in tracking space: yaw + right, pitch + up, roll + right ear down. Seconds as TimePresented in the log.";
        public List<StraightAheadEvent> StraightAhead = new List<StraightAheadEvent>();
        public List<DiscardedTrial> Discarded = new List<DiscardedTrial>();
        public List<FlashExtra> Flashes = new List<FlashExtra>();
    }

    /// <summary>When "straight ahead", the centre of the area in which targets appear, was set.</summary>
    public class StraightAheadEvent
    {
        public float TimeSec;
        public string Reason;       // practice start, test start, headset back, recentered
        public float YawDeg, PitchDeg;
    }

    /// <summary>A trial that was interrupted. It is not in the log; its location is tested again later.</summary>
    public class DiscardedTrial
    {
        public float TimeSec;
        public string Reason;       // headset back, recentered, stopped
        public int LocationID;
    }

    /// <summary>One entry per entry of the log's History, in the same order.</summary>
    public class FlashExtra
    {
        public int HistoryIndex, TrialIndex;
        public bool Counted;
        public int LocationID;
        public string Eye;
        public float TimePresented;
        public float StraightAheadYawDeg, StraightAheadPitchDeg;
        public float TargetYawDeg, TargetPitchDeg, TargetFromStraightAheadDeg;
        public float HeadYawDeg, HeadPitchDeg, HeadRollDeg, HeadFromTargetDeg, HeadSpeedDegPerSec;
        public float ResponseHeadYawDeg, ResponseHeadPitchDeg, ResponseHeadRollDeg, ResponseHeadFromTargetDeg;
    }

    public static class HeadAngles
    {
        public static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        public static float Pitch(Vector3 dir) => Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

        public static float Roll(Quaternion head) =>
            Mathf.Atan2(-(head * Vector3.right).y, (head * Vector3.up).y) * Mathf.Rad2Deg;
    }
}
