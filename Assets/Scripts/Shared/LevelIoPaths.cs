using System;
using System.IO;
using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class LevelIoPaths
    {
        public const string Extension = "json";
        public const string DefaultFileName = "level.json";

        public static string LevelsDirectory
        {
            get
            {
                string directory;

                if (Application.isMobilePlatform)
                {
                    // Android/iOS-safe writable location.
                    directory = Path.Combine(Application.persistentDataPath, "Levels");
                }
                else
                {
                    directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "KaplaoGames",
                        "Levels");
                }

                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        /// <summary>
        /// Temporary cache for browse "Play" — not shown in the user's world list.
        /// </summary>
        public static string BrowsePlayDirectory
        {
            get
            {
                string directory;

                if (Application.isMobilePlatform)
                    directory = Path.Combine(Application.persistentDataPath, "BrowsePlay");
                else
                    directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "KaplaoGames",
                        "BrowsePlay");

                Directory.CreateDirectory(directory);
                return directory;
            }
        }
    }
}
