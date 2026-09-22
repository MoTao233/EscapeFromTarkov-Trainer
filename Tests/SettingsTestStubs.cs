// Minimal host contracts for running production persistence and team logic outside Unity.
// The regular trainer build additionally checks these calls against the actual game assemblies.
using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable

namespace EFT.UI
{
	internal static class PreloaderUI { public static bool Instantiated => true; }
	internal static class ConsoleScreen
	{
		public static readonly List<string> Messages = new();
		public static void Log(string message) => Messages.Add(message);
	}
}

namespace EFT.Trainer
{
	internal static class TestStrings { public static string Red(this string value) => value; }
}

namespace EFT.Trainer.Features
{
	internal interface IFeature { [JsonIgnore] string Name { get; } }
	internal abstract class Feature { }
}

namespace JsonType
{
	public enum ELootRarity { Common, Rare, Superrare }
}

namespace EFT
{
	internal interface IPlayer { string? ProfileId { get; } }
	internal sealed class Player : IPlayer
	{
		public string? ProfileId { get; set; }
		public Profile? Profile { get; set; } = new();
		public AIData? AIData { get; set; }
	}
	internal sealed class Profile { public PlayerInfo? Info { get; set; } = new(); }
	internal sealed class PlayerInfo { public string? GroupId { get; set; } }
	internal sealed class AIData { public BotOwner? BotOwner { get; set; } }
	internal sealed class BotOwner { public BotFollower? BotFollower { get; set; } }
	internal sealed class BotFollower
	{
		public Boss? BossToFollow { get; set; }
		public bool HaveBoss => BossToFollow != null;
	}
	internal sealed class Boss
	{
		public Player? Leader { get; set; }
		public IPlayer? Player() => Leader;
	}
}
