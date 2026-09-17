using EFT.Trainer.Features;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.UI;

// Resources belong to Players and are released with that component.
internal sealed class EspDrawing
{
	private Texture2D? _dot;
	private GUIStyle? _text;
	private readonly GUIContent _content = new();

	public static void Fill(Rect rect, Color color)
	{
		if (rect.width <= 0 || rect.height <= 0 || color.a <= 0) return;
		GUI.color = color;
		GUI.DrawTexture(rect, Texture2D.whiteTexture);
	}

	public static void Line(EspProjection view, Vector2 from, Vector2 to, float width, Color color)
	{
		if (!view.Clip(ref from, ref to)) return;
		var matrix = GUI.matrix;
		var delta = to - from;
		GUI.color = color;
		GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
		GUI.DrawTexture(new Rect(from.x, from.y - width / 2, delta.magnitude, width), Texture2D.whiteTexture);
		GUI.matrix = matrix;
	}

	public static void Box(EspProjection view, Rect box, float width, Color color, bool corners)
	{
		if (!corners)
		{
			Line(view, new Vector2(box.xMin, box.yMin), new Vector2(box.xMax, box.yMin), width, color);
			Line(view, new Vector2(box.xMax, box.yMin), new Vector2(box.xMax, box.yMax), width, color);
			Line(view, new Vector2(box.xMax, box.yMax), new Vector2(box.xMin, box.yMax), width, color);
			Line(view, new Vector2(box.xMin, box.yMax), new Vector2(box.xMin, box.yMin), width, color);
			return;
		}
		float x = Mathf.Min(24, box.width * 0.22f), y = Mathf.Min(24, box.height * 0.12f);
		for (int i = 0; i < 4; i++)
		{
			bool right = (i & 1) != 0, bottom = (i & 2) != 0;
			var corner = new Vector2(right ? box.xMax : box.xMin, bottom ? box.yMax : box.yMin);
			Line(view, corner, corner + new Vector2(right ? -x : x, 0), width, color);
			Line(view, corner, corner + new Vector2(0, bottom ? -y : y), width, color);
		}
	}

	public void Dot(EspProjection view, Vector2 center, float radius, Color color)
	{
		var rect = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
		if (!view.Contains(rect)) return;
		if (_dot == null)
		{
			_dot = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			var pixels = new Color[32 * 32];
			for (int y = 0; y < 32; y++)
				for (int x = 0; x < 32; x++)
					pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Clamp01(15.5f - new Vector2(x - 15.5f, y - 15.5f).magnitude));
			_dot.SetPixels(pixels);
			_dot.Apply(false, true);
		}
		GUI.color = color;
		GUI.DrawTexture(rect, _dot);
	}

	public static void Head(EspProjection view, Vector2 center, float radius, float width, Color color)
	{
		int segments = radius < 8 ? 12 : 24;
		var previous = center + new Vector2(radius, 0);
		for (int i = 1; i <= segments; i++)
		{
			float angle = i * (2 * Mathf.PI / segments);
			var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
			Line(view, previous, next, width, color);
			previous = next;
		}
	}

	public void Label(EspProjection view, Vector2 anchor, string text, Color color, int size, bool above)
	{
		if (string.IsNullOrEmpty(text)) return;
		_text ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = false, padding = new RectOffset(0, 0, 0, 0) };
		_text.fontSize = size;
		_text.normal.textColor = Color.white;
		_content.text = text;
		var dimensions = _text.CalcSize(_content);
		var rect = new Rect(anchor.x - dimensions.x / 2, above ? anchor.y - dimensions.y : anchor.y, dimensions.x, dimensions.y);
		if (!view.Contains(rect)) return;
		GUI.color = new Color(0, 0, 0, 0.9f);
		GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), _content, _text);
		GUI.color = color;
		GUI.Label(rect, _content, _text);
	}

	public void Dispose()
	{
		if (_dot != null) Object.Destroy(_dot);
		_dot = null;
		_text = null;
	}
}
