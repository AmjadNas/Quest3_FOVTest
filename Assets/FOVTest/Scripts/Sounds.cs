using UnityEngine;

namespace FOVTest
{
    /// <summary>Short synthesised cues: target locked, right area, wrong area, no answer.</summary>
    public class Sounds
    {
        const int Rate = 24000;
        readonly AudioSource source;
        readonly AudioClip locked, right, wrong, none;

        public Sounds(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            locked = Tone("locked", 0.05f, 1000f);
            right = Tone("right", 0.09f, 660f, 990f);
            wrong = Tone("wrong", 0.12f, 260f, 200f);
            none = Tone("none", 0.12f, 420f);
        }

        /// <summary>0 to 100, as SoundFXVolume in the log.</summary>
        public float Volume { set => source.volume = Mathf.Clamp01(value / 100f); }

        public void Locked() => source.PlayOneShot(locked);

        public void Answer(Outcome o) =>
            source.PlayOneShot(o == Outcome.Seen ? right : o == Outcome.Incorrect ? wrong : none);

        static AudioClip Tone(string name, float secondsEach, params float[] hz)
        {
            int n = (int)(secondsEach * Rate);
            var data = new float[n * hz.Length];
            for (int k = 0; k < hz.Length; k++)
                for (int i = 0; i < n; i++)
                {
                    float env = Mathf.Sin(Mathf.PI * i / n);     // no clicks
                    data[k * n + i] = 0.95f * env * Mathf.Sin(2f * Mathf.PI * hz[k] * i / Rate);
                }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
