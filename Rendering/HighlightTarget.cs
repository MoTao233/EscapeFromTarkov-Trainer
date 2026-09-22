using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Rendering
{
    // This cache describes the original renderers; no material or renderer state is changed.
    internal sealed class HighlightTarget
    {
        internal sealed class Part
        {
            public Renderer Renderer = null!;
            public int Submesh;
            public Texture AlphaTexture = null!;
            public Vector4 AlphaST;
            public float Cutoff;
        }

        public readonly List<Part> Parts = new List<Part>();
        public Color Fill, Edge, Hidden;
        public bool XRay;
        public float MaximumDistance;

        public void Refresh(IEnumerable<Renderer> renderers)
        {
            Parts.Clear();
            foreach (var renderer in renderers)
            {
                if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < mesh.subMeshCount && i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;
                    var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    var scale = texture != null ? material.GetTextureScale("_MainTex") : Vector2.one;
                    var offset = texture != null ? material.GetTextureOffset("_MainTex") : Vector2.zero;
                    Parts.Add(new Part
                    {
                        Renderer = renderer, Submesh = i,
                        AlphaTexture = texture != null ? texture : Texture2D.whiteTexture,
                        AlphaST = new Vector4(scale.x, scale.y, offset.x, offset.y),
                        Cutoff = material.HasProperty("_Cutoff") && (material.IsKeywordEnabled("_ALPHATEST_ON") ||
                            material.GetTag("RenderType", false) == "TransparentCutout") ? material.GetFloat("_Cutoff") : 0
                    });
                }
            }
        }

        public static bool CanDraw(Renderer renderer, Camera camera)
        {
            return renderer != null && renderer.enabled && !renderer.forceRenderingOff &&
                renderer.gameObject.activeInHierarchy && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0;
        }

        public bool TryGetBounds(Camera camera, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var part in Parts)
            {
                if (!CanDraw(part.Renderer, camera)) continue;
                var next = part.Renderer.bounds;
                if (!found) bounds = next;
                else bounds.Encapsulate(next);
                found = true;
            }
            return found;
        }
    }
}
