using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

// Frame-local samples shared by boxes, shootable colors and skeletons.
// Does not change CameraExtensions.IsTransformVisible used by other features.
internal sealed class EspVisibility
{
	// Same world/terrain/hit/interactive layers as original Shootable (EFT 0.16.1).
	private const int LayerMask = 0b0010_00100_0101_0001_1000_0000_0000;
	private readonly RaycastHit[] _hits = new RaycastHit[32];
	private readonly Dictionary<Transform, bool> _points = new(256);
	private Vector3 _origin;
	private Transform? _local;
	public int Raycasts { get; private set; }

	public void BeginFrame(Camera camera, Player local)
	{
		_points.Clear();
		_origin = camera.transform.position;
		_local = local.Transform.Original;
		Raycasts = 0;
	}

	public bool IsVisible(Player target, Transform bone)
	{
		if (bone == null) return false;
		if (_points.TryGetValue(bone, out bool value)) return value;
		value = IsVisible(target, bone.position);
		_points[bone] = value;
		return value;
	}

	public bool IsVisible(Player target, Vector3 destination)
	{
		var direction = destination - _origin;
		float distance = direction.magnitude;
		if (!EspGeometry.Finite(distance) || distance < 0.001f) return false;
		Raycasts++;
		// Inspect every hit: a target torso can precede a wall hiding its arm.
		int count = Physics.RaycastNonAlloc(_origin, direction / distance, _hits, distance, LayerMask, QueryTriggerInteraction.Ignore);
		bool visible = count < _hits.Length; // Saturation is uncertain: hide conservatively.
		var targetRoot = target.Transform.Original;
		for (int i = 0; i < count; i++)
		{
			var hit = _hits[i].transform;
			if (hit != null && !hit.IsChildOf(targetRoot) && (_local == null || !hit.IsChildOf(_local))) visible = false;
			_hits[i] = default;
		}
		return visible;
	}

	public void Clear()
	{
		_points.Clear();
		_local = null;
	}
}
