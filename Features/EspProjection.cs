using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

internal sealed class EspProjection
{
	public Camera Camera { get; private set; } = null!;
	public bool Circular { get; private set; }
	public Vector2 Center { get; private set; }
	public float Radius { get; private set; }
	public Rect Viewport { get; private set; }
	private float _scale;
	private Vector2 _offset;

	public void Configure(Camera mainCamera, Camera? opticCamera, Vector2 center, float radius)
	{
		Circular = opticCamera != null && radius > 0;
		Camera = Circular ? opticCamera! : mainCamera;
		Center = center;
		Radius = radius;
		Viewport = new Rect(0, 0, Screen.width, Screen.height);
		_scale = Screen.height / (float)Mathf.Max(1, mainCamera.scaledPixelHeight);
		_offset = Circular ? new Vector2(mainCamera.pixelWidth / 2f - Camera.pixelWidth / 2f,
			mainCamera.pixelHeight / 2f - Camera.pixelHeight / 2f) : Vector2.zero;
	}

	public bool TryProject(Vector3 world, out Vector2 screen)
	{
		var point = Camera.WorldToScreenPoint(world);
		screen = new Vector2((point.x + _offset.x) * _scale, Screen.height - (point.y + _offset.y) * _scale);
		return point.z > 0.01f && EspGeometry.Finite(screen.x) && EspGeometry.Finite(screen.y);
	}

	public bool Contains(Vector2 point) => Viewport.Contains(point) &&
		(!Circular || (point - Center).sqrMagnitude <= Radius * Radius);

	public bool Contains(Rect rect) => Contains(new Vector2(rect.xMin, rect.yMin)) &&
		Contains(new Vector2(rect.xMax, rect.yMin)) && Contains(new Vector2(rect.xMin, rect.yMax)) &&
		Contains(new Vector2(rect.xMax, rect.yMax));

	public bool Clip(ref Vector2 from, ref Vector2 to) => EspGeometry.ClipLine(ref from, ref to, Viewport, Circular, Center, Radius);
}
