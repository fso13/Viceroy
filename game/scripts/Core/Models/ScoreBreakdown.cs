namespace Namestnik.Core.Models;

public sealed class ScoreBreakdown
{
	public required int PlayerId { get; init; }
	public required string DisplayName { get; init; }

	public int Circles { get; set; }
	public int Infinites { get; set; }
	public int Laws { get; set; }
	public int VpTokens { get; set; }
	public int Magic { get; set; }
	public int Sets { get; set; }
	public int AttackPenalty { get; set; }

	public int Total =>
		Circles + Infinites + Laws + VpTokens + Magic + Sets - AttackPenalty;

	public List<string> Notes { get; } = new();

	public override string ToString() =>
		$"{DisplayName}: {Total} " +
		$"(круги {Circles}, ∞ {Infinites}, законы {Laws}, VP-жетоны {VpTokens}, " +
		$"магия {Magic}, наборы {Sets}, штраф атаки −{AttackPenalty})";
}

public sealed class MatchResult
{
	public required IReadOnlyList<ScoreBreakdown> Scores { get; init; }
	public required IReadOnlyList<int> WinnerIds { get; init; }

	public bool IsDraw => WinnerIds.Count > 1;
}
