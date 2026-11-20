using UnityEngine;



namespace KaplaoGames.Shared

{

    public enum PublicLevelFileStorage : byte

    {

        /// <summary>Legacy: Supabase Storage (~50 MB/file on free).</summary>

        SupabaseStorage = 0,



        /// <summary>Timeweb Cloud S3 via KaplaoGamesWebsite presigned URLs (recommended).</summary>

        TimewebS3 = 1,



        /// <summary>Legacy: Cloudflare R2 worker.</summary>

        CloudflareR2Worker = 2

    }



    /// <summary>

    /// Place at Assets/Resources/PublicLevelCatalogConfig.asset

    /// Catalog metadata = Supabase. Level zips = Timeweb S3 via website.

    /// </summary>

    [CreateAssetMenu(fileName = "PublicLevelCatalogConfig", menuName = "Kaplao/Public Level Catalog Config")]

    public class PublicLevelCatalogConfig : ScriptableObject

    {

        [Header("Catalog (Supabase Postgres)")]

        [Tooltip("https://xxxx.supabase.co")]

        public string supabaseUrl = string.Empty;



        [Tooltip("anon public API key")]

        public string anonKey = string.Empty;



        [Tooltip("PostgREST table name")]

        public string levelsTable = "levels";



        [Header("File storage")]

        public PublicLevelFileStorage fileStorage = PublicLevelFileStorage.TimewebS3;



        [Tooltip("Legacy Supabase Storage bucket name")]

        public string storageBucket = "levels";



        [Header("Website (Timeweb S3 proxy)")]

        [Tooltip("KaplaoGamesWebsite base URL. Upload/download go through /api/levels/* on this site.")]

        public string websiteUrl = "https://kaplaogames.vercel.app";



        [Tooltip("Must match GAME_SERVER_SECRET on the website (same as Kaplao Account Config).")]

        public string gameServerSecret = string.Empty;



        [Header("Cloudflare Worker (legacy)")]

        public string cloudflareWorkerUrl = string.Empty;

        public string cloudflareUploadToken = string.Empty;



        [Header("Browse")]

        public int pageSize = 20;



        static PublicLevelCatalogConfig cached;



        public static PublicLevelCatalogConfig Load()

        {

            if (cached != null)

                return cached;



            cached = Resources.Load<PublicLevelCatalogConfig>("PublicLevelCatalogConfig");

            return cached;

        }



        public bool IsCatalogConfigured =>

            !string.IsNullOrWhiteSpace(supabaseUrl) &&

            !string.IsNullOrWhiteSpace(anonKey) &&

            !string.IsNullOrWhiteSpace(levelsTable);



        public bool IsFileStorageConfigured

        {

            get

            {

                switch (fileStorage)

                {

                    case PublicLevelFileStorage.TimewebS3:

                        return IsCatalogConfigured && !string.IsNullOrWhiteSpace(GameServerSecret);

                    case PublicLevelFileStorage.CloudflareR2Worker:

                        return !string.IsNullOrWhiteSpace(cloudflareWorkerUrl);

                    default:

                        return !string.IsNullOrWhiteSpace(storageBucket);

                }

            }

        }



        public bool IsConfigured => IsCatalogConfigured && IsFileStorageConfigured;



        public string RestBaseUrl => supabaseUrl.TrimEnd('/') + "/rest/v1/";

        public string StorageBaseUrl => supabaseUrl.TrimEnd('/') + "/storage/v1/object/";

        public string WorkerBaseUrl => cloudflareWorkerUrl.TrimEnd('/');

        /// <summary>Supabase Edge Functions for Timeweb upload/download (Unity resolves supabase.co reliably).</summary>
        public string LevelFunctionsBaseUrl => supabaseUrl.TrimEnd('/') + "/functions/v1";



        public string WebsiteUrl

        {

            get

            {

                if (!string.IsNullOrWhiteSpace(websiteUrl))

                    return websiteUrl.Trim().TrimEnd('/');



                if (KaplaoAccountConfig.Instance != null)

                    return KaplaoAccountConfig.Instance.WebsiteUrl;



                return string.Empty;

            }

        }



        public string GameServerSecret

        {

            get

            {

                if (!string.IsNullOrWhiteSpace(gameServerSecret))

                    return gameServerSecret.Trim();



                if (KaplaoAccountConfig.Instance != null)

                    return KaplaoAccountConfig.Instance.GameServerSecret;



                return string.Empty;

            }

        }

    }

}


