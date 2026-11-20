using KaplaoGames.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Persistent music player. Assign 4 clips in the Inspector (menu/gameplay × Trick or Treat / Maf).
    /// Created by Kaplao/Setup Audio System.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/Audio/Kaplao Audio Manager")]
    public class KaplaoAudioManager : MonoBehaviour
    {
        const string MainMenuSceneName = "MainMenu";

        public static KaplaoAudioManager Instance { get; private set; }

        [Header("Trick or Treat")]
        [SerializeField] AudioClip trickOrTreatMenu;
        [SerializeField] AudioClip trickOrTreatGameplay;

        [Header("Maf")]
        [SerializeField] AudioClip mafMenu;
        [SerializeField] AudioClip mafGameplay;

        [Header("Playback")]
        [SerializeField] [Range(0f, 1f)] float inspectorMusicGain = 1f;

        AudioSource musicSource;
        bool playingGameplay;

        public static KaplaoAudioManager Ensure()
        {
            if (Instance != null)
                return Instance;

            KaplaoAudioManager existing = Object.FindFirstObjectByType<KaplaoAudioManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("KaplaoAudioManager");
            return go.AddComponent<KaplaoAudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
                musicSource = gameObject.AddComponent<AudioSource>();

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            KaplaoAudioPreference.Load();
            ApplyMusicVolume();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Start()
        {
            RefreshForActiveScene();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshForActiveScene();
        }

        public void RefreshForActiveScene()
        {
            bool gameplay = !IsMenuScene(SceneManager.GetActiveScene().name);
            PlayForContext(gameplay);
        }

        public void ApplyPreferences()
        {
            ApplyMusicVolume();
            PlayForContext(playingGameplay);
        }

        public void SetMusicPack(KaplaoMusicPack pack)
        {
            KaplaoAudioPreference.SetMusicPack(pack);
            PlayForContext(playingGameplay);
        }

        public void SetMusicVolume(float volume)
        {
            KaplaoAudioPreference.SetMusicVolume(volume);
            ApplyMusicVolume();
        }

        public void SetSfxVolume(float volume)
        {
            KaplaoAudioPreference.SetSfxVolume(volume);
        }

        void ApplyMusicVolume()
        {
            if (musicSource == null)
                return;

            musicSource.volume = KaplaoAudioPreference.MusicVolume * Mathf.Clamp01(inspectorMusicGain);
        }

        void PlayForContext(bool gameplay)
        {
            playingGameplay = gameplay;
            AudioClip clip = ResolveClip(gameplay);
            if (clip == null)
            {
                if (musicSource != null)
                    musicSource.Stop();
                return;
            }

            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                ApplyMusicVolume();
                return;
            }

            musicSource.clip = clip;
            ApplyMusicVolume();
            musicSource.Play();
        }

        AudioClip ResolveClip(bool gameplay)
        {
            bool maf = KaplaoAudioPreference.MusicPack == KaplaoMusicPack.Maf;
            if (gameplay)
                return maf ? mafGameplay : trickOrTreatGameplay;
            return maf ? mafMenu : trickOrTreatMenu;
        }

        static bool IsMenuScene(string sceneName)
        {
            return string.Equals(sceneName, MainMenuSceneName, System.StringComparison.Ordinal);
        }
    }
}
