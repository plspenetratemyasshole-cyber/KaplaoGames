#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using KaplaoGames.Shared;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KaplaoGames.Editor
{
  public static class RunnerCharacterSetup
  {
    const string CharacterClipsFolder = "Assets/Sprites/Runner/Clips/Characters";
    const string OverridesFolder = "Assets/Sprites/Runner/Overrides";
    const string BaseControllerPath = "Assets/Sprites/Runner/RunnerController.controller";
    const string LibraryPath = "Assets/Resources/RunnerCharacterLibrary.asset";
    const string ResourcesFolder = "Assets/Resources";

    [MenuItem("Kaplao/Setup Runner Characters")]
    public static void SetupRunnerCharacters()
    {
      AnimatorController baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
      if (baseController == null)
      {
        Debug.LogError("[Kaplao] Base RunnerController not found. Run Kaplao → Setup Runner Animations first.");
        return;
      }

      EnsureFolder(CharacterClipsFolder);
      EnsureFolder(OverridesFolder);
      EnsureFolder(ResourcesFolder);

      AnimationClip baseIdle = LoadClip("Assets/Sprites/Runner/Clips/runner_idle.anim");
      AnimationClip baseWalk = LoadClip("Assets/Sprites/Runner/Clips/runner_walk.anim");
      AnimationClip baseJump = LoadClip("Assets/Sprites/Runner/Clips/runner_jump.anim");
      AnimationClip baseDead = LoadClip("Assets/Sprites/Runner/Clips/runner_dead.anim");

      if (baseIdle == null || baseWalk == null || baseJump == null || baseDead == null)
      {
        Debug.LogError("[Kaplao] Base animation clips are missing. Run Kaplao → Setup Runner Animations first.");
        return;
      }

      var overrides = new List<AnimatorOverrideController>();
      IReadOnlyList<string> publicFolders = RunnerSpriteEditorUtil.GetPublicCharacterFolders();
      IReadOnlyList<string> adminFolders = RunnerSpriteEditorUtil.GetAdminCharacterFolders();
      var characterFolders = new List<string>();
      characterFolders.AddRange(publicFolders);
      characterFolders.AddRange(adminFolders);

      if (characterFolders.Count == 0)
      {
        Debug.LogWarning("[Kaplao] No character folders found under Assets/Sprites/Runner/. " +
                         "Create folders like Knight, Rogue, Wizard with runner_idle/runner_walk(or runner_run)/runner_jump/runner_dead PNGs.");
      }

      for (int i = 0; i < characterFolders.Count; i++)
      {
        string spritesDir = characterFolders[i];
        string folderName = Path.GetFileName(spritesDir);

        AnimationClip idleClip = CreateCharacterClip(folderName, "runner_idle",
          new[] { "runner_idle", "Idle", "idle" }, new[] { "Idle_" }, 8f, true, spritesDir);
        AnimationClip walkClip = CreateCharacterClip(folderName, "runner_walk",
          new[] { "runner_walk", "runner_run", "Walk", "walk", "Run", "run" }, new[] { "Run_", "Walk_" }, 12f, true, spritesDir);
        AnimationClip jumpClip = CreateCharacterClip(folderName, "runner_jump",
          new[] { "runner_jump", "Jump", "jump" }, new[] { "Jump_" }, 14f, false, spritesDir);
        AnimationClip deadClip = CreateCharacterClip(folderName, "runner_dead",
          new[] { "runner_dead", "Dead", "dead" }, new[] { "Dead_" }, 10f, false, spritesDir);

        if (idleClip == null || walkClip == null || jumpClip == null || deadClip == null)
        {
          Debug.LogWarning($"[Kaplao] Skipped character '{folderName}' — missing textures or sliced sprites.");
          continue;
        }

        AnimatorOverrideController overrideController =
          CreateOrUpdateOverride(folderName, baseController, baseIdle, baseWalk, baseJump, baseDead,
            idleClip, walkClip, jumpClip, deadClip);
        if (overrideController != null)
          overrides.Add(overrideController);
      }

      RunnerCharacterLibrary library = AssetDatabase.LoadAssetAtPath<RunnerCharacterLibrary>(LibraryPath);
      if (library == null)
      {
        library = ScriptableObject.CreateInstance<RunnerCharacterLibrary>();
        AssetDatabase.CreateAsset(library, LibraryPath);
      }

      SerializedObject serializedLibrary = new SerializedObject(library);
      SerializedProperty charactersProperty = serializedLibrary.FindProperty("characters");
      charactersProperty.arraySize = overrides.Count;
      for (int i = 0; i < overrides.Count; i++)
        charactersProperty.GetArrayElementAtIndex(i).objectReferenceValue = overrides[i];
      SerializedProperty publicCountProperty = serializedLibrary.FindProperty("publicCharacterCount");
      if (publicCountProperty != null)
        publicCountProperty.intValue = publicFolders.Count;
      serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(library);

      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();

      Debug.Log($"[Kaplao] Runner character library updated with {overrides.Count} character(s) " +
                $"({publicFolders.Count} public + {adminFolders.Count} admin).");
    }

    static AnimatorOverrideController CreateOrUpdateOverride(
      string characterName,
      AnimatorController baseController,
      AnimationClip baseIdle,
      AnimationClip baseWalk,
      AnimationClip baseJump,
      AnimationClip baseDead,
      AnimationClip idleClip,
      AnimationClip walkClip,
      AnimationClip jumpClip,
      AnimationClip deadClip)
    {
      string path = $"{OverridesFolder}/{characterName}.overrideController";
      AnimatorOverrideController overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
      if (overrideController == null)
      {
        overrideController = new AnimatorOverrideController
        {
          runtimeAnimatorController = baseController,
          name = $"{characterName}Override"
        };
        AssetDatabase.CreateAsset(overrideController, path);
      }
      else
      {
        overrideController.runtimeAnimatorController = baseController;
      }

      var list = new List<KeyValuePair<AnimationClip, AnimationClip>>
      {
        new KeyValuePair<AnimationClip, AnimationClip>(baseIdle, idleClip),
        new KeyValuePair<AnimationClip, AnimationClip>(baseWalk, walkClip),
        new KeyValuePair<AnimationClip, AnimationClip>(baseJump, jumpClip),
        new KeyValuePair<AnimationClip, AnimationClip>(baseDead, deadClip)
      };
      overrideController.ApplyOverrides(list);
      EditorUtility.SetDirty(overrideController);
      Debug.Log($"[Kaplao] Character override ready: {path}");
      return overrideController;
    }

    static AnimationClip CreateCharacterClip(
      string characterName,
      string clipFileName,
      string[] textureBaseNames,
      string[] spritePrefixes,
      float fps,
      bool loop,
      string spritesFolder)
    {
      string texturePath = RunnerSpriteEditorUtil.ResolveTexturePath(textureBaseNames, spritesFolder);
      if (texturePath == null)
        return null;

      Sprite[] sprites = LoadSprites(texturePath, spritePrefixes);
      if (sprites.Length == 0)
        return null;

      EnsureFolder($"{CharacterClipsFolder}/{characterName}");
      string clipPath = $"{CharacterClipsFolder}/{characterName}/{clipFileName}.anim";
      AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
      if (clip == null)
      {
        clip = new AnimationClip();
        AssetDatabase.CreateAsset(clip, clipPath);
      }

      clip.frameRate = fps;
      var settings = AnimationUtility.GetAnimationClipSettings(clip);
      settings.loopTime = loop;
      AnimationUtility.SetAnimationClipSettings(clip, settings);

      var binding = new EditorCurveBinding
      {
        type = typeof(SpriteRenderer),
        path = string.Empty,
        propertyName = "m_Sprite"
      };

      var keys = new ObjectReferenceKeyframe[sprites.Length];
      for (int i = 0; i < sprites.Length; i++)
      {
        keys[i] = new ObjectReferenceKeyframe
        {
          time = i / fps,
          value = sprites[i]
        };
      }

      AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
      EditorUtility.SetDirty(clip);
      return clip;
    }

    static AnimationClip LoadClip(string path) => AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

    static void EnsureFolder(string path)
    {
      if (AssetDatabase.IsValidFolder(path))
        return;

      string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
      string leaf = Path.GetFileName(path);
      if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        EnsureFolder(parent);

      AssetDatabase.CreateFolder(parent, leaf);
    }

    static Sprite[] LoadSprites(string texturePath, string[] prefixes)
    {
      UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
      var sprites = new List<Sprite>();
      var fallbackSprites = new List<Sprite>();
      for (int i = 0; i < assets.Length; i++)
      {
        if (assets[i] is not Sprite sprite)
          continue;

        fallbackSprites.Add(sprite);

        if (prefixes == null || prefixes.Length == 0)
        {
          sprites.Add(sprite);
          continue;
        }

        for (int p = 0; p < prefixes.Length; p++)
        {
          if (sprite.name.StartsWith(prefixes[p], StringComparison.Ordinal))
          {
            sprites.Add(sprite);
            break;
          }
        }
      }

      if (sprites.Count == 0)
        sprites.AddRange(fallbackSprites);

      sprites.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
      return sprites.ToArray();
    }
  }
}
#endif
