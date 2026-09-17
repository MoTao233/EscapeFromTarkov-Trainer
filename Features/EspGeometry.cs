using System;
using UnityEngine;

namespace EFT.Trainer.Features;

// Screen-space math only: independent of players, physics and GUI state.
internal static class EspGeometry
{
	public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

	public static bool ClipLine(ref Vector2 from, ref Vector2 to, Rect viewport, bool circular, Vector2 center, float radius)
	{
		if (!Finite(from.x) || !Finite(from.y) || !Finite(to.x) || !Finite(to.y)) return false;
		var delta = to - from;
		float start = 0, end = 1;
		if (!Clip(-delta.x, from.x - viewport.xMin, ref start, ref end) ||
			!Clip(delta.x, viewport.xMax - from.x, ref start, ref end) ||
			!Clip(-delta.y, from.y - viewport.yMin, ref start, ref end) ||
			!Clip(delta.y, viewport.yMax - from.y, ref start, ref end)) return false;
		if (circular)
		{
			var offset = from - center;
			float a = delta.sqrMagnitude, b = 2 * Vector2.Dot(offset, delta), c = offset.sqrMagnitude - radius * radius;
			if (radius <= 0 || (a < 0.0001f && c > 0)) return false;
			if (a >= 0.0001f)
			{
				float discriminant = b * b - 4 * a * c;
				if (discriminant < 0) return false;
				float root = (float)Math.Sqrt(discriminant);
				start = Math.Max(start, (-b - root) / (2 * a));
				end = Math.Min(end, (-b + root) / (2 * a));
			}
		}
		if (start > end) return false;
		to = from + delta * end;
		from += delta * start;
		return (to - from).sqrMagnitude > 0.01f;
	}

	private static bool Clip(float p, float q, ref float start, ref float end)
	{
		if (Math.Abs(p) < 0.00001f) return q >= 0;
		float t = q / p;
		if (p < 0) start = Math.Max(start, t);
		else end = Math.Min(end, t);
		return start <= end;
	}

	public static float HealthFraction(float current, float maximum)
	{
		if (!Finite(current) || !Finite(maximum) || maximum <= 0) return 0;
		return Math.Max(0, Math.Min(1, current / maximum));
	}
}
