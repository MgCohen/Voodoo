using System.Collections.Generic;
using UnityEngine;

namespace Prototype.Shared
{
    /// Helpers that all three prototypes share. Kept static and dependency-free.
    public static class PrototypeBrushUtil
    {
        public static void SetLayerRecursive(GameObject _Root, int _Layer)
        {
            _Root.layer = _Layer;
            for (int i = 0; i < _Root.transform.childCount; i++)
            {
                SetLayerRecursive(_Root.transform.GetChild(i).gameObject, _Layer);
            }
        }

        // Applies a single tint colour to every Renderer in the brush, mirroring BrushMainMenu.Set().
        // Uses material instances (not MaterialPropertyBlock) so existing brush shaders work without modification.
        public static void ApplyColorToRenderers(List<Renderer> _Renderers, Color _Color)
        {
            for (int i = 0; i < _Renderers.Count; i++)
            {
                if (_Renderers[i] != null)
                {
                    _Renderers[i].material.color = _Color;
                }
            }
        }

        public static List<Renderer> CollectRenderers(GameObject _Root)
        {
            var list = new List<Renderer>();
            _Root.GetComponentsInChildren(true, list);
            return list;
        }

        // Disables physics/gameplay components that the gameplay brush prefab carries
        // but which the prototype scenes don't need (avoids NullRefs from missing services).
        public static void StripGameplayComponents(GameObject _Root)
        {
            var rb = _Root.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // Disable any colliders so taps don't get hijacked by the world objects.
            foreach (var col in _Root.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
        }
    }
}
