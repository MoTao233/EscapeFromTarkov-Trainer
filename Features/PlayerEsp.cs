using System.Collections.Generic;
using EFT.InventoryLogic;
using EFT.Trainer.Properties;
using EFT.Trainer.UI;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

// Coordinates the 2D overlay only. Chams and other features do not use these caches.
internal sealed class PlayerEsp
{
	private static readonly string[] Joints =
	[
		Bones.Head, Bones.Neck, Bones.Spine3, Bones.Pelvis,
		Bones.LCollarbone, Bones.LForearm1, Bones.LPalm,
		Bones.RCollarbone, Bones.RForearm1, Bones.RPalm,
		Bones.LThigh1, Bones.LCalf, Bones.LFoot,
		Bones.RThigh1, Bones.RCalf, Bones.RFoot
	];
	private static readonly List<string[]> SimpleConnections =
	[
		[Bones.Neck, Bones.Spine3], [Bones.Spine3, Bones.Pelvis],
		[Bones.Spine3, Bones.LCollarbone], [Bones.LCollarbone, Bones.LForearm1], [Bones.LForearm1, Bones.LPalm],
		[Bones.Spine3, Bones.RCollarbone], [Bones.RCollarbone, Bones.RForearm1], [Bones.RForearm1, Bones.RPalm],
		[Bones.Pelvis, Bones.LThigh1], [Bones.LThigh1, Bones.LCalf], [Bones.LCalf, Bones.LFoot],
		[Bones.Pelvis, Bones.RThigh1], [Bones.RThigh1, Bones.RCalf], [Bones.RCalf, Bones.RFoot]
	];
	private readonly EspVisibility _visibility = new();
	private readonly EspDrawing _drawing = new();
	private readonly Dictionary<Transform, (bool valid, Vector2 screen)> _projected = new(256);
	private readonly Dictionary<int, string> _distances = new();
	private readonly Dictionary<string, string> _weapons = new();
	private EspProjection _view = null!;
	private int _frame = -1;
	private Camera? _camera;
	private Player? _local;

	public void BeginFrame(EspProjection view, Player local)
	{
		_view = view;
		if (_frame == Time.frameCount && _camera == view.Camera && _local == local) return;
		_frame = Time.frameCount;
		_camera = view.Camera;
		_local = local;
		_projected.Clear();
		_visibility.BeginFrame(view.Camera, local);
	}

	private bool Point(Dictionary<string, Transform> bones, string name, out Transform bone, out Vector2 screen)
	{
		screen = default;
		if (!bones.TryGetValue(name, out bone) || bone == null) return false;
		if (!_projected.TryGetValue(bone, out var point))
		{
			bool valid = _view.TryProject(bone.position, out screen);
			point = (valid, screen);
			_projected[bone] = point;
		}
		screen = point.screen;
		return point.valid;
	}

	private bool Bounds(Dictionary<string, Transform> bones, out Rect box)
	{
		float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
		int points = 0;
		foreach (var name in Joints)
		{
			if (!Point(bones, name, out _, out var screen)) continue;
			left = Mathf.Min(left, screen.x); right = Mathf.Max(right, screen.x);
			top = Mathf.Min(top, screen.y); bottom = Mathf.Max(bottom, screen.y);
			points++;
		}
		box = default;
		if (points < 2) return false;
		float padding = Mathf.Clamp((bottom - top) * 0.035f, 2, 12);
		if (Point(bones, Bones.Head, out _, out var head) && Point(bones, Bones.Neck, out _, out var neck))
		{
			float radius = Vector2.Distance(head, neck);
			left = Mathf.Min(left, head.x - radius); right = Mathf.Max(right, head.x + radius);
			top = Mathf.Min(top, head.y - radius);
		}
		box = Rect.MinMaxRect(left - padding, top - padding, right + padding, bottom + padding);
		if (!_view.Viewport.Overlaps(box) || box.width < 1 || box.height < 1) return false;
		if (_view.Circular)
		{
			var closest = new Vector2(Mathf.Clamp(_view.Center.x, box.xMin, box.xMax), Mathf.Clamp(_view.Center.y, box.yMin, box.yMax));
			if ((closest - _view.Center).sqrMagnitude > _view.Radius * _view.Radius) return false;
		}
		return true;
	}

	private bool AnyVisible(Player target, Dictionary<string, Transform> bones)
	{
		foreach (var name in Joints)
			if (Point(bones, name, out var bone, out var point) && _view.Contains(point) && _visibility.IsVisible(target, bone)) return true;
		return false;
	}

	public void Draw(Player target, Players settings, PlayerColor colors)
	{
		if (!settings.ShowBoxes && !settings.ShowSkeletons && !settings.ShowInfos) return;
		float distance = Vector3.Distance(_view.Camera.transform.position, target.Transform.position);
		if (settings.MaximumDistance > 0 && distance > settings.MaximumDistance) return;
		var bones = target.PlayerBody?.SkeletonRootJoint?.Bones;
		if (bones == null || !Bounds(bones, out var box)) return;
		bool needsVisibility = settings.VisibleOnly || settings.ShowShootable;
		bool visible = !needsVisibility || AnyVisible(target, bones);
		if (settings.VisibleOnly && !visible) return;

		var skeletonColor = settings.ModernEsp && settings.UnifiedEspColor ? settings.EspColor : colors.Color;
		var borderColor = settings.ModernEsp && settings.UnifiedEspColor ? settings.EspColor : colors.BorderColor;
		borderColor = StateColor(settings, visible, borderColor, true);
		float boxWidth = Width(settings.BoxThickness);
		if (settings.ShowBoxes)
		{
			if (settings.ModernEsp && settings.BoxFillOpacity > 0)
			{
				var fill = borderColor;
				fill.a = Mathf.Clamp01(settings.BoxFillOpacity);
				// Do not fill a square over the outside of a circular optic.
				if (!_view.Circular || _view.Contains(box))
					EspDrawing.Fill(Rect.MinMaxRect(Mathf.Max(0, box.xMin), Mathf.Max(0, box.yMin),
						Mathf.Min(Screen.width, box.xMax), Mathf.Min(Screen.height, box.yMax)), fill);
			}
			EspDrawing.Box(_view, box, boxWidth, borderColor, settings.ModernEsp);
		}
		if (settings.ShowSkeletons)
		{
			DrawConnections(target, bones, settings.ModernEsp ? SimpleConnections : Bones.Connections, settings, skeletonColor);
			if (!settings.ModernEsp && distance < 75)
				DrawConnections(target, bones, Bones.FingerConnections, settings, skeletonColor);
			DrawHead(target, bones, settings, skeletonColor);
			if (settings.ModernEsp && settings.ShowJoints)
				foreach (var name in Joints)
				{
					if (name == Bones.Head || !Point(bones, name, out var bone, out var point)) continue;
					bool jointVisible = !needsVisibility || _visibility.IsVisible(target, bone);
					if (!settings.VisibleOnly || jointVisible)
						_drawing.Dot(_view, point, Width(settings.SkeletonThickness) + 1, StateColor(settings, jointVisible, skeletonColor, false));
				}
		}
		if (settings.ShowInfos) DrawInfo(target, settings, colors, box, distance);
	}

	private void DrawConnections(Player target, Dictionary<string, Transform> bones, List<string[]> connections, Players settings, Color color)
	{
		bool check = settings.VisibleOnly || settings.ShowShootable;
		foreach (var connection in connections)
		{
			if (!Point(bones, connection[0], out var from, out var a) || !Point(bones, connection[1], out var to, out var b)) continue;
			var clippedA = a; var clippedB = b;
			if (!_view.Clip(ref clippedA, ref clippedB)) continue;
			bool visible = !check || (_visibility.IsVisible(target, from) && _visibility.IsVisible(target, to) &&
				_visibility.IsVisible(target, (from.position + to.position) * 0.5f));
			if (settings.VisibleOnly && !visible) continue;
			EspDrawing.Line(_view, a, b, Width(settings.SkeletonThickness), StateColor(settings, visible, color, false));
		}
	}

	private void DrawHead(Player target, Dictionary<string, Transform> bones, Players settings, Color color)
	{
		if (!Point(bones, Bones.Head, out var headBone, out var head) || !Point(bones, Bones.Neck, out var neckBone, out var neck)) return;
		bool visible = !(settings.VisibleOnly || settings.ShowShootable) ||
			(_visibility.IsVisible(target, headBone) && _visibility.IsVisible(target, neckBone));
		if (settings.VisibleOnly && !visible) return;
		float radius = Vector2.Distance(head, neck);
		if (radius < 1 || radius > Screen.height) return;
		EspDrawing.Head(_view, head, radius, Width(settings.SkeletonThickness), StateColor(settings, visible, color, false));
	}

	private void DrawInfo(Player target, Players settings, PlayerColor colors, Rect box, float distance)
	{
		var health = target.HealthController;
		if (health is not { IsAlive: true }) return;
		var bodyHealth = health.GetBodyPartHealth(EBodyPart.Common);
		float fraction = EspGeometry.HealthFraction(bodyHealth.Current, bodyHealth.Maximum);
		int meters = Mathf.Clamp(Mathf.RoundToInt(distance), 0, 10000);
		if (!_distances.TryGetValue(meters, out var distanceText))
			_distances[meters] = distanceText = string.Format(Strings.FeaturePointOfInterestsDistanceFormat, meters);
		string weaponText = string.Empty;
		if (target.HandsController?.Item is Weapon weapon)
		{
			var key = weapon.ShortName;
			if (!_weapons.TryGetValue(key, out weaponText)) _weapons[key] = weaponText = key.Localized();
		}
		if (!settings.ModernEsp)
		{
			_drawing.Label(_view, new Vector2(box.center.x, box.yMin - 4),
				string.Format(Strings.FeaturePlayersFormat, weaponText, Mathf.Round(fraction * 100), distanceText).Trim(), colors.InfoColor, settings.EspTextSize, true);
			return;
		}
		if (settings.ShowHealthBar)
		{
			var bar = new Rect(box.xMin - 8, box.yMin, 3, box.height);
			if (_view.Contains(bar))
			{
				EspDrawing.Fill(bar, new Color(0, 0, 0, 0.65f));
				var fill = new Rect(bar.x, bar.yMax - bar.height * fraction, bar.width, bar.height * fraction);
				EspDrawing.Fill(fill, Color.Lerp(new Color(1, 0.3f, 0.25f), new Color(0.6f, 0.9f, 0.5f), fraction));
			}
		}
		if (settings.ShowWeapon) _drawing.Label(_view, new Vector2(box.center.x, box.yMin - 4), weaponText, Color.white, settings.EspTextSize, true);
		if (settings.ShowDistance) _drawing.Label(_view, new Vector2(box.center.x, box.yMax + 4), distanceText, Color.white, settings.EspTextSize, false);
	}

	private static Color StateColor(Players settings, bool visible, Color fallback, bool border)
	{
		if (!settings.ShowShootable) return fallback;
		var colors = visible ? settings.ShootableColors : settings.ShowNotShootable ? settings.NotShootableColors : null;
		return colors == null ? fallback : border ? colors.BorderColor : colors.Color;
	}

	private static float Width(float value) => EspGeometry.Finite(value) ? Mathf.Clamp(value, 1, 6) : 2;

	public void Clear()
	{
		_projected.Clear(); _visibility.Clear(); _distances.Clear(); _weapons.Clear();
		_local = null; _camera = null; _frame = -1;
	}

	public void Dispose() { Clear(); _drawing.Dispose(); }
}
