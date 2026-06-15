using UnityEngine;

namespace RoadPlay
{
    /// <summary>Generates a short two-tone car-horn clip in code (no audio assets needed).</summary>
    public static class Honk
    {
        static AudioClip clip;

        public static AudioClip Clip()
        {
            if (clip != null) return clip;

            const int sampleRate = 44100;
            const float duration = 0.5f;
            int n = (int)(sampleRate * duration);
            var data = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;
                // quick attack, gentle release envelope
                float env = Mathf.Clamp01(Mathf.Min(t * 25f, (duration - t) * 8f));
                float freq = (t < 0.18f) ? 440f : 392f; // two-tone honk
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t));
                data[i] = square * 0.22f * env;
            }

            clip = AudioClip.Create("honk", n, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
