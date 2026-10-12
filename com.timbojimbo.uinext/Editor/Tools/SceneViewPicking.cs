using System;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// A click in the Scene view selects the node drawn topmost under it, where it is drawn (moved, scaled, scrolled,
    /// presented), inside its clips, whether it takes the pointer or not; a click again in the same place goes on to the
    /// one under it, as Unity's own picking does. Unity finds nothing of its own to pick on a node (a RectTransform draws
    /// nothing it knows of), so this is a custom picking pass, which Unity asks where it found nothing.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneViewPicking
    {
        static SceneViewPicking() => HandleUtility.pickGameObjectCustomPasses += Pick;

        // `position` is in the camera's pixels, y up from its bottom.
        private static GameObject Pick(Camera camera, int layers, Vector2 position, GameObject[] ignore, GameObject[] filter,
            out int materialIndex)
        {
            materialIndex = -1;
            if (camera == null) return null;
            var ray = camera.ScreenPointToRay(position);
            var trees = UITree.Trees;
            for (int t = trees.Count - 1; t >= 0; t--)
            {
                var tree = trees[t];
                var root = tree.Root;
                if (root == null || !root.isActiveAndEnabled || (layers & (1 << root.gameObject.layer)) == 0) continue;
                // A root is drawn in its transform's plane, its points along x and down its y.
                var transform = root.transform;
                if (!new Plane(transform.forward, transform.position).Raycast(ray, out float distance)) continue;
                var local = transform.InverseTransformPoint(ray.GetPoint(distance));
                int index = tree.PickTopmost(new Vector2(local.x, -local.y), node => Allowed(node.gameObject, ignore, filter));
                if (index >= 0) return tree.Nodes[index].gameObject;
            }
            return null;
        }

        // Not picked already (a click again goes on to the next one under it), and among what may be picked, if that is
        // limited (a prefab being edited).
        private static bool Allowed(GameObject candidate, GameObject[] ignore, GameObject[] filter) =>
            (ignore == null || Array.IndexOf(ignore, candidate) < 0) && (filter == null || Array.IndexOf(filter, candidate) >= 0);
    }
}
