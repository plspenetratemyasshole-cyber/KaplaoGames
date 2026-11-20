#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KaplaoGames.Editor
{
  static class RunnerSpriteEditorUtil
  {
    public const string SpritesFolder = "Assets/Sprites/Runner";

    static readonly string[] IgnoredSubfolders =
    {
      "Clips",
      "Overrides",
      "Characters"
    };

    public static bool IsAdminCharacterFolderName(string folderName)
    {
      return !string.IsNullOrEmpty(folderName) &&
             folderName.StartsWith("Admin_", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> GetPublicCharacterFolders()
    {
      return FilterCharacterFolders(includeAdmin: false);
    }

    public static IReadOnlyList<string> GetAdminCharacterFolders()
    {
      return FilterCharacterFolders(includeAdmin: true, adminOnly: true);
    }

    /// <summary>Public skins first (alphabetical), then admin skins (alphabetical).</summary>
    public static IReadOnlyList<string> GetLibraryCharacterFolders()
    {
      var folders = new List<string>();
      folders.AddRange(GetPublicCharacterFolders());
      folders.AddRange(GetAdminCharacterFolders());
      return folders;
    }

    public static IReadOnlyList<string> GetCharacterFolders()
    {
      return GetLibraryCharacterFolders();
    }

    static IReadOnlyList<string> FilterCharacterFolders(bool includeAdmin, bool adminOnly = false)
    {
      var folders = new List<string>();
      string absoluteRoot = Path.GetFullPath(SpritesFolder);
      if (!Directory.Exists(absoluteRoot))
        return folders;

      foreach (string directory in Directory.GetDirectories(absoluteRoot))
      {
        string folderName = Path.GetFileName(directory);
        if (Array.IndexOf(IgnoredSubfolders, folderName) >= 0)
          continue;

        bool isAdmin = IsAdminCharacterFolderName(folderName);
        if (adminOnly && !isAdmin)
          continue;
        if (!includeAdmin && isAdmin)
          continue;

        string assetPath = $"{SpritesFolder}/{folderName}";
        if (AssetDatabase.IsValidFolder(assetPath))
          folders.Add(assetPath);
      }

      folders.Sort(StringComparer.OrdinalIgnoreCase);
      return folders;
    }

    public static string ResolveReferenceCharacterFolder()
    {
      IReadOnlyList<string> folders = GetPublicCharacterFolders();
      for (int i = 0; i < folders.Count; i++)
      {
        if (HasCoreAnimationTextures(folders[i]))
          return folders[i];
      }

      if (HasCoreAnimationTextures(SpritesFolder))
        return SpritesFolder;

      return folders.Count > 0 ? folders[0] : SpritesFolder;
    }

    public static string ResolveTexturePath(string[] baseNames, string preferredFolder = null)
    {
      if (!string.IsNullOrEmpty(preferredFolder))
      {
        string preferredPath = TryNamesInFolder(preferredFolder, baseNames);
        if (preferredPath != null)
          return preferredPath;
      }

      string rootPath = TryNamesInFolder(SpritesFolder, baseNames);
      if (rootPath != null)
        return rootPath;

      IReadOnlyList<string> folders = GetCharacterFolders();
      for (int i = 0; i < folders.Count; i++)
      {
        string path = TryNamesInFolder(folders[i], baseNames);
        if (path != null)
          return path;
      }

      return null;
    }

    static string TryNamesInFolder(string folder, string[] baseNames)
    {
      for (int i = 0; i < baseNames.Length; i++)
      {
        string path = $"{folder}/{baseNames[i]}.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
          return path;
      }

      return null;
    }

    static bool HasCoreAnimationTextures(string folder)
    {
      return ResolveTexturePath(new[] { "runner_idle", "Idle", "idle" }, folder) != null &&
             ResolveTexturePath(new[] { "runner_walk", "runner_run", "Walk", "walk", "Run", "run" }, folder) != null &&
             ResolveTexturePath(new[] { "runner_jump", "Jump", "jump" }, folder) != null &&
             ResolveTexturePath(new[] { "runner_dead", "Dead", "dead" }, folder) != null;
    }
  }
}
#endif
