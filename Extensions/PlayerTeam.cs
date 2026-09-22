using System;

#nullable enable

namespace EFT.Trainer.Extensions;

internal static class PlayerTeam
{
	public static bool IsTeammate(Player? player, Player? local)
	{
		if (player == null || local == null || player == local) return false;
		if (SameGroup(player, local)) return true;

		// FriendlyPMC / FriendlyFireTeam attach recruited AI to the game's native follower data.
		// Following a different boss (including an enemy boss) does not make a bot friendly.
		var follower = player.AIData?.BotOwner?.BotFollower;
		if (follower == null || !follower.HaveBoss) return false;
		var leader = follower.BossToFollow?.Player();
		return leader != null && (ReferenceEquals(leader, local) || SameId(leader.ProfileId, local.ProfileId) ||
			leader is Player leaderPlayer && SameGroup(leaderPlayer, local));
	}

	private static bool SameGroup(Player player, Player local) =>
		SameId(player.Profile?.Info?.GroupId, local.Profile?.Info?.GroupId);

	private static bool SameId(string? left, string? right) =>
		!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
		left != "0" && string.Equals(left, right, StringComparison.Ordinal);
}
