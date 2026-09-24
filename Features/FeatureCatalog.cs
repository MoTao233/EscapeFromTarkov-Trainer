using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace EFT.Trainer.Features;

// Where a feature lives in the menu. Order is per category, spaced so entries can be inserted later.
internal readonly struct FeaturePlacement(Type type, FeatureCategory category, FeatureArea area, int order, bool legacy = false)
{
	public Type Type { get; } = type;
	public FeatureCategory Category { get; } = category;
	public FeatureArea Area { get; } = area;
	public int Order { get; } = order;
	public bool Legacy { get; } = legacy;
}

internal enum FeatureCategory
{
	Aim,
	Esp,
	Vision,
	Combat,
	Character,
	World,
	System,
	Misc,
}

// Secondary grouping inside a category; General is rendered without a header.
internal enum FeatureArea
{
	General,
	Radar,
	Interaction,
}

internal static class FeatureCatalog
{
	public static readonly FeatureCategory[] Categories =
	[
		FeatureCategory.Aim,
		FeatureCategory.Esp,
		FeatureCategory.Vision,
		FeatureCategory.Combat,
		FeatureCategory.Character,
		FeatureCategory.World,
		FeatureCategory.System,
		FeatureCategory.Misc,
	];

	private static readonly FeaturePlacement[] Placements =
	[
		new(typeof(Aimbot), FeatureCategory.Aim, FeatureArea.General, 10),

		new(typeof(Players), FeatureCategory.Esp, FeatureArea.General, 10),
		new(typeof(Highlights), FeatureCategory.Esp, FeatureArea.General, 20),
		new(typeof(Grenades), FeatureCategory.Esp, FeatureArea.General, 30),
		new(typeof(LootItems), FeatureCategory.Esp, FeatureArea.General, 40),
		new(typeof(LootableContainers), FeatureCategory.Esp, FeatureArea.General, 50),
		new(typeof(ExfiltrationPoints), FeatureCategory.Esp, FeatureArea.General, 60),
		new(typeof(Quests), FeatureCategory.Esp, FeatureArea.General, 70),
		new(typeof(Radar), FeatureCategory.Esp, FeatureArea.Radar, 10, legacy: true),
		new(typeof(Map), FeatureCategory.Esp, FeatureArea.Radar, 20, legacy: true),

		new(typeof(NightVision), FeatureCategory.Vision, FeatureArea.General, 10),
		new(typeof(ThermalVision), FeatureCategory.Vision, FeatureArea.General, 20),
		new(typeof(NoVisor), FeatureCategory.Vision, FeatureArea.General, 30),
		new(typeof(NoFlash), FeatureCategory.Vision, FeatureArea.General, 40),
		new(typeof(FovChanger), FeatureCategory.Vision, FeatureArea.General, 50),
		new(typeof(FreeCamera), FeatureCategory.Vision, FeatureArea.General, 60),
		new(typeof(CrossHair), FeatureCategory.Vision, FeatureArea.General, 70),
		new(typeof(Hud), FeatureCategory.Vision, FeatureArea.General, 80),
		new(typeof(Hits), FeatureCategory.Vision, FeatureArea.General, 90),

		new(typeof(NoRecoil), FeatureCategory.Combat, FeatureArea.General, 10),
		new(typeof(NoSway), FeatureCategory.Combat, FeatureArea.General, 20),
		new(typeof(NoMalfunctions), FeatureCategory.Combat, FeatureArea.General, 30),
		new(typeof(WallShoot), FeatureCategory.Combat, FeatureArea.General, 40),
		new(typeof(AutomaticGun), FeatureCategory.Combat, FeatureArea.General, 50),
		new(typeof(Ammunition), FeatureCategory.Combat, FeatureArea.General, 60),
		new(typeof(Durability), FeatureCategory.Combat, FeatureArea.General, 70),
		new(typeof(QuickTrow), FeatureCategory.Combat, FeatureArea.General, 80),

		new(typeof(Health), FeatureCategory.Character, FeatureArea.General, 10),
		new(typeof(Stamina), FeatureCategory.Character, FeatureArea.General, 20),
		new(typeof(NoCollision), FeatureCategory.Character, FeatureArea.General, 30),
		new(typeof(Ghost), FeatureCategory.Character, FeatureArea.General, 40),
		new(typeof(Speed), FeatureCategory.Character, FeatureArea.General, 50),
		new(typeof(Skills), FeatureCategory.Character, FeatureArea.General, 60),
		new(typeof(Examine), FeatureCategory.Character, FeatureArea.Interaction, 10),
		new(typeof(Interact), FeatureCategory.Character, FeatureArea.Interaction, 20),

		new(typeof(AirDrop), FeatureCategory.World, FeatureArea.General, 10),
		new(typeof(Mortar), FeatureCategory.World, FeatureArea.General, 20),
		new(typeof(Train), FeatureCategory.World, FeatureArea.General, 30),
		new(typeof(Weather), FeatureCategory.World, FeatureArea.General, 40),
		new(typeof(WorldInteractiveObjects), FeatureCategory.World, FeatureArea.General, 50),

		new(typeof(Commands), FeatureCategory.System, FeatureArea.General, 10),
		new(typeof(GameState), FeatureCategory.System, FeatureArea.General, 20),
	];

	private static readonly Dictionary<Type, FeaturePlacement> ByType = Placements.ToDictionary(p => p.Type);

	public static FeaturePlacement? GetPlacement(Type type)
	{
		return ByType.TryGetValue(type, out var placement) ? placement : null;
	}

	public static bool IsLegacy(Type type)
	{
		return GetPlacement(type)?.Legacy ?? false;
	}

	public static FeatureArea GetArea(Type type)
	{
		return GetPlacement(type)?.Area ?? FeatureArea.General;
	}

	public static Feature[] GetFeatures(FeatureCategory category, FeatureArea area)
	{
		var hash = Placements.ToDictionary(p => p.Type, p => p.Order);

		return
		[
			.. Context
				.Features
				.Value
				.Where(f => GetPlacement(f.GetType()) is { } placement && placement.Category == category && placement.Area == area)
				.OrderBy(f => hash[f.GetType()])
		];
	}

	public static FeatureArea[] GetAreas(FeatureCategory category)
	{
		return
		[
			.. Placements
				.Where(p => p.Category == category)
				.Select(p => p.Area)
				.Distinct()
				.OrderBy(a => a)
		];
	}
}
