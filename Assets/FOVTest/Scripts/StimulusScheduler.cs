using System;
using System.Collections.Generic;
using System.Linq;

namespace FOVTest
{
    public enum Outcome { Seen, Incorrect, NoResponse, Invalid }

    /// <summary>Random interleaving of every eye and location. Misses are retested later, up to the
    /// layout's limit; invalid trials (dropped frames, headset removed) are repeated later.</summary>
    public class StimulusScheduler
    {
        readonly List<SpotDef> queue = new List<SpotDef>();
        readonly Dictionary<int, int> retests = new Dictionary<int, int>();   // spot id -> retests used
        readonly Random rng;

        public int TotalLocations { get; }
        public int CountedTrials { get; private set; }       // valid trials so far (next TrialIndex)
        public int LocationsDone => TotalLocations - queue.Select(q => q.Id).Distinct().Count();

        public StimulusScheduler(IEnumerable<SpotDef> spots, int? seed = null)
        {
            rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var all = spots.ToList();
            TotalLocations = all.Count;
            foreach (var s in all)
                for (int i = 0; i < Math.Max(1, s.TimesToTest); i++) queue.Add(s);
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
        }

        public bool HasNext => queue.Count > 0;
        public SpotDef Next() => queue[0];

        /// <summary>Report the outcome of the spot returned by Next().</summary>
        public void Report(Outcome outcome)
        {
            var s = queue[0];
            queue.RemoveAt(0);
            if (outcome == Outcome.Invalid)
            {
                Reinsert(s);
                return;
            }
            CountedTrials++;
            if (outcome == Outcome.Seen) return;
            int used = retests.TryGetValue(s.Id, out var r) ? r : 0;
            if (used < s.TimesToRetestIfMissed)
            {
                retests[s.Id] = used + 1;
                Reinsert(s);
            }
        }

        // Back into the queue at a random position, but never straight away when other spots remain.
        void Reinsert(SpotDef s)
        {
            int from = Math.Min(1, queue.Count);
            queue.Insert(rng.Next(from, queue.Count + 1), s);
        }
    }
}
