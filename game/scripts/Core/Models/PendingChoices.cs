namespace Namestnik.Core.Models;

public sealed class PendingRewardChoice
{
	public required int PlayerId { get; init; }
	public required int HostInstanceId { get; init; }
	public required IReadOnlyList<Reward> Options { get; init; }
	public List<string> OptionLabels { get; init; } = new();
}

public sealed class PendingPassGems
{
	public required int PlayerId { get; init; }
	public required int Amount { get; init; }
	public List<GemColor> Picked { get; init; } = new();
}

public enum TokenKind
{
	VictoryPoints,
	Science,
	Magic,
	Defense
}

/// <summary>Law 67: optional 1-token swap with another player for 3 gems.</summary>
public sealed class PendingTokenSwap
{
	public required int PlayerId { get; init; }
	public required int LawInstanceId { get; init; }
	public int? OwnCardInstanceId { get; set; }
	public TokenKind? OwnToken { get; set; }
	public int? OtherPlayerId { get; set; }
	public int? OtherCardInstanceId { get; set; }
	public TokenKind? OtherToken { get; set; }
	public List<GemColor> Payment { get; } = new();
}
