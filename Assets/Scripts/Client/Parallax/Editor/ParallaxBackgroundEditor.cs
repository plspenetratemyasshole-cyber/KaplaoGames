#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KaplaoGames.Client.Editor
{
    [CustomEditor(typeof(ParallaxBackground))]
    public class ParallaxBackgroundEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "After changing layers, press Rebuild Layers.\n" +
                "Color alpha must be 255 — alpha 0 makes the layer invisible.\n" +
                "Y Position is offset from camera center (layer follows camera vertically).\n" +
                "Put ParallaxBackground in the game scene (SampleScene), not MainMenu.",
                MessageType.Info);

            var parallax = (ParallaxBackground)target;
            if (GUILayout.Button("Rebuild Layers"))
                parallax.RebuildLayers();
        }
    }
}
#endif
