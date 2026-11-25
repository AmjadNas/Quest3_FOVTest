using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace FOVTest
{
    /// <summary>Summary statistics. Every formula here reproduces the reference log's Summary from its
    /// History (see CoreTests.Summary_ReproducesReferenceLog).</summary>
    public static class SummaryBuilder
    {
        /// <summary>A response within 30 deg either side is correct, so a blind guess is right 1 time in 6.</summary>
        const float GuessCorrectProbability = 1f / 6f;

        public static SummaryLog Build(IReadOnlyList<TrialLog> history, int totalLocations,
            float testDurationSec, float tutorialDurationSec, float backgroundBrightness,
            float responseTimeoutSec, TestLayout layout, DateTime end, long endUnix)
        {
            bool Accepted(TrialLog t) => !t.Invalid && t.Seen;
            float Mean(IEnumerable<TrialLog> ts, Func<TrialLog, float> f)
            {
                var l = ts.ToList();
                return l.Count == 0 ? 0f : (float)l.Average(t => (double)f(t));
            }

            var byLocation = history.GroupBy(t => t.Spot.LocationID).ToList();   // in order of appearance
            var tested = byLocation.Where(g => g.Any(t => !t.Invalid)).ToList();
            int numIncorrect = history.Count(t => !t.Invalid && t.Responded && !t.Seen);

            var s = new SummaryLog
            {
                TestDate = end.ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                TestUnixTime = endUnix,
                TestMinutes = (int)(testDurationSec / 60f),
                TestSeconds = (int)(testDurationSec % 60f),
                TutorialDurationSec = tutorialDurationSec,
                TestDurationSec = testDurationSec,
                TotalProcedureDurationSec = tutorialDurationSec + testDurationSec,
                BackgroundBrightness = backgroundBrightness,
                TestContrasts = history.Select(t => t.Spot.ContrastEst).Distinct().ToList(),
                NumStimuliPresented = history.Count(t => !t.Invalid),
                NumInitialMiss = byLocation.Count(g => g.Count() > 1 && !Accepted(g.First())),
                NumLocationsTested = tested.Count,
                SpotsSeen = tested.Count(g => g.Any(Accepted)),
                SpotsMissed = tested.Count(g => !g.Any(Accepted)),
                SpotsUntested = totalLocations - tested.Count,
                NumIncorrectResponse = numIncorrect,
                MeanSeenResponseTimeSec = Mean(history.Where(Accepted), t => t.ResponseTimeSec),
                MeanIncorrectResponseTimeSec =
                    Mean(history.Where(t => t.Responded && !Accepted(t)), t => t.ResponseTimeSec),
                MeanNoResponseTimeSec = Mean(history.Where(t => !t.Responded), t => t.ResponseTimeSec),
                Layout = layout.FileName,
                Reliability = 0f,
                WaitTimeBeforeMissedSec = responseTimeoutSec,
                LayoutData = Escape(layout.Csv),
                LayoutMetaJson = layout.MetaJson,
                LayoutDescription = layout.Name,
            };
            if (history.Count > 0)
            {
                s.PercentCorrect = history.Count(Accepted) / (float)history.Count;
                s.GuessRate = numIncorrect / (1f - GuessCorrectProbability) / history.Count;
                s.MeanTrialDurationSec = testDurationSec / history.Count;
            }
            foreach (var g in tested)
            {
                var valid = g.Where(t => !t.Invalid).ToList();
                var spot = valid[0].Spot;
                s.TestLocations.Add(new TestLocationLog
                {
                    LocationID = g.Key,
                    Eye = spot.Eye,
                    XDegrees = (float)Math.Round(spot.XDegrees, 4),
                    YDegrees = (float)Math.Round(spot.YDegrees, 4),
                    PercentCorrect = valid.Count(t => t.Seen) / (float)valid.Count,
                    TrialIndices = valid.Select(t => t.TrialIndex).ToList(),
                });
            }
            return s;
        }

        /// <summary>The reference stores the CSV already escaped (literal \n and \").</summary>
        public static string Escape(string csv)
        {
            var q = JsonConvert.ToString(csv);
            return q.Substring(1, q.Length - 2);
        }
    }
}
