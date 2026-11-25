using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FOVTest
{
    /// <summary>Piecewise-linear luminance table: digital brightness (0..1) to cd/m^2, and back.
    /// Same representation as LuminanceFunctionParams in the log ("[v, cd] [v, cd] ...").</summary>
    public class LuminanceModel
    {
        public readonly List<Vector2> Points;   // x = brightness, y = cd/m^2
        public readonly bool Known;             // true only if the table was measured
        public readonly string Profile;

        public LuminanceModel(IEnumerable<Vector2> points, string profile, bool known)
        {
            Points = points.OrderBy(p => p.x).ToList();
            if (Points.Count < 2) throw new FormatException("Luminance table needs at least two [v, cd] points.");
            for (int i = 1; i < Points.Count; i++)
                if (Points[i].y <= Points[i - 1].y)
                    throw new FormatException("Luminance table must increase with brightness.");
            Profile = profile;
            Known = known;
        }

        public float BlackLevel => Points[0].y;
        public float WhiteLevel => Points[Points.Count - 1].y;

        /// <summary>Nominal Quest 3 curve: 100 cd/m^2 peak, gamma 2.2, 0.1 cd/m^2 black. NOT a measurement:
        /// the headset's brightness slider changes it. Replace it with luminance.json after calibrating.</summary>
        public static LuminanceModel Quest3Nominal()
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float v = i / 20f;
                pts.Add(new Vector2(v, 0.1f + 99.9f * Mathf.Pow(v, 2.2f)));
            }
            return new LuminanceModel(pts, "Quest3-nominal", false);
        }

        public static LuminanceModel Parse(string lut, string profile, bool known)
        {
            var pts = new List<Vector2>();
            foreach (var part in lut.Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = part.Split(',');
                if (s.Length != 2) continue;
                pts.Add(new Vector2(float.Parse(s[0], CultureInfo.InvariantCulture),
                                    float.Parse(s[1], CultureInfo.InvariantCulture)));
            }
            return new LuminanceModel(pts, profile, known);
        }

        public string ToLut()
        {
            var sb = new StringBuilder();
            foreach (var p in Points)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append('[').Append(p.x.ToString("0.###", CultureInfo.InvariantCulture)).Append(", ")
                  .Append(p.y.ToString("0.##", CultureInfo.InvariantCulture)).Append(']');
            }
            return sb.ToString();
        }

        /// <summary>Brightness (framebuffer value) that shows 'cd', clamped to what the display can do.</summary>
        public float BrightnessFor(float cd)
        {
            if (cd <= BlackLevel) return Points[0].x;
            for (int i = 1; i < Points.Count; i++)
                if (cd <= Points[i].y)
                {
                    var a = Points[i - 1];
                    var b = Points[i];
                    return Mathf.Lerp(a.x, b.x, (cd - a.y) / (b.y - a.y));
                }
            return Points[Points.Count - 1].x;
        }

        /// <summary>A colour (linear-light mix, largest channel 1) shown at luminance "cd", using the grey table for
        /// each channel's tone curve. Saturated colours cannot reach the white level and are clamped.</summary>
        public Color Tint(float cd, Color mix)
        {
            float y = 0.2126f * mix.r + 0.7152f * mix.g + 0.0722f * mix.b;     // luminance of the mix relative to white
            float k = Mathf.Min(1f, cd / (y * WhiteLevel));
            float Channel(float m) => m <= 0f ? 0f : BrightnessFor(k * m * WhiteLevel);
            return new Color(Channel(mix.r), Channel(mix.g), Channel(mix.b), 1f);
        }

        public Color TintForContrast(float backgroundCd, float weber, Color mix) =>
            Tint(backgroundCd * (1f + weber), mix);

        /// <summary>Weber contrast: L = background * (1 + contrast).</summary>
        public float BrightnessForContrast(float backgroundCd, float weber) =>
            BrightnessFor(backgroundCd * (1f + weber));
    }
}
