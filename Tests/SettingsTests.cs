using System;
using System.IO;
using EFT;
using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using EFT.Trainer.Features;
using UnityEngine;

#nullable enable

internal sealed class SettingsFixture : Feature
{
	[ConfigurationProperty(Order = 1)] public bool ShowTeammates { get; set; }
	[ConfigurationProperty(Order = 2)] public PlayerColor TeammateColors { get; set; } = new(Color.cyan, Color.cyan, Color.cyan);
	[ConfigurationProperty(Order = 3)] public KeyCode Key { get; set; } = KeyCode.Insert;
	[ConfigurationProperty(Order = 4)] public Color Tint { get; set; } = Color.green;
	[ConfigurationProperty(Order = 5)] public float BodyTint { get; set; } = 0.3f;
	[ConfigurationProperty(Order = 6)] public TrackedItem[] Items { get; set; } = [];
	[ConfigurationProperty(Skip = true)] public bool Skipped { get; set; } = true;
	public bool Unmarked { get; set; } = true;
	private int _validated = 12;
	[ConfigurationProperty(Order = 7)] public int Validated
	{
		get => _validated;
		set => _validated = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
	}
	[ConfigurationProperty(Order = 8)] public int Tail { get; set; } = 42;
}

internal static class SettingsTests
{
	private static int _assertions;
	private static void Check(bool value, string message)
	{
		_assertions++;
		if (!value) throw new Exception(message);
	}
	private static bool Same(Color a, Color b) => a.Equals(b);
	private static SettingsFixture Reload(string path)
	{
		var fresh = new SettingsFixture();
		Check(ConfigurationManager.Load(path, [fresh]), "Load must report success.");
		return fresh;
	}

	private static void Main(string[] args)
	{
		try { Run(args); }
		catch (Exception error)
		{
			Console.Error.WriteLine(error.GetType().FullName);
			Console.Error.WriteLine(error.Message);
			Console.Error.WriteLine(error.StackTrace);
			foreach (var message in EFT.UI.ConsoleScreen.Messages) Console.Error.WriteLine(message);
			Environment.ExitCode = 1;
		}
	}

	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private static void Run(string[] args)
	{
		if (args.Length != 1) throw new ArgumentException("Pass a workspace test directory.");
		var root = Path.Combine(Path.GetFullPath(args[0]), Guid.NewGuid().ToString("N"));
		var path = Path.Combine(root, "missing-documents", "Escape from Tarkov", "trainer.ini");
		Check(!Directory.Exists(Path.GetDirectoryName(path)), "Start without the config directory.");
		var settings = new SettingsFixture
		{
			ShowTeammates = true, BodyTint = 0.71f, Key = KeyCode.Home,
			TeammateColors = new(new Color(.11f, .22f, .33f, .44f), Color.yellow, Color.magenta)
			{ OccludedColor = new Color(.9f, .8f, .7f, .6f) },
			Items = [new TrackedItem("医疗包"), new TrackedItem("custom", new Color(.1f, .2f, .3f, .4f))]
		};
		Check(ConfigurationManager.Save(path, [settings]), "Saving must create missing parents.");
		Check(ConfigurationManager.LastOperationSucceeded && ConfigurationManager.LastStatus.Contains(path), "Visible success must identify the file.");
		string first = File.ReadAllText(path);
		Check(!first.Contains("Skipped=") && !first.Contains("Unmarked="), "Only persisted properties belong in config.");
		var loaded = Reload(path);
		Check(loaded.ShowTeammates && loaded.Key == KeyCode.Home && loaded.BodyTint == .71f, "Fresh instance restores toggles, key and opacity.");
		Check(Same(loaded.TeammateColors.Color, settings.TeammateColors.Color), "Nested RGBA including alpha survives restart.");
		Check(Same(loaded.TeammateColors.BorderColor, Color.yellow) && Same(loaded.TeammateColors.InfoColor, Color.magenta), "Separate outline and info colors survive restart.");
		Check(Same(loaded.TeammateColors.OccludedColor, settings.TeammateColors.OccludedColor), "Hidden fill color survives restart.");
		Check(loaded.Items.Length == 2 && loaded.Items[0].Name == "医疗包" && loaded.Items[0].Color == null &&
			Same(loaded.Items[1].Color!.Value, settings.Items[1].Color!.Value), "Legacy tracked item names and nullable custom colors remain compatible.");
		settings.BodyTint = .19f;
		settings.ShowTeammates = false;
		Check(ConfigurationManager.Save(path, [settings]), "Replacing an existing save succeeds.");
		Check(File.ReadAllText(path + ".bak") == first, "Previous complete config is backed up.");
		loaded = Reload(path);
		Check(!loaded.ShowTeammates && loaded.BodyTint == .19f, "The latest save is loaded, not the backup.");
		loaded.BodyTint = .95f;
		Check(ConfigurationManager.Load(path, [loaded]) && loaded.BodyTint == .19f, "Manual load resets an edited live setting.");
		string beforeFailure = File.ReadAllText(path);
		using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
			Check(!ConfigurationManager.Save(path, [settings]), "A locked destination must report failure.");
		Check(!ConfigurationManager.LastOperationSucceeded && File.ReadAllText(path) == beforeFailure, "Failed replacement preserves the original file and reports an error.");
		Check(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length == 0, "Failed save cleans its temporary file.");
		Check(ConfigurationManager.Save(path, [settings]), "Saving works again after unlocking, with an existing backup.");

		File.WriteAllLines(path,
		[
			"SettingsFixture.TeammateColors=null",
			"SettingsFixture.Key=\"invalid-key\"",
			"SettingsFixture.Tint=[1,0]",
			"SettingsFixture.BodyTint=0.62",
			"SettingsFixture.Validated=-1",
			"SettingsFixture.Tail=89",
			"SettingsFixture.UnifiedEspColor=true"
		]);
		loaded = new SettingsFixture();
		Check(!ConfigurationManager.Load(path, [loaded]), "Partial load must not claim full success.");
		Check(loaded.TeammateColors != null && loaded.Key == KeyCode.Insert && Same(loaded.Tint, Color.green) && loaded.Validated == 12,
			"Null objects, bad key, short color and failing setter keep their previous values.");
		Check(loaded.BodyTint == .62f && loaded.Tail == 89, "Valid values after corrupt entries still load.");
		Check(ConfigurationManager.LastStatus.Contains("4"), "Partial-load status includes rejected count.");
		File.WriteAllText(path, "SettingsFixture.TeammateColors={\"Color\":[0.1,0.2,0.3,0.4],\"BorderColor\":[1,0,0,1],\"InfoColor\":[1,1,1,1]}");
		loaded = Reload(path);
		Check(Same(loaded.TeammateColors.OccludedColor, loaded.TeammateColors.Color), "Older color objects without hidden fill inherit the body color.");
		File.WriteAllText(path, "SettingsFixture.Tint=[1,2,3,\"NaN\"]\nSettingsFixture.Tail=99");
		Check(!ConfigurationManager.Load(path, [loaded]) && loaded.Tail == 99 && Same(loaded.Tint, Color.green), "Non-finite colors cannot poison rendering or block remaining settings.");
		File.WriteAllText(path, "; empty config");
		Check(!ConfigurationManager.Load(path, [loaded]), "Zero restored values cannot be reported as success.");
		Check(!ConfigurationManager.Load(Path.Combine(root, "absent.ini"), [loaded]), "Missing file reports failure.");
		var tracked = Path.Combine(root, "another-missing-directory", "tracked.json");
		ConfigurationManager.SavePropertyValue(tracked, settings, nameof(SettingsFixture.Items));
		loaded.Items = [];
		ConfigurationManager.LoadPropertyValue(tracked, loaded, nameof(SettingsFixture.Items));
		Check(loaded.Items.Length == 2, "Single-property exports also create parents and load correctly.");

		TestTeams();
		Console.WriteLine($"PASS: {_assertions} configuration and team assertions. Fixtures: {root}");
	}

	private static Player Person(string? id, string? group) => new() { ProfileId = id, Profile = new Profile { Info = new PlayerInfo { GroupId = group } } };
	private static Player Follower(Player? leader) => new()
	{
		AIData = new AIData { BotOwner = new BotOwner { BotFollower = new BotFollower { BossToFollow = new Boss { Leader = leader } } } }
	};
	private static void TestTeams()
	{
		var local = Person("local", "squad");
		Check(!PlayerTeam.IsTeammate(null, local) && !PlayerTeam.IsTeammate(local, null), "Missing players are not teammates.");
		Check(!PlayerTeam.IsTeammate(local, local), "Local player is not a display target.");
		Check(PlayerTeam.IsTeammate(Person("friend", "squad"), local), "Nonempty shared GroupId identifies coop teammates.");
		Check(!PlayerTeam.IsTeammate(Person("enemy", "other"), local), "Different group remains hostile.");
		foreach (var empty in new string?[] { null, "", " ", "0" })
			Check(!PlayerTeam.IsTeammate(Person("a", empty), Person("b", empty)), "Missing/default group cannot make unrelated players friendly.");
		Check(!PlayerTeam.IsTeammate(new Player { Profile = null }, local), "Incomplete spawn profile is safe.");
		Check(PlayerTeam.IsTeammate(Follower(local), local), "Own recruited AI is a teammate even without GroupId.");
		Check(PlayerTeam.IsTeammate(Follower(Person("local", null)), local), "Matching leader profile works through a different player wrapper.");
		Check(PlayerTeam.IsTeammate(Follower(Person("friend", "squad")), local), "A coop teammate's follower is friendly.");
		Check(!PlayerTeam.IsTeammate(Follower(Person("boss", "enemy")), local), "An enemy boss's follower remains hostile.");
		var follower = Follower(local);
		follower.AIData!.BotOwner!.BotFollower!.BossToFollow = null;
		Check(!PlayerTeam.IsTeammate(follower, local), "Dismissed follower is reevaluated immediately.");
		Check(!PlayerTeam.IsTeammate(Follower(null), local), "Missing AI leader is safe.");
		Check(!PlayerTeam.IsTeammate(Follower(Person(null, null)), Person(null, null)), "Missing leader profile IDs never match each other.");
	}
}
