using Namestnik.Core.Models;

namespace Namestnik.Core.Commands;

/// <summary>Player intent sent from UI / network / AI into the engine.</summary>
public abstract record GameCommand(int PlayerId);

/// <summary>Submit a sealed auction bid for the current round.</summary>
public sealed record SubmitAuctionBidCommand(int PlayerId, AuctionBid Bid) : GameCommand(PlayerId);

public sealed record PassAuctionCommand(int PlayerId) : GameCommand(PlayerId);

public sealed record BidGemCommand(int PlayerId, GemColor Color) : GameCommand(PlayerId);

public sealed record BidAttackCommand(int PlayerId, int? PreferredCharacterId = null)
	: GameCommand(PlayerId);

/// <summary>Pick a card after winning a contested/sole auction with 2 options.</summary>
public sealed record ChooseAuctionCardCommand(int PlayerId, int CharacterId) : GameCommand(PlayerId);

public sealed record PlayCardCommand(
	int PlayerId,
	int HandIndex,
	int PyramidLevel,
	int SlotHint,
	int? LawTargetInstanceId = null,
	IReadOnlyList<int>? ExtraHandIndices = null)
	: GameCommand(PlayerId);

public sealed record DiscardForGemsCommand(int PlayerId, int HandIndex) : GameCommand(PlayerId);

public sealed record PassDevelopmentCommand(int PlayerId) : GameCommand(PlayerId);

/// <summary>Level 5: false = rewards of levels 1–3, true = 15 VP.</summary>
public sealed record ChooseLevel5RewardCommand(int PlayerId, bool TakeFifteenVp) : GameCommand(PlayerId);

/// <summary>Resolve an active on-play / triggered law prompt.</summary>
public sealed record ResolveLawCommand(
	int PlayerId,
	int? OptionIndex = null,
	GemColor? GemColor = null,
	IReadOnlyList<int>? HandIndices = null,
	int? CharacterDefinitionId = null)
	: GameCommand(PlayerId);

/// <summary>When passing auction, pick the next gem color (or confirm when Amount reached).</summary>
public sealed record ClaimPassGemsCommand(int PlayerId, GemColor? Color = null, bool Confirm = false)
	: GameCommand(PlayerId);

public sealed record ChooseRewardCommand(int PlayerId, int OptionIndex) : GameCommand(PlayerId);

public sealed record UndoCommand(int PlayerId) : GameCommand(PlayerId);

/// <summary>Law 67 multi-step: decline, pick own/other token, pay 3 gems, confirm.</summary>
public sealed record ResolveTokenSwapCommand(
	int PlayerId,
	bool Decline = false,
	int? OwnCardInstanceId = null,
	TokenKind? OwnToken = null,
	int? OtherPlayerId = null,
	int? OtherCardInstanceId = null,
	TokenKind? OtherToken = null,
	GemColor? PayGem = null,
	bool Confirm = false)
	: GameCommand(PlayerId);
