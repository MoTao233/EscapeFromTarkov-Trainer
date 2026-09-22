using System;
using System.Collections.Generic;
using System.IO;
using Comfort.Common;
using EFT.Interactive;
using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using EFT.Trainer.Properties;
using EFT.Trainer.Rendering;
using JetBrains.Annotations;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

[UsedImplicitly]
internal sealed class Highlights : ToggleFeature
{
	public override string Name => Strings.ResourceManager.GetString("FeatureHighlightsName") ?? "Highlights";
	public override string Description => Strings.ResourceManager.GetString("FeatureHighlightsDescription") ?? "Character and loot outlines with translucent fill.";

	[ConfigurationProperty(Order = 10)] public bool HighlightItems { get; set; } = true;
	[ConfigurationProperty(Order = 11)] public bool HighlightContainers { get; set; } = true;
	[ConfigurationProperty(Order = 12)] public Color ItemOutlineColor { get; set; } = new(0.396f, 0.902f, 0.863f, 1);
	[ConfigurationProperty(Order = 13)] public Color ContainerOutlineColor { get; set; } = new(0.4f, 0.8f, 1, 1);
	[ConfigurationProperty(Order = 14)] public Color QuestOutlineColor { get; set; } = new(1, 0.75f, 0.2f, 1);
	private float _itemDistance = 10, _itemFillOpacity = 0.12f, _itemOutlineWidth = 2, _screenRadius;
	private int _maximumItemHighlights = 64;
	[ConfigurationProperty(Order = 15)] public float ItemHighlightDistance { get => _itemDistance; set => _itemDistance = Clamp(value, 1, 100, 10); }
	[ConfigurationProperty(Order = 16)] public float ItemFillOpacity { get => _itemFillOpacity; set => _itemFillOpacity = Clamp(value, 0, 1, 0.12f); }
	[ConfigurationProperty(Order = 17)] public float ItemOutlineWidth { get => _itemOutlineWidth; set => _itemOutlineWidth = Clamp(value, 1, 5, 2); }
	[ConfigurationProperty(Order = 20)] public float HighlightScreenRadius { get => _screenRadius; set => _screenRadius = Clamp(value, 0, 10000, 0); }
	[ConfigurationProperty(Order = 21)] public int MaximumItemHighlights { get => _maximumItemHighlights; set => _maximumItemHighlights = Mathf.Clamp(value, 1, 256); }

	private sealed class PlayerEntry
	{
		public readonly HighlightTarget Target = new();
		public float NextRefresh;
	}
	private sealed class ItemEntry(Component owner, bool container)
	{
		public readonly Component Owner = owner;
		public readonly bool Container = container;
		public readonly HighlightTarget Target = new();
		public float Distance;
	}
	private readonly Dictionary<Player, PlayerEntry> _playerCache = [];
	private readonly Dictionary<Component, ItemEntry> _itemCache = [];
	private readonly List<Player> _deadPlayers = [];
	private readonly List<Component> _removedItems = [];
	private readonly HashSet<Component> _seenItems = [];
	private readonly List<Renderer> _renderers = [];
	private readonly List<ItemEntry> _nearbyItems = [];
	private readonly List<HighlightTarget> _playerTargets = [], _itemTargets = [];
	private readonly List<LootableContainer> _containers = [];
	private GameWorld? _world;
	private Players? _players;
	private HighlightRenderer? _renderer;
	private AssetBundle? _bundle;
	private bool _loadAttempted, _failed;
	private float _nextItems, _nextContainers;
	private int _preparedFrame = -1;
	private Camera? _mainCamera;
	private float _nextStatus, _nextCameraStatus;
	private int _cameraCallbacks;
	private string _lastCamera = "none";
	private string? _logPath;
	private bool _logUnavailable;

	private static float Clamp(float value, float min, float max, float fallback) => EspGeometry.Finite(value) ? Mathf.Clamp(value, min, max) : fallback;
	private static Color Alpha(Color color, float opacity) => new(color.r, color.g, color.b, color.a * opacity);

	[UsedImplicitly]
	private void OnEnable()
	{
		Camera.onPreRender += RenderCamera;
		Log($"Enabled; build={GetType().Assembly.ManifestModule.ModuleVersionId}; data={Application.dataPath}; graphics={SystemInfo.graphicsDeviceType}; device={SystemInfo.graphicsDeviceName}");
	}

	[UsedImplicitly]
	private void LateUpdate()
	{
		try { PrepareFrame(); }
		catch (Exception error)
		{
			_renderer?.Detach();
			_preparedFrame = -1;
			if (!_failed) Log("Target preparation failed: " + error);
			_failed = true;
		}
		if (Time.unscaledTime < _nextStatus) return;
		_nextStatus = Time.unscaledTime + 5;
		Log($"State: enabled={Enabled}; failed={_failed}; world={_world?.GetType().Name ?? "none"}; alive={_world?.MainPlayer.IsAlive()}; map={GameState.Current?.MapMode}; camera={_mainCamera?.name ?? "none"}; playersEnabled={_players?.Enabled}; charms={_players?.ShowCharms}; players={_playerTargets.Count}; items={_itemTargets.Count}; callbacks={_cameraCallbacks}; lastCamera={_lastCamera}; prepared={_preparedFrame}");
	}

	private void PrepareFrame()
	{
		var world = Singleton<GameWorld>.Instance;
		if (_world != world)
		{
			ClearWorld();
			_world = world;
		}
		if (!Enabled || _failed || world == null || world is HideoutGameWorld || !world.MainPlayer.IsAlive() || GameState.Current?.MapMode == true)
		{
			_renderer?.Detach();
			_playerTargets.Clear();
			_itemTargets.Clear();
			_preparedFrame = -1;
			return;
		}
		// The ESP snapshot refreshes every two seconds and can retain the previous camera.
		_mainCamera = Camera.main;
		_players ??= GetComponent<Players>();
		if (!LoadRenderer()) return;
		PreparePlayers(world);
		PrepareItems(world);
		if (_playerTargets.Count == 0 && _itemTargets.Count == 0) _renderer?.Detach();
		_preparedFrame = Time.frameCount;
	}

	private bool LoadRenderer()
	{
		if (_renderer != null) return true;
		if (_loadAttempted) return false;
		_loadAttempted = true;
		try
		{
			var path = Path.Combine(Application.dataPath, "trainer-highlights");
			if (!File.Exists(path)) throw new FileNotFoundException("Deploy trainer-highlights beside the game's Managed directory.", path);
			_bundle = AssetBundle.LoadFromFile(path);
			if (_bundle == null) throw new InvalidOperationException("Unity could not load the highlight asset bundle: " + path);
			var mask = _bundle.LoadAsset<Shader>("assets/trainer/highlightmask.shader");
			var composite = _bundle.LoadAsset<Shader>("assets/trainer/highlightcomposite.shader");
			if (mask == null || composite == null || !mask.isSupported || !composite.isSupported || SystemInfo.supportedRenderTargetCount < 2)
				throw new InvalidOperationException("The highlight shaders are missing or unsupported by this graphics device.");
			_renderer = new HighlightRenderer(mask, composite);
			Log("Shaders loaded; event=" + HighlightRenderer.Event + "; native scene depth; outline + translucent fill.");
		}
		catch (Exception error)
		{
			Debug.LogError("[EFT Trainer] Highlights unavailable: " + error);
			Log("Highlights unavailable: " + error);
		}
		return _renderer != null;
	}

	private void PreparePlayers(GameWorld world)
	{
		_playerTargets.Clear();
		_deadPlayers.Clear();
		foreach (var pair in _playerCache)
			if (!pair.Key.IsAlive()) _deadPlayers.Add(pair.Key);
		foreach (var player in _deadPlayers) _playerCache.Remove(player);
		if (_players == null || !_players.Enabled || !_players.ShowCharms || world.RegisteredPlayers == null) return;
		foreach (var candidate in world.RegisteredPlayers)
		{
			if (candidate is not Player player || player == world.MainPlayer || !player.IsAlive() || player.PlayerBody == null) continue;
			if (!_playerCache.TryGetValue(player, out var entry))
			{
				entry = new PlayerEntry();
				_playerCache.Add(player, entry);
			}
			if (Time.unscaledTime >= entry.NextRefresh)
			{
				// Include body, clothing and worn equipment; exclude first-person hands and weapon effects.
				_renderers.Clear();
				player.PlayerBody.GetComponentsInChildren(true, _renderers);
				entry.Target.Refresh(_renderers);
				entry.NextRefresh = Time.unscaledTime + 2;
			}
			var colors = _players.GetPlayerColors(player);
			var target = entry.Target;
			target.Fill = Alpha(colors.Color, _players.BodyTint);
			target.Edge = colors.BorderColor;
			target.Hidden = Alpha(colors.OccludedColor, _players.OccludedOpacity);
			target.XRay = _players.XRayVision;
			target.MaximumDistance = _players.MaximumDistance;
			_playerTargets.Add(target);
		}
	}

	private void PrepareItems(GameWorld world)
	{
		_itemTargets.Clear();
		if (!HighlightItems && !HighlightContainers) return;
		if (Time.unscaledTime >= _nextItems)
		{
			_seenItems.Clear();
			_nearbyItems.Clear();
			var position = world.MainPlayer.Transform.position;
			if (HighlightItems)
			{
				var loot = world.LootItems;
				for (int i = 0; i < loot.Count; i++)
				{
					var item = loot.GetByIndex(i);
					if (item is Corpse || !item.IsValid() || !item.isActiveAndEnabled) continue;
					ConsiderItem(item, false, position);
				}
			}
			if (HighlightContainers)
			{
				if (Time.unscaledTime >= _nextContainers)
				{
					_containers.Clear();
					_containers.AddRange(LocationScene.GetAllObjects<LootableContainer>());
					_nextContainers = Time.unscaledTime + 10;
				}
				foreach (var container in _containers)
					if (container != null && container.isActiveAndEnabled) ConsiderItem(container, true, position);
			}
			_removedItems.Clear();
			foreach (var pair in _itemCache)
				if (!_seenItems.Contains(pair.Key)) _removedItems.Add(pair.Key);
			foreach (var owner in _removedItems) _itemCache.Remove(owner);
			_nearbyItems.Sort((a, b) => a.Distance.CompareTo(b.Distance));
			_nextItems = Time.unscaledTime + 0.5f;
		}
		foreach (var entry in _nearbyItems)
		{
			if (entry.Owner == null || !entry.Owner.gameObject.activeInHierarchy || (entry.Container ? !HighlightContainers : !HighlightItems)) continue;
			var color = entry.Container ? ContainerOutlineColor : ItemOutlineColor;
			if (entry.Owner is LootItem loot && loot.Item != null && loot.Item.QuestItem) color = QuestOutlineColor;
			entry.Target.Edge = color;
			entry.Target.Fill = Alpha(color, ItemFillOpacity);
			entry.Target.MaximumDistance = ItemHighlightDistance;
			_itemTargets.Add(entry.Target);
		}
	}

	private void ConsiderItem(Component owner, bool container, Vector3 position)
	{
		var distance = (owner.transform.position - position).sqrMagnitude;
		if (distance > (ItemHighlightDistance + 3) * (ItemHighlightDistance + 3)) return;
		_seenItems.Add(owner);
		if (!_itemCache.TryGetValue(owner, out var entry))
		{
			entry = new ItemEntry(owner, container);
			_renderers.Clear();
			owner.GetComponentsInChildren(true, _renderers);
			entry.Target.Refresh(_renderers);
			_itemCache.Add(owner, entry);
		}
		entry.Distance = distance;
		_nearbyItems.Add(entry);
	}

	private bool IsGameCamera(Camera camera)
	{
		if (camera == null || camera.cameraType != CameraType.Game) return false;
		if (camera == _mainCamera) return true;
		// Camera whitelist prevents highlights in inventory icons, mirrors and preview cameras.
		if (camera.name != "BaseOpticCamera(Clone)") return false;
		return _world?.MainPlayer?.HandsController?.IsAiming == true;
	}

	private void RenderCamera(Camera camera)
	{
		_cameraCallbacks++;
		_lastCamera = camera.name;
		if (_renderer == null) return;
		// Optic/manual cameras can render before LateUpdate. Cached targets remain valid;
		// an unrelated camera must never erase commands already prepared for the main one.
		if (!Enabled || _failed || _preparedFrame < 0)
		{
			_renderer.Clear();
			return;
		}
		if (!IsGameCamera(camera))
		{
			_renderer.Clear(camera);
			return;
		}
		try
		{
			_renderer.Record(camera, _playerTargets, _itemTargets, 2 * (_players?.OutlineScale ?? 1), ItemOutlineWidth, HighlightScreenRadius, MaximumItemHighlights);
			if (Time.unscaledTime >= _nextCameraStatus)
			{
				_nextCameraStatus = Time.unscaledTime + 5;
				var target = camera.targetTexture;
				Log($"Render: camera={camera.name}; path={camera.actualRenderingPath}; pixels={camera.pixelWidth}x{camera.pixelHeight}; scaled={camera.scaledPixelWidth}x{camera.scaledPixelHeight}; rect={camera.rect}; target={(target != null ? target.name + " " + target.width + "x" + target.height : "screen")}; selected={_renderer.TargetCount}; draws={_renderer.DrawCount}; frame={Time.frameCount}; prepared={_preparedFrame}; event={HighlightRenderer.Event}");
			}
		}
		catch (Exception error)
		{
			_renderer.Detach();
			_failed = true;
			Debug.LogError("[EFT Trainer] Highlights disabled after a rendering error: " + error);
			Log("Rendering failed: " + error);
		}
	}

	private void ClearWorld()
	{
		_renderer?.Detach();
		_playerCache.Clear(); _itemCache.Clear(); _containers.Clear(); _nearbyItems.Clear();
		_playerTargets.Clear(); _itemTargets.Clear(); _seenItems.Clear();
		_mainCamera = null;
		_nextItems = _nextContainers = 0;
		_preparedFrame = -1;
		_failed = false;
	}

	[UsedImplicitly]
	private void OnDisable()
	{
		Camera.onPreRender -= RenderCamera;
		ClearWorld();
	}

	[UsedImplicitly]
	private void OnDestroy()
	{
		OnDisable();
		_renderer?.Dispose();
		_renderer = null;
		// Unity destroys native objects during shutdown before their managed wrappers.
		if (_bundle != null) _bundle.Unload(true);
		_bundle = null;
	}

	private void Log(string message)
	{
		if (_logUnavailable) return;
		try
		{
			_logPath ??= Path.GetFullPath(Path.Combine(Application.dataPath, "..", "trainer-highlights.log"));
			if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 1024 * 1024)
				File.WriteAllText(_logPath, "Highlight diagnostic log rotated.\n");
			File.AppendAllText(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
		}
		catch (Exception error)
		{
			_logUnavailable = true;
			Debug.LogError("[EFT Trainer] Cannot write highlight diagnostics: " + error.Message);
		}
	}
}
