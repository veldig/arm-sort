using UnityEngine;

namespace ArmSort
{
    // Generates short sound effects in code, so the game ships with no audio files.
    public static class Sfx
    {
        const int Rate = 44100;

        public static AudioClip Notes(string name, float[] freqs, float noteLen, bool square = false, float volume = 0.35f)
        {
            int per = Mathf.CeilToInt(noteLen * Rate);
            int tail = Mathf.CeilToInt(0.06f * Rate);
            int total = per * freqs.Length + tail;
            var data = new float[total];

            for (int n = 0; n < freqs.Length; n++)
            {
                float f = freqs[n];
                int start = n * per;
                int len = per + tail;
                for (int i = 0; i < len && start + i < total; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 4f / noteLen);
                    float s = Mathf.Sin(2f * Mathf.PI * f * t);
                    if (square) s = Mathf.Sign(s) * 0.5f;
                    data[start + i] += s * env * volume;
                }
            }

            var clip = AudioClip.Create(name, total, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
