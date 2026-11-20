#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using KaplaoGames.Client;
using Mirror;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KaplaoGames.Editor
{
    public static class RunnerAnimationSetup
    {
        const string SpritesFolder = "Assets/Sprites/Runner";
        const string ClipsFolder = "Assets/Sprites/Runner/Clips";
        const string ControllerPath = "Assets/Sprites/Runner/RunnerController.controller";
        const string RunnerPrefabPath = "Assets/Prefabs/Runner.prefab";

        [MenuItem("Kaplao/Apply Pixel Art Import (Runner Sprites)")]
        public static void ApplyPixelArtImportSettings()
        {
            int changed = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                bool dirty = false;
                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    dirty = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    dirty = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            Debug.Log($"[Kaplao] Pixel art import applied to {changed} texture(s) in {SpritesFolder}.");
        }

        [MenuItem("Kaplao/Setup Runner Animations")]
        public static void SetupRunnerAnimations()
        {
            EnsureFolder(ClipsFolder);

            string referenceFolder = RunnerSpriteEditorUtil.ResolveReferenceCharacterFolder();
            Debug.Log($"[Kaplao] Building base runner animations from '{referenceFolder}'.");

            AnimationClip idleClip = CreateSpriteClip("runner_idle",
                new[] { "runner_idle", "Idle", "idle" },
                new[] { "Idle_" },
                8f, true);
            AnimationClip walkClip = CreateSpriteClip("runner_walk",
                new[] { "runner_walk", "runner_run", "Walk", "walk", "Run", "run" },
                new[] { "Run_", "Walk_" },
                12f, true);
            AnimationClip jumpClip = CreateSpriteClip("runner_jump",
                new[] { "runner_jump", "Jump", "jump" },
                new[] { "Jump_" },
                14f, false);
            AnimationClip deadClip = CreateSpriteClip("runner_dead",
                new[] { "runner_dead", "Dead", "dead" },
                new[] { "Dead_" },
                10f, false);

            if (idleClip == null || walkClip == null || jumpClip == null || deadClip == null)
            {
                Debug.LogError("[Kaplao] Setup failed: one or more clips are empty. " +
                               "Check Console for missing PNG paths in Assets/Sprites/Runner.");
                return;
            }

            AnimatorController controller = CreateController(idleClip, walkClip, jumpClip, deadClip);
            WireRunnerPrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ApplyPixelArtImportSettings();
            Debug.Log("[Kaplao] Runner animations ready. Press Play and test as Runner.");
        }

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

        static AnimationClip CreateSpriteClip(
            string clipName,
            string[] textureBaseNames,
            string[] spritePrefixes,
            float fps,
            bool loop)
        {
            string texturePath = RunnerSpriteEditorUtil.ResolveTexturePath(
                textureBaseNames,
                RunnerSpriteEditorUtil.ResolveReferenceCharacterFolder());
            if (texturePath == null)
            {
                Debug.LogError($"[Kaplao] Texture not found for {clipName}. " +
                               $"Expected one of: {string.Join(", ", textureBaseNames)}.png under {RunnerSpriteEditorUtil.SpritesFolder}");
                return null;
            }

            Sprite[] sprites = LoadSprites(texturePath, spritePrefixes);
            if (sprites.Length == 0)
            {
                Debug.LogError($"[Kaplao] No usable sprites in {texturePath}. " +
                               "Make sure the PNG is sliced, or that its sub-sprite names match the expected prefixes.");
                return null;
            }

            string clipPath = $"{ClipsFolder}/{clipName}.anim";
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
            Debug.Log($"[Kaplao] Created {clipName}.anim from {texturePath} ({sprites.Length} frames).");
            return clip;
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

        static AnimatorController CreateController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip jumpClip,
            AnimationClip deadClip)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Grabbed", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine root = controller.layers[0].stateMachine;
            AnimatorState idle = root.AddState("Idle", new Vector3(300f, 0f, 0f));
            AnimatorState walk = root.AddState("Walk", new Vector3(300f, 80f, 0f));
            AnimatorState jump = root.AddState("Jump", new Vector3(540f, 40f, 0f));
            AnimatorState dead = root.AddState("Dead", new Vector3(540f, -80f, 0f));

            idle.motion = idleClip;
            walk.motion = walkClip;
            jump.motion = jumpClip;
            dead.motion = deadClip;

            root.defaultState = idle;

            AddTransition(idle, walk, AnimatorConditionMode.Greater, 0.15f, "Speed", false);
            AddTransition(walk, idle, AnimatorConditionMode.Less, 0.15f, "Speed", false);

            AddBoolTransition(idle, jump, false, "Grounded");
            AddBoolTransition(walk, jump, false, "Grounded");
            AddBoolTransition(jump, idle, true, "Grounded", extraMode: AnimatorConditionMode.Less, extraThreshold: 0.15f, extraParam: "Speed");
            AddBoolTransition(jump, walk, true, "Grounded", extraMode: AnimatorConditionMode.Greater, extraThreshold: 0.15f, extraParam: "Speed");

            AnimatorStateTransition jumpReplay = jump.AddTransition(jump);
            jumpReplay.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            jumpReplay.hasExitTime = false;
            jumpReplay.duration = 0.05f;
            jumpReplay.canTransitionToSelf = true;

            AnimatorStateTransition anyToDead = root.AddAnyStateTransition(dead);
            anyToDead.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            anyToDead.hasExitTime = false;
            anyToDead.duration = 0.05f;
            anyToDead.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void AddTransition(
            AnimatorState from,
            AnimatorState to,
            AnimatorConditionMode mode,
            float threshold,
            string parameter,
            bool hasExitTime)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.AddCondition(mode, threshold, parameter);
            transition.hasExitTime = hasExitTime;
            transition.duration = 0.05f;
        }

        static void AddBoolTransition(
            AnimatorState from,
            AnimatorState to,
            bool value,
            string parameter,
            AnimatorConditionMode extraMode = AnimatorConditionMode.If,
            float extraThreshold = 0f,
            string extraParam = null)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.AddCondition(
                value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameter);

            if (!string.IsNullOrEmpty(extraParam))
                transition.AddCondition(extraMode, extraThreshold, extraParam);

            transition.hasExitTime = false;
            transition.duration = 0.05f;
        }

        static void WireRunnerPrefab(AnimatorController controller)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Kaplao] Runner prefab not found.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(RunnerPrefabPath);
            try
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

                Transform visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    Debug.LogError("[Kaplao] Runner prefab has no Visual child.");
                    return;
                }

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null)
                    animator = visual.gameObject.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;

                NetworkAnimator networkAnimator = root.GetComponent<NetworkAnimator>();
                if (networkAnimator == null)
                    networkAnimator = root.AddComponent<NetworkAnimator>();

                networkAnimator.clientAuthority = true;
                networkAnimator.animator = animator;

                RunnerAnimationClient animationClient = root.GetComponent<RunnerAnimationClient>();
                if (animationClient == null)
                    animationClient = root.AddComponent<RunnerAnimationClient>();

                if (root.GetComponent<RunnerAppearanceClient>() == null)
                    root.AddComponent<RunnerAppearanceClient>();

                if (root.GetComponent<RunnerLocalSortingClient>() == null)
                    root.AddComponent<RunnerLocalSortingClient>();

                SerializedObject serialized = new SerializedObject(animationClient);
                serialized.FindProperty("visualRoot").objectReferenceValue = visual;
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, RunnerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
