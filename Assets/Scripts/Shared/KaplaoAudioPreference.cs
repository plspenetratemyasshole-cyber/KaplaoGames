using UnityEngine;

namespace KaplaoGames.Shared
{
    public enum KaplaoMusicPack
    {
        TrickOrTreat = 0,
        Maf = 1
    }

    /// <summary>
    /// Local audio preferences (music pack + volumes) stored in PlayerPrefs.
    /// </summary>
    public static class KaplaoAudioPreference
    {
        const string PackKey = "kaplao.audio.musicPack";
        const string MusicVolumeKey = "kaplao.audio.musicVolume";
        const string SfxVolumeKey = "kaplao.audio.sfxVolume";

        public const float DefaultMusicVolume = 0.6f;
        public const float DefaultSfxVolume = 1f;

        public static KaplaoMusicPack MusicPack { get; private set; } = KaplaoMusicPack.Maf;
        public static float MusicVolume { get; private set; } = DefaultMusicVolume;
        public static float SfxVolume { get; private set; } = DefaultSfxVolume;

        public static void Load()
        {
            MusicPack = (KaplaoMusicPack)Mathf.Clamp(
                PlayerPrefs.GetInt(PackKey, (int)KaplaoMusicPack.Maf),
                0,
                1);
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        }

        public static void SetMusicPack(KaplaoMusicPack pack)
        {
            MusicPack = pack;
            PlayerPrefs.SetInt(PackKey, (int)pack);
            PlayerPrefs.Save();
        }

        public static void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.Save();
        }

        public static void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            PlayerPrefs.Save();
        }
    }
}
