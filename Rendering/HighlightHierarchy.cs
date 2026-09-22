using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#nullable enable

namespace EFT.Trainer.Rendering
{
    // Interaction scripts can live on a lid or an invisible collider. Collect the visual
    // assembly too, but reject shared scene parents instead of outlining a whole building.
    internal sealed class HighlightHierarchy
    {
        private const int MaximumNodes = 256;
        private readonly Stack<Transform> _pending = new Stack<Transform>();
        private readonly List<Renderer> _candidates = new List<Renderer>();
        private readonly HashSet<Renderer> _seen = new HashSet<Renderer>();

        public void Collect<T>(T owner, IEnumerable<GameObject>? ownedObjects, bool includeParent,
            float maximumSize, List<Renderer> result) where T : Component
        {
            result.Clear();
            _seen.Clear();
            Append(owner.transform, owner, false, maximumSize, result);
            if (ownedObjects != null)
                foreach (var root in ownedObjects)
                    if (root != null && root != owner.gameObject && root.scene == owner.gameObject.scene)
                        Append(root.transform, owner, true, maximumSize, result);
            var parent = owner.transform.parent;
            if (parent != null && (includeParent || result.Count == 0))
                Append(parent, owner, true, maximumSize, result);
        }

        private void Append<T>(Transform root, T owner, bool checkScope, float maximumSize,
            List<Renderer> result) where T : Component
        {
            _pending.Clear();
            _candidates.Clear();
            _pending.Push(root);
            int nodes = 0;
            bool hasBounds = false;
            Bounds bounds = default;
            while (_pending.Count > 0)
            {
                if (++nodes > MaximumNodes) return;
                var node = _pending.Pop();
                var other = node.GetComponent<T>();
                if (other != null && other != owner)
                {
                    if (checkScope) return; // Another container/door/item owns this parent too.
                    continue;
                }
                var renderer = node.GetComponent<Renderer>();
                if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) &&
                    renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                {
                    _candidates.Add(renderer);
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    {
                        if (!hasBounds) bounds = renderer.bounds;
                        else bounds.Encapsulate(renderer.bounds);
                        hasBounds = true;
                    }
                }
                for (int i = 0; i < node.childCount; i++) _pending.Push(node.GetChild(i));
            }
            if (checkScope && hasBounds && (Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) > maximumSize ||
                bounds.SqrDistance(owner.transform.position) > maximumSize * maximumSize)) return;
            foreach (var renderer in _candidates)
                if (_seen.Add(renderer)) result.Add(renderer);
        }
    }
}
