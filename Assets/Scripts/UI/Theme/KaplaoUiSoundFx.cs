using System;
using UnityEngine;
using KaplaoGames.Shared;

namespace KaplaoGames.UI.Theme
{
    /// <summary>
    /// Velvety procedural UI sound effects synthesizer.
    /// Generates gentle sinusoidal impulses on Awake with no clicks or harsh transients.
    /// Frame-rate independent, zero GC runtime playback, scales with KaplaoAudioPreference.SfxVolume.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Sound FX")]
    public class KaplaoUiSoundFx : MonoBehaviour
    {
        static KaplaoUiSoundFx instance;
        AudioSource audioSource;

        AudioClip hoverClip;
        AudioClip clickClip;
        AudioClip toggleOnClip;
        AudioClip toggleOffClip;
        AudioClip successClip;

        float lastHoverTime;
        const float HoverCooldown = 0.04f;

        public static KaplaoUiSoundFx Instance
        {
            get
            {
                if (instance == null)
                    EnsureInstance();
                return instance;
            }
        }

        static void EnsureInstance()
        {
            if (instance != null) return;
            instance = FindFirstObjectByType<KaplaoUiSoundFx>();
            if (instance != null) return;

            GameObject go = new GameObject("KaplaoUiSoundFx");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            instance = go.AddComponent<KaplaoUiSoundFx>();
        }

        void Awake()
        {
            if (instance == null)
                instance = this;
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D UI audio
            audioSource.loop = false;

            GenerateProceduralClips();
        }

        void GenerateProceduralClips()
        {
            hoverClip = CreateHoverClip();
            clickClip = CreateClickClip();
            toggleOnClip = CreateToggleClip(true);
            toggleOffClip = CreateToggleClip(false);
            successClip = CreateSuccessClip();
        }

        static AudioClip CreateHoverClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.035f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 110f); // Fast decay
                float wave = Mathf.Sin(2f * Mathf.PI * 1400f * t);
                data[i] = wave * env;
            }

            AudioClip clip = AudioClip.Create("UI_Hover", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateClickClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.05f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                // Velvet pop: gentle pitch drop from 520Hz down to 220Hz
                float freq = Mathf.Lerp(520f, 220f, progress);
                float env = Mathf.Exp(-t * 65f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = wave * env;
            }

            AudioClip clip = AudioClip.Create("UI_Click", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateToggleClip(bool on)
        {
            const int sampleRate = 44100;
            const float duration = 0.055f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];

            float startFreq = on ? 580f : 780f;
            float endFreq = on ? 880f : 480f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float freq = Mathf.Lerp(startFreq, endFreq, progress);
                float env = Mathf.Exp(-t * 55f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = wave * env;
            }

            AudioClip clip = AudioClip.Create(on ? "UI_ToggleOn" : "UI_ToggleOff", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateSuccessClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.22f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];

            const float note1Time = 0.0f;
            const float note2Time = 0.065f;
            const float freq1 = 587.33f; // D5
            const float freq2 = 880.00f; // A5

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                if (t >= note1Time)
                {
                    float t1 = t - note1Time;
                    float env1 = Mathf.Exp(-t1 * 18f);
                    sample += Mathf.Sin(2f * Mathf.PI * freq1 * t1) * env1 * 0.6f;
                }

                if (t >= note2Time)
                {
                    float t2 = t - note2Time;
                    float env2 = Mathf.Exp(-t2 * 14f);
                    sample += Mathf.Sin(2f * Mathf.PI * freq2 * t2) * env2 * 0.8f;
                }

                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("UI_Success", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void PlayHover()
        {
            if (!Application.isPlaying) return;
            EnsureInstance();
            if (instance == null || instance.audioSource == null) return;

            float now = Time.unscaledTime;
            if (now - instance.lastHoverTime < HoverCooldown)
                return;
            instance.lastHoverTime = now;

            float sfxVol = KaplaoAudioPreference.SfxVolume;
            if (sfxVol <= 0.001f) return;

            if (instance.hoverClip != null)
                instance.audioSource.PlayOneShot(instance.hoverClip, 0.08f * sfxVol);
        }

        public static void PlayClick()
        {
            if (!Application.isPlaying) return;
            EnsureInstance();
            if (instance == null || instance.audioSource == null) return;

            float sfxVol = KaplaoAudioPreference.SfxVolume;
            if (sfxVol <= 0.001f) return;

            if (instance.clickClip != null)
                instance.audioSource.PlayOneShot(instance.clickClip, 0.22f * sfxVol);
        }

        public static void PlayToggle(bool on)
        {
            if (!Application.isPlaying) return;
            EnsureInstance();
            if (instance == null || instance.audioSource == null) return;

            float sfxVol = KaplaoAudioPreference.SfxVolume;
            if (sfxVol <= 0.001f) return;

            AudioClip clip = on ? instance.toggleOnClip : instance.toggleOffClip;
            if (clip != null)
                instance.audioSource.PlayOneShot(clip, 0.18f * sfxVol);
        }

        public static void PlaySuccess()
        {
            if (!Application.isPlaying) return;
            EnsureInstance();
            if (instance == null || instance.audioSource == null) return;

            float sfxVol = KaplaoAudioPreference.SfxVolume;
            if (sfxVol <= 0.001f) return;

            if (instance.successClip != null)
                instance.audioSource.PlayOneShot(instance.successClip, 0.25f * sfxVol);
        }
    }
}
