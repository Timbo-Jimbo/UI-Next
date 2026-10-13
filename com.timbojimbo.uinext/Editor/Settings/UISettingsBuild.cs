using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// Puts the active UI Next settings asset (and the loader it refers to) into a player's preloaded assets for the
    /// duration of a build, so it ships and loads before the first scene. The list is restored afterwards; an aborted build
    /// leaves it in, which is harmless: the next build finds it there and leaves it.
    /// </summary>
    internal sealed class UISettingsBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static Object s_added;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            s_added = null;
            var settings = UISettingsProvider.Active;
            if (settings == null) return;
            var preloaded = new List<Object>(PlayerSettings.GetPreloadedAssets());
            if (preloaded.Contains(settings)) return;
            preloaded.Add(settings);
            s_added = settings;
            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (s_added == null) return;
            var preloaded = new List<Object>(PlayerSettings.GetPreloadedAssets());
            preloaded.Remove(s_added);
            s_added = null;
            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
        }
    }
}
