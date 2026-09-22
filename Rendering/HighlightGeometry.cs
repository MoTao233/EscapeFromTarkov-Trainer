using UnityEngine;

namespace EFT.Trainer.Rendering
{
    internal static class HighlightGeometry
    {
        // Conservative projected bounds: crossing the near plane must not make a target pop out.
        public static Rect ProjectBounds(Camera camera, Bounds bounds, int width, int height)
        {
            var min = bounds.min;
            var max = bounds.max;
            float left = 1, bottom = 1, right = 0, top = 0;
            for (int corner = 0; corner < 8; corner++)
            {
                var p = camera.WorldToViewportPoint(new Vector3((corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z));
                if (p.z <= camera.nearClipPlane) return new Rect(0, 0, width, height);
                left = Mathf.Min(left, p.x); right = Mathf.Max(right, p.x);
                bottom = Mathf.Min(bottom, p.y); top = Mathf.Max(top, p.y);
            }
            return Rect.MinMaxRect(Mathf.Clamp01(left) * width, Mathf.Clamp01(bottom) * height,
                Mathf.Clamp01(right) * width, Mathf.Clamp01(top) * height);
        }

        public static bool IntersectsRadius(Rect rect, int width, int height, float radius)
        {
            if (radius <= 0) return true;
            float x = width * 0.5f, y = height * 0.5f;
            float dx = x - Mathf.Clamp(x, rect.xMin, rect.xMax);
            float dy = y - Mathf.Clamp(y, rect.yMin, rect.yMax);
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
