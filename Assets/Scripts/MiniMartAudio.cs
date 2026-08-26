using System.Collections.Generic;
using UnityEngine;

namespace MiniMart
{
    public enum SfxKind { Harvest, Stock, Sale, Deny, Upgrade, Unhappy, DayEnd, Register }

    /// <summary>
    /// Generates every sound effect procedurally at boot, so the project needs no audio assets
    /// and still gives the player feedback for each action.
    /// </summary>
    public class MiniMartAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private readonly Dictionary<SfxKind, AudioClip> clips = new Dictionary<SfxKind, AudioClip>();
        private AudioSource source;

        public static MiniMartAudio Create(Transform parent)
        {
            GameObject root = new GameObject("MiniMart_Audio");
            root.transform.SetParent(parent, false);
            MiniMartAudio audio = root.AddComponent<MiniMartAudio>();
            audio.Build();
            return audio;
        }

        private void Build()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.45f;

            clips[SfxKind.Harvest] = Sweep("Sfx_Harvest", 0.16f, 520f, 880f, 0.5f, false);
            clips[SfxKind.Stock] = Sweep("Sfx_Stock", 0.13f, 300f, 190f, 0.55f, false);
            clips[SfxKind.Deny] = Sweep("Sfx_Deny", 0.2f, 190f, 120f, 0.42f, true);
            clips[SfxKind.Unhappy] = Sweep("Sfx_Unhappy", 0.34f, 420f, 160f, 0.45f, true);
            clips[SfxKind.Register] = Sweep("Sfx_Register", 0.09f, 1180f, 1180f, 0.35f, true);
            clips[SfxKind.Sale] = Arpeggio("Sfx_Sale", new[] { 784f, 1046f }, 0.1f, 0.45f, false);
            clips[SfxKind.Upgrade] = Arpeggio("Sfx_Upgrade", new[] { 523f, 659f, 784f, 1046f }, 0.09f, 0.45f, false);
            clips[SfxKind.DayEnd] = Arpeggio("Sfx_DayEnd", new[] { 659f, 523f, 392f, 523f }, 0.16f, 0.4f, false);
        }

        public void Play(SfxKind kind)
        {
            if (source == null || !clips.TryGetValue(kind, out AudioClip clip) || clip == null) return;
            source.PlayOneShot(clip);
        }

        /// <summary>One tone gliding from startHz to endHz with a soft attack and decay.</summary>
        private static AudioClip Sweep(string name, float duration, float startHz, float endHz, float volume, bool square)
        {
            int samples = Mathf.Max(64, (int)(duration * SampleRate));
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float hz = Mathf.Lerp(startHz, endHz, t);
                phase += hz / SampleRate * Mathf.PI * 2f;
                float wave = square ? (Mathf.Sin(phase) >= 0f ? 0.6f : -0.6f) : Mathf.Sin(phase);
                data[i] = wave * Envelope(t) * volume;
            }
            return ToClip(name, data);
        }

        /// <summary>A short run of notes, used for sales and upgrades.</summary>
        private static AudioClip Arpeggio(string name, float[] notes, float noteDuration, float volume, bool square)
        {
            int perNote = Mathf.Max(64, (int)(noteDuration * SampleRate));
            float[] data = new float[perNote * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            {
                float phase = 0f;
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)perNote;
                    phase += notes[n] / SampleRate * Mathf.PI * 2f;
                    float wave = square ? (Mathf.Sin(phase) >= 0f ? 0.6f : -0.6f) : Mathf.Sin(phase);
                    data[n * perNote + i] = wave * Envelope(t) * volume;
                }
            }
            return ToClip(name, data);
        }

        private static float Envelope(float t) => Mathf.Min(1f, t / 0.06f) * Mathf.Pow(1f - t, 1.6f);

        private static AudioClip ToClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
