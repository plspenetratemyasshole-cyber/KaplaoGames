#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KaplaoGames.Editor
{
    static class SceneUiBinderUtility
    {
        public static Transform FindRelative(Transform root, string relativePath)
        {
            if (root == null || string.IsNullOrEmpty(relativePath))
                return null;

            return root.Find(relativePath);
        }

        public static T GetComponentOnPath<T>(Transform root, string relativePath) where T : Component
        {
            Transform target = FindRelative(root, relativePath);
            return target != null ? target.GetComponent<T>() : null;
        }

        public static void SetObject(SerializedObject serialized, string propertyName, Object value, StringBuilder report, string label)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                report.AppendLine($"  [skip] Property '{propertyName}' not found on component.");
                return;
            }

            property.objectReferenceValue = value;
            report.AppendLine(value != null
                ? $"  [ok] {label}"
                : $"  [missing] {label}");
        }

        public static void MarkDirty(Object target, UnityEngine.SceneManagement.Scene scene)
        {
            EditorUtility.SetDirty(target);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
