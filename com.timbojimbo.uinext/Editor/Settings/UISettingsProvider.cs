using System.Collections.Generic;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// Edit > Project Settings > Timbo Jimbo > UI Next: picks the active <see cref="UISettings"/> asset and edits its fields
    /// in place. The choice is an EditorBuildSettings config object, so it lives in ProjectSettings and
    /// <see cref="UISettingsBuild"/> can preload the asset into builds.
    /// </summary>
    internal static class UISettingsProvider
    {
        /// <summary>The registered settings asset, or null. Setting it registers the asset and applies it.</summary>
        internal static UISettings Active
        {
            get
            {
                EditorBuildSettings.TryGetConfigObject(UISettings.ConfigObjectKey, out UISettings settings);
                return settings;
            }
            set
            {
                if (value != null)
                    EditorBuildSettings.AddConfigObject(UISettings.ConfigObjectKey, value, true);
                else
                    EditorBuildSettings.RemoveConfigObject(UISettings.ConfigObjectKey);
                UISettings.SetActive(value);
            }
        }

        // The first time a project opens with no settings registered, the first settings asset in it is registered.
        [InitializeOnLoadMethod]
        private static void RegisterFound() => EditorApplication.delayCall += () =>
        {
            if (Active != null) return;
            var guids = AssetDatabase.FindAssets("t:" + nameof(UISettings));
            if (guids.Length == 0) return;
            var found = AssetDatabase.LoadAssetAtPath<UISettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (found != null)
                Active = found;
        };

        [SettingsProvider]
        private static SettingsProvider Create()
        {
            SerializedObject serializedObject = null;

            return new SettingsProvider("Project/Timbo Jimbo/UI Next", SettingsScope.Project)
            {
                label = "UI Next",
                keywords = new HashSet<string>(new[] { "Image", "Loader", "CDN", "Cache" }),
                deactivateHandler = () => serializedObject = null,
                guiHandler = _ =>
                {
                    var previousLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 250;

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    EditorGUILayout.BeginVertical();
                    GUILayout.Space(10);

                    var active = Active;
                    var picked = (UISettings)EditorGUILayout.ObjectField("Settings Asset", active, typeof(UISettings), false);
                    if (picked != active)
                        Active = active = picked;

                    if (active == null)
                    {
                        EditorGUILayout.HelpBox("No settings asset. Images load their URLs as they are, keep 64 MB of the images nothing shows in memory, and 256 MB on disk.", MessageType.Info);
                        if (GUILayout.Button("Create", GUILayout.Width(120)))
                        {
                            var created = CreateAsset();
                            if (created != null)
                                Active = created;
                        }
                    }
                    else
                    {
                        if (serializedObject == null || serializedObject.targetObject != active)
                            serializedObject = new SerializedObject(active);
                        serializedObject.Update();
                        EditorGUILayout.Space();
                        var property = serializedObject.GetIterator();
                        for (bool enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                            if (property.propertyPath != "m_Script")
                                EditorGUILayout.PropertyField(property, true);
                        // Applying runs the asset's OnValidate, which tells images.
                        serializedObject.ApplyModifiedProperties();
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();

                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }
            };
        }

        private static UISettings CreateAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create UI Next Settings", "UINextSettings", "asset", "Where to save the settings asset.");
            if (string.IsNullOrEmpty(path))
                return null;
            var asset = ScriptableObject.CreateInstance<UISettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
