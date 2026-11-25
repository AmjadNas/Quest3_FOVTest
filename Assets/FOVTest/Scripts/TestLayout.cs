using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace FOVTest
{
    public class SpotDef
    {
        public int Id, GroupId, LocationId, TimesToTest, TimesToRetestIfMissed;
        public float X, Y, DiameterDeg, Contrast, DurationSec, BackgroundCd;
        public string Eye;      // "Left" / "Right"
        public string Rule, Note;
        public bool IsBlindspot => Rule == "fixed_blindspot";
        public float PolarDeg => Mathf.Atan2(Y, X) * Mathf.Rad2Deg;
    }

    public class LayoutRule
    {
        public string Type;
        public float Contrast;
        public int TimesToTest, TimesToRetestIfMissed;
    }

    public class LayoutMeta
    {
        public float BackgroundLuminanceCd = 2, CursorContrast = 6, FixationDotContrast = 8,
                     FixationCircleContrast = 2, ResponseLinesContrast = 1, FixedCircleRadiusDegrees = 32;
        public float ResponseTimeoutSec = 2;
        public bool AllowSkipUnseen = true;
        public float TimeLimitMinutes = 30;
        public Dictionary<string, LayoutRule> Rules = new Dictionary<string, LayoutRule>();
    }

    /// <summary>A stimulus layout: a CSV in the log's LayoutData format plus a JSON metadata file.</summary>
    public class TestLayout
    {
        public string Name, FileName, Csv, MetaJson;
        public LayoutMeta Meta;
        public readonly List<SpotDef> Spots = new List<SpotDef>();

        /// <summary>Asset name and the name used in the log's ActivitySettings.Layout.</summary>
        public static readonly (string asset, string name)[] Builtin =
        {
            ("VVPC_Training-30deg", "Training-30deg"),
            ("24-2", "24-2"),
        };

        public static TestLayout LoadBuiltin(string name)
        {
            foreach (var b in Builtin)
            {
                if (b.name != name) continue;
                var csv = Resources.Load<TextAsset>("Layouts/" + b.asset + ".csv");
                var meta = Resources.Load<TextAsset>("Layouts/" + b.asset + ".meta");
                if (csv == null || meta == null) break;
                return Parse(b.name, b.asset + ".csv", csv.text, meta.text);
            }
            throw new ArgumentException("Unknown layout " + name);
        }

        public static TestLayout Parse(string name, string fileName, string csv, string metaJson)
        {
            var layout = new TestLayout
            {
                Name = name,
                FileName = fileName,
                Csv = csv.Replace("\r\n", "\n").Trim('\n', '﻿'),
                MetaJson = metaJson.Replace("\r\n", "\n").Trim('\n', '﻿'),
            };
            layout.Meta = JsonConvert.DeserializeObject<LayoutMeta>(layout.MetaJson);
            var lines = layout.Csv.Split('\n');
            var header = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList();
            for (int i = 1; i < lines.Length; i++)
            {
                var c = lines[i].Split(',');
                if (c.Length != header.Count)
                    throw new FormatException($"{fileName} line {i + 1}: expected {header.Count} columns, got {c.Length}.");
                string S(string n) => c[header.IndexOf(n)].Trim().Trim('"');
                float F(string n) => float.Parse(S(n), CultureInfo.InvariantCulture);
                int I(string n) => int.Parse(S(n), CultureInfo.InvariantCulture);
                var rule = S("contrast_rule");
                if (layout.Meta.Rules.TryGetValue(rule, out var r) && r.Type != "FixedContrast")
                    throw new NotSupportedException($"Rule type {r.Type} is not supported (only FixedContrast).");
                layout.Spots.Add(new SpotDef
                {
                    Id = I("stimulus_id"),
                    GroupId = I("stimulus_group_id"),
                    X = F("stimulus_x_deg"),
                    Y = F("stimulus_y_deg"),
                    DiameterDeg = F("stimulus_diameter_deg"),
                    Eye = S("stimulus_eye").Equals("left", StringComparison.OrdinalIgnoreCase) ? "Left" : "Right",
                    Contrast = F("stimulus_contrast"),
                    DurationSec = F("stimulus_duration_sec"),
                    Rule = rule,
                    BackgroundCd = F("background_luminance_cd"),
                    TimesToTest = I("times_to_test"),
                    TimesToRetestIfMissed = I("times_to_retest_if_missed"),
                    LocationId = I("stimulus_location_id"),
                    Note = S("notes"),
                });
            }
            return layout;
        }

        /// <summary>Show every stimulus at another diameter. The CSV kept for the log is rewritten too, and the layout is
        /// renamed, so that a log can never claim the original size or the original layout file.</summary>
        public void SetDiameter(float deg)
        {
            if (Spots.All(s => Mathf.Abs(s.DiameterDeg - deg) < 1e-3f)) return;
            var size = deg.ToString("0.###", CultureInfo.InvariantCulture);
            var lines = Csv.Split('\n');
            int col = Array.FindIndex(lines[0].Split(','), h => h.Trim().ToLowerInvariant() == "stimulus_diameter_deg");
            for (int i = 1; i < lines.Length; i++)
            {
                var c = lines[i].Split(',');
                c[col] = size;
                lines[i] = string.Join(",", c);
            }
            Csv = string.Join("\n", lines);
            foreach (var s in Spots) s.DiameterDeg = deg;
            FileName = FileName.Replace(".csv", "_" + size + "deg.csv");
            Name += " " + size + "deg";
        }

        /// <summary>Spots to run for the current settings.</summary>
        public List<SpotDef> Select(string testEyes, bool includeBlindspot) =>
            Spots.Where(s => (includeBlindspot || !s.IsBlindspot) &&
                             (testEyes == "Bilateral" || testEyes == s.Eye)).ToList();
    }
}
