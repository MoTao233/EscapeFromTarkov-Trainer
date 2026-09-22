using System.Linq;
using EFT.CameraControl;
using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using EFT.Trainer.Properties;
using JetBrains.Annotations;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

public class ShootableColor(Color color, Color borderColor) : IFeature
{
	[ConfigurationProperty(Order = 1)]
	public Color Color { get; set; } = color;

	[ConfigurationProperty(Order = 2)]
	public Color BorderColor { get; set; } = borderColor;

	public string Name => nameof(ShootableColor);
}

[UsedImplicitly]
internal class Players : ToggleFeature
{
	public override string Name => Strings.FeaturePlayersName;
	public override string Description => Strings.FeaturePlayersDescription;

	[ConfigurationProperty(Order = 10)]
	public PlayerColor BearColors { get; set; } = new(Color.blue, Color.blue, Color.blue);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor UsecColors { get; set; } = new(Color.green, Color.green, Color.green);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor ScavColors { get; set; } = new(Color.yellow, Color.yellow, Color.yellow);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor BossColors { get; set; } = new(Color.red, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor CultistColors { get; set; } = new(Color.magenta, Color.magenta, Color.magenta);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor ScavRaiderColors { get; set; } = new(new Color(1, 0.5f, 0), new Color(1, 0.5f, 0), new Color(1, 0.5f, 0));

	[ConfigurationProperty(Order = 10)]
	public PlayerColor ScavAssaultColors { get; set; } = new(Color.yellow, Color.yellow, Color.yellow);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor MarksmanColors { get; set; } = new(Color.yellow, Color.yellow, Color.yellow);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor RogueUsecColors { get; set; } = new(Color.gray, Color.gray, Color.gray);

	[ConfigurationProperty(Order = 20)]
	public bool ShowBoxes { get; set; } = true;

	[ConfigurationProperty(Order = 21)]
	public float BoxThickness { get; set; } = 2f;

	[ConfigurationProperty(Order = 30)]
	public bool ShowCharms { get; set; } = true;

	[ConfigurationProperty(Order = 31)]
	public bool XRayVision { get; set; } = false;

	private float _occludedOpacity = 0.55f;
	[ConfigurationProperty(Order = 32)]
	public float OccludedOpacity
	{
		get => _occludedOpacity;
		set => _occludedOpacity = EspGeometry.Finite(value) ? Mathf.Clamp01(value) : 0.55f;
	}

	private float _bodyTint = 0.3f;
	[ConfigurationProperty(Order = 33)]
	public float BodyTint
	{
		get => _bodyTint;
		set => _bodyTint = EspGeometry.Finite(value) ? Mathf.Clamp01(value) : 0.3f;
	}

	private float _outlineScale = 1f;
	[ConfigurationProperty(Order = 34)]
	public float OutlineScale
	{
		get => _outlineScale;
		set => _outlineScale = EspGeometry.Finite(value) ? Mathf.Clamp(value, 0.1f, 5f) : 1f;
	}

	[ConfigurationProperty(Order = 40)]
	public bool ShowInfos { get; set; } = true;

	[ConfigurationProperty(Order = 50)]
	public bool ShowSkeletons { get; set; } = true;

	[ConfigurationProperty(Order = 51)]
	public float SkeletonThickness { get; set; } = 2;

	[ConfigurationProperty(Order = 60)]
	public bool ShowShootable { get; set; } = false;

	[ConfigurationProperty(Order = 61)]
	public ShootableColor ShootableColors { get; set; } = new(Color.green, Color.red);

	[ConfigurationProperty(Order = 62)]
	public bool ShowNotShootable { get; set; } = false;

	[ConfigurationProperty(Order = 63)]
	public ShootableColor NotShootableColors { get; set; } = new(Color.red, Color.blue);

	[ConfigurationProperty(Order = 19)]
	public float MaximumDistance { get; set; } = 0f;

	private readonly PlayerEsp _esp = new();
	private readonly EspProjection _projection = new();

	[ConfigurationProperty(Order = 11)]
	public bool VisibleOnly { get; set; } = true;

	[ConfigurationProperty(Order = 12)]
	public bool ModernEsp { get; set; } = true;

	[ConfigurationProperty(Order = 13)]
	public bool ShowTeammates { get; set; } = false;

	[ConfigurationProperty(Order = 14)]
	public PlayerColor TeammateColors { get; set; } = new(Color.cyan, Color.cyan, Color.cyan);

	private float _boxFillOpacity = 0.08f;
	[ConfigurationProperty(Order = 22)]
	public float BoxFillOpacity
	{
		get => _boxFillOpacity;
		set => _boxFillOpacity = EspGeometry.Finite(value) ? Mathf.Clamp01(value) : 0.08f;
	}

	[ConfigurationProperty(Order = 41)]
	public bool ShowHealthBar { get; set; } = true;

	[ConfigurationProperty(Order = 42)]
	public bool ShowWeapon { get; set; } = true;

	[ConfigurationProperty(Order = 43)]
	public bool ShowDistance { get; set; } = true;

	private int _espTextSize = 14;
	[ConfigurationProperty(Order = 44)]
	public int EspTextSize { get => _espTextSize; set => _espTextSize = Mathf.Clamp(value, 10, 28); }

	[ConfigurationProperty(Order = 52)]
	public bool ShowJoints { get; set; } = true;

	private static Camera? _opticCamera;
	private static (Vector2 center, float radius) _scopeParameters;

	[UsedImplicitly]
	protected void OnGUI()
	{
		var snapshot = GameState.Current;
		if (snapshot == null)
		{
			_esp.Clear();
			return;
		}

		if (snapshot.MapMode)
			return;

		var hostiles = snapshot.Hostiles;

		var player = snapshot.LocalPlayer;
		if (player == null)
		{
			_esp.Clear();
			return;
		}

		var camera = snapshot.Camera;
		if (camera == null)
			return;

		if (!Enabled)
		{
			_esp.Clear();
			return;
		}

		var isAiming = AimingCheck(camera, player);
		if (Event.current.type == EventType.Repaint)
		{
			_projection.Configure(camera, isAiming ? _opticCamera : null, _scopeParameters.center, _scopeParameters.radius);
			_esp.BeginFrame(_projection, player);
		}

		foreach (var ennemy in hostiles)
		{
			if (!ennemy.IsValid())
				continue;

			if (!TryGetDisplayColors(ennemy, player, out var playerColors))
				continue;

			if (Event.current.type != EventType.Repaint)
				continue;

			var previousColor = GUI.color;
			try { _esp.Draw(ennemy, this, playerColors); }
			finally { GUI.color = previousColor; }
		}
	}

	[UsedImplicitly]
	private void OnDestroy() => _esp.Dispose();

	private static bool AimingCheck(Camera camera, Player player)
	{
		_scopeParameters = default;
		var handsController = player.HandsController;
		if (handsController == null)
			return false;

		var weaponAnimation = player.ProceduralWeaponAnimation;
		if (weaponAnimation == null)
			return false;

		var aimingMod = weaponAnimation.CurrentAimingMod;
		if (aimingMod == null)
			return false;

		if (aimingMod.ScopesCount <= 0)
			return false;

		if (!handsController.IsAiming || aimingMod.GetCurrentOpticZoom() <= 1)
			return false;

		if (_opticCamera == null)
			_opticCamera = Camera.allCameras.FirstOrDefault(c => c.name == "BaseOpticCamera(Clone)");
		if (_opticCamera == null)
			return false;

		var currentOptic = weaponAnimation.HandsContainer?.Weapon?.GetComponentInChildren<OpticSight>();
		return currentOptic != null && GetScopeParameters(camera, currentOptic);
	}

	public bool TryGetDisplayColors(Player player, Player? local, out PlayerColor colors)
	{
		var teammate = PlayerTeam.IsTeammate(player, local);
		colors = teammate ? TeammateColors : GetPlayerColors(player.GetHostileType());
		return !teammate || ShowTeammates;
	}

	public PlayerColor GetPlayerColors(HostileType hostileType)
	{
		return hostileType switch
		{
			HostileType.Bear => BearColors,
			HostileType.Usec => UsecColors,
			HostileType.Scav => ScavColors,
			HostileType.Boss => BossColors,
			HostileType.Cultist => CultistColors,
			HostileType.ScavRaider => ScavRaiderColors,
			HostileType.ScavAssault => ScavAssaultColors,
			HostileType.Marksman => MarksmanColors,
			HostileType.RogueUsec => RogueUsecColors,
			_ => ScavColors,
		};
	}

	public static Vector2 ScopePointToScreenPoint(Camera camera, Vector3 worldPoint, bool clamp = false)
	{
		if (_opticCamera == null || !GetCameraOffset(camera, out var scale, out var cameraOffset))
			return camera.WorldPointToScreenPoint(worldPoint);

		var scopePoint = (Vector2)_opticCamera.WorldToScreenPoint(worldPoint) + cameraOffset;
		scopePoint.y = Screen.height - scopePoint.y * scale;
		scopePoint.x *= scale;

		if (clamp)
			return ClampPointToScope(scopePoint);

		var distance = Vector2.Distance(_scopeParameters.center, scopePoint);
		if (distance <= _scopeParameters.radius)
			return scopePoint;

		return Vector2.zero;
	}

	private static bool GetCameraOffset(Camera camera, out float scale, out Vector2 cameraOffset)
	{
		scale = 0f;
		cameraOffset = Vector2.zero;

		if (_opticCamera == null)
			return false;

		scale = Screen.height / (float)camera.scaledPixelHeight;
		cameraOffset = new Vector2(
			camera.pixelWidth / 2 - _opticCamera.pixelWidth / 2,
			camera.pixelHeight / 2 - _opticCamera.pixelHeight / 2);

		return true;
	}
	private static Vector2 ClampPointToScope(Vector2 scopePoint)
	{
		var distance = Vector2.Distance(_scopeParameters.center, scopePoint);

		var clampedPoint = scopePoint;

		if (distance > _scopeParameters.radius)
		{
			var clampedVector = (scopePoint - _scopeParameters.center).normalized * _scopeParameters.radius;
			clampedPoint = _scopeParameters.center + clampedVector;
		}

		return clampedPoint;
	}

	private static bool GetScopeParameters(Camera camera, OpticSight currentOptic)
	{
		if (currentOptic.LensRenderer == null)
			return false;
		var lensMesh = currentOptic.LensRenderer.GetComponent<MeshFilter>()?.sharedMesh;
		if (lensMesh == null)
			return false;
		var opticTransform = currentOptic.LensRenderer.transform;
		var lensUpperRight = opticTransform.TransformPoint(lensMesh.bounds.max);
		var lensUpperLeft = opticTransform.TransformPoint(new Vector3(lensMesh.bounds.min.x, 0, lensMesh.bounds.max.z));

		var lensUpperRight3D = camera.WorldPointToScreenPoint(lensUpperRight);
		var lensUpperLeft3D = camera.WorldPointToScreenPoint(lensUpperLeft);
		_scopeParameters.radius = Vector2.Distance(lensUpperRight3D, lensUpperLeft3D) / 2;
		_scopeParameters.center = camera.WorldPointToScreenPoint(opticTransform.position);
		return EspGeometry.Finite(_scopeParameters.radius) && _scopeParameters.radius > 0;
	}
}
