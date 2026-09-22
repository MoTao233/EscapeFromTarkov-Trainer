using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#nullable enable

namespace EFT.Trainer.Rendering
{
    internal sealed class HighlightRenderer : IDisposable
    {
        // EFT captures the scene at AfterForwardAlpha, then presents private SSAA/DLSS targets.
        public const CameraEvent Event = CameraEvent.BeforeForwardAlpha;
        private static readonly int FillId = Shader.PropertyToID("_TrainerFill");
        private static readonly int EdgeId = Shader.PropertyToID("_TrainerEdge");
        private static readonly int ExpandedId = Shader.PropertyToID("_TrainerExpanded");
        private static readonly int FillColorId = Shader.PropertyToID("_TrainerFillColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_TrainerEdgeColor");
        private static readonly int HiddenColorId = Shader.PropertyToID("_TrainerHiddenColor");
        private static readonly int AlphaTexId = Shader.PropertyToID("_TrainerAlphaTex");
        private static readonly int AlphaSTId = Shader.PropertyToID("_TrainerAlphaST");
        private static readonly int CutoffId = Shader.PropertyToID("_TrainerCutoff");
        private static readonly int RadiusId = Shader.PropertyToID("_TrainerRadius");
        private static readonly int TexelId = Shader.PropertyToID("_TrainerTexel");
        private readonly RenderTargetIdentifier[] _mrt = { new RenderTargetIdentifier(FillId), new RenderTargetIdentifier(EdgeId) };
        private readonly Material _mask, _composite;
        private readonly Plane[] _planes = new Plane[6];
        private readonly Dictionary<Camera, CommandBuffer> _buffers = new Dictionary<Camera, CommandBuffer>();
        private readonly List<HighlightTarget> _selected = new List<HighlightTarget>();
        public int TargetCount { get; private set; }
        public int DrawCount { get; private set; }

        public HighlightRenderer(Shader mask, Shader composite)
        {
            _mask = new Material(mask) { hideFlags = HideFlags.HideAndDontSave };
            _composite = new Material(composite) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Record(Camera camera, IReadOnlyList<HighlightTarget> players, IReadOnlyList<HighlightTarget> items,
            float playerWidth, float itemWidth, float screenRadius, int itemLimit)
        {
            TargetCount = DrawCount = 0;
            if (players.Count == 0 && items.Count == 0)
            {
                Clear(camera);
                return;
            }
            if (!_buffers.TryGetValue(camera, out var buffer))
            {
                buffer = new CommandBuffer { name = "EFT Trainer: outlines and translucent fill" };
                _buffers.Add(camera, buffer);
                camera.AddCommandBuffer(Event, buffer);
            }
            buffer.Clear();
            int width = Mathf.Max(1, camera.scaledPixelWidth), height = Mathf.Max(1, camera.scaledPixelHeight);
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            bool allocated = false;
            float radius = screenRadius * width / Mathf.Max(1, camera.pixelWidth);
            RecordLayer(buffer, camera, items, itemWidth, radius, itemLimit, width, height, ref allocated);
            RecordLayer(buffer, camera, players, playerWidth, radius, int.MaxValue, width, height, ref allocated);
            if (allocated)
            {
                buffer.ReleaseTemporaryRT(ExpandedId);
                buffer.ReleaseTemporaryRT(EdgeId);
                buffer.ReleaseTemporaryRT(FillId);
            }
        }

        private void RecordLayer(CommandBuffer buffer, Camera camera, IReadOnlyList<HighlightTarget> targets,
            float thickness, float radius, int limit, int width, int height, ref bool allocated)
        {
            bool started = false;
            int count = 0;
            Rect area = default;
            _selected.Clear();
            foreach (var target in targets)
            {
                if (count >= limit) break;
                if (!target.TryGetBounds(camera, out var bounds) || !GeometryUtility.TestPlanesAABB(_planes, bounds)) continue;
                if (target.MaximumDistance > 0 && bounds.SqrDistance(camera.transform.position) > target.MaximumDistance * target.MaximumDistance) continue;
                var rect = HighlightGeometry.ProjectBounds(camera, bounds, width, height);
                if (rect.width <= 0 || rect.height <= 0 || !HighlightGeometry.IntersectsRadius(rect, width, height, radius)) continue;
                if (!started)
                {
                    if (!allocated)
                    {
                        // Shared buffers are requested only when there is something to draw.
                        buffer.GetTemporaryRT(FillId, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                        buffer.GetTemporaryRT(EdgeId, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                        buffer.GetTemporaryRT(ExpandedId, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                        buffer.SetGlobalVector(TexelId, new Vector4(1f / width, 1f / height, width, height));
                        allocated = true;
                    }
                    buffer.SetRenderTarget(_mrt, BuiltinRenderTextureType.CameraTarget);
                    // The scene depth belongs to the game: neither clear it nor write to it.
                    buffer.ClearRenderTarget(false, true, Color.clear);
                    area = rect;
                    started = true;
                }
                else area = Rect.MinMaxRect(Mathf.Min(area.xMin, rect.xMin), Mathf.Min(area.yMin, rect.yMin),
                    Mathf.Max(area.xMax, rect.xMax), Mathf.Max(area.yMax, rect.yMax));
                _selected.Add(target);
                count++;
                TargetCount++;
            }
            if (!started) return;
            // Hidden fragments first, so hidden faces cannot cover another object's visible faces.
            foreach (var target in _selected)
                if (target.XRay) DrawTarget(buffer, camera, target, 1);
            foreach (var target in _selected) DrawTarget(buffer, camera, target, 0);
            int pixels = Mathf.Clamp(Mathf.RoundToInt(thickness), 1, 5);
            buffer.SetGlobalInt(RadiusId, pixels);
            // Clear the dilation target, then restrict both screen passes to selected bounds.
            buffer.SetRenderTarget(ExpandedId);
            buffer.ClearRenderTarget(false, true, Color.clear);
            var scissor = Rect.MinMaxRect(Mathf.Max(0, Mathf.Floor(area.xMin) - pixels - 1),
                Mathf.Max(0, Mathf.Floor(area.yMin) - pixels - 1), Mathf.Min(width, Mathf.Ceil(area.xMax) + pixels + 1),
                Mathf.Min(height, Mathf.Ceil(area.yMax) + pixels + 1));
            buffer.EnableScissorRect(scissor);
            buffer.Blit(BuiltinRenderTextureType.None, ExpandedId, _composite, 0);
            buffer.Blit(BuiltinRenderTextureType.None, BuiltinRenderTextureType.CameraTarget, _composite, 1);
            buffer.DisableScissorRect();
            buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        }

        private void DrawTarget(CommandBuffer buffer, Camera camera, HighlightTarget target, int pass)
        {
            buffer.SetGlobalColor(FillColorId, target.Fill);
            buffer.SetGlobalColor(EdgeColorId, target.Edge);
            buffer.SetGlobalColor(HiddenColorId, target.Hidden);
            foreach (var part in target.Parts)
            {
                if (!HighlightTarget.CanDraw(part.Renderer, camera)) continue;
                buffer.SetGlobalTexture(AlphaTexId, part.AlphaTexture != null ? part.AlphaTexture : Texture2D.whiteTexture);
                buffer.SetGlobalVector(AlphaSTId, part.AlphaST);
                buffer.SetGlobalFloat(CutoffId, part.Cutoff);
                buffer.DrawRenderer(part.Renderer, _mask, part.Submesh, pass);
                DrawCount++;
            }
        }

        public void Clear()
        {
            foreach (var buffer in _buffers.Values) buffer.Clear();
            TargetCount = DrawCount = 0;
        }

        public void Clear(Camera camera)
        {
            if (_buffers.TryGetValue(camera, out var buffer)) buffer.Clear();
        }

        public void Detach()
        {
            foreach (var pair in _buffers)
            {
                if (pair.Key != null) pair.Key.RemoveCommandBuffer(Event, pair.Value);
                pair.Value.Release();
            }
            _buffers.Clear();
        }

        public void Dispose()
        {
            Detach();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_mask);
                UnityEngine.Object.Destroy(_composite);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_mask);
                UnityEngine.Object.DestroyImmediate(_composite);
            }
        }
    }
}
