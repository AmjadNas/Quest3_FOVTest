using UnityEngine;

namespace FOVTest
{
    /// <summary>Visual-angle geometry. XDegrees/YDegrees are polar: eccentricity is measured along the
    /// great circle, sqrt(x^2 + y^2), at meridian atan2(y, x). +x = right, +y = up.</summary>
    public static class VisualAngle
    {
        /// <summary>World direction of a point (xDeg, yDeg) relative to a fixation direction, using 'up'
        /// (the head's up at stimulus onset) as the vertical meridian.</summary>
        public static Vector3 Direction(Vector3 fixationDir, Vector3 up, float xDeg, float yDeg)
        {
            fixationDir.Normalize();
            Frame(fixationDir, up, out var right, out var upT);
            float ecc = Mathf.Sqrt(xDeg * xDeg + yDeg * yDeg) * Mathf.Deg2Rad;
            float theta = Mathf.Atan2(yDeg, xDeg);
            var tangent = right * Mathf.Cos(theta) + upT * Mathf.Sin(theta);
            return (fixationDir * Mathf.Cos(ecc) + tangent * Mathf.Sin(ecc)).normalized;
        }

        /// <summary>Eccentricity (deg) and polar angle (deg, CCW from +x) of 'dir' relative to fixation.</summary>
        public static void Measure(Vector3 fixationDir, Vector3 up, Vector3 dir,
                                   out float eccDeg, out float polarDeg)
        {
            fixationDir.Normalize();
            Frame(fixationDir, up, out var right, out var upT);
            dir.Normalize();
            eccDeg = Vector3.Angle(fixationDir, dir);
            polarDeg = Mathf.Atan2(Vector3.Dot(dir, upT), Vector3.Dot(dir, right)) * Mathf.Rad2Deg;
        }

        static void Frame(Vector3 fwd, Vector3 up, out Vector3 right, out Vector3 upT)
        {
            right = Vector3.Cross(up, fwd).normalized;
            upT = Vector3.Cross(fwd, right).normalized;
        }

        /// <summary>Choice sector of a response relative to the stimulus polar angle: six 60-degree sectors,
        /// 0 = correct (within 30 deg either side), matching the log's ChoiceIndex.</summary>
        public static int ChoiceSector(float stimPolarDeg, float responsePolarDeg)
        {
            float delta = Mathf.Repeat(responsePolarDeg - stimPolarDeg + 30f, 360f);
            return Mathf.FloorToInt(delta / 60f) % 6;
        }
    }
}
