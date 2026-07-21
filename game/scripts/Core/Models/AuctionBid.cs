namespace Namestnik.Core.Models;

public enum AuctionSubPhase
{
	/// <summary>Collecting simultaneous sealed bids from active players.</summary>
	CollectingBids,

	/// <summary>One or more winners must pick among 2 cards of a color.</summary>
	ChoosingCards,

	/// <summary>Passer chooses gem colors (3 + science).</summary>
	ClaimingPassGems,

	/// <summary>Auction finished for this turn; board will advance.</summary>
	Done
}

public abstract record AuctionBid;

public sealed record GemAuctionBid(GemColor Color) : AuctionBid;

public sealed record AttackAuctionBid(int? PreferredCharacterId = null) : AuctionBid;

public sealed record PassAuctionBid : AuctionBid;

/// <summary>Winner still needs to pick a card from Options.</summary>
public sealed class PendingCardChoice
{
	public required int PlayerId { get; init; }
	public required GemColor Color { get; init; }
	public required List<int> Options { get; init; }
}

public sealed class SealedBidEntry
{
	public required int PlayerId { get; init; }
	public required AuctionBid Bid { get; init; }
}
