using Namestnik.Core.Models;

namespace Namestnik.Core.Events;

/// <summary>Immutable facts emitted by the engine for UI / network replication.</summary>
public abstract record GameEvent;

public sealed record MatchStartedEvent(int Seed, GameMode Mode, int PlayerCount) : GameEvent;

public sealed record PhaseChangedEvent(TurnPhase Phase, int Turn) : GameEvent;

public sealed record AuctionRoundStartedEvent(int Round) : GameEvent;

public sealed record BidAcceptedEvent(int PlayerId) : GameEvent;

public sealed record AuctionRoundResolvedEvent(int Round, string Summary) : GameEvent;

public sealed record CardChoiceRequiredEvent(int PlayerId, GemColor Color, IReadOnlyList<int> Options)
	: GameEvent;

public sealed record AuctionCardAcquiredEvent(int PlayerId, int CharacterId, GemColor? Color)
	: GameEvent;

public sealed record AuctionBoardAdvancedEvent(bool LastCardsDealt) : GameEvent;

public sealed record AuctionResolvedEvent(string Summary) : GameEvent;

public sealed record Level5ChoiceRequiredEvent(int PlayerId, int CharacterDefinitionId) : GameEvent;

public sealed record LawPromptEvent(
	int PlayerId,
	int LawDefinitionId,
	LawPromptKind Kind,
	IReadOnlyList<string> OptionLabels) : GameEvent;

public sealed record RewardChoiceRequiredEvent(int PlayerId, IReadOnlyList<string> OptionLabels) : GameEvent;

public sealed record PassGemsRequiredEvent(int PlayerId, int Amount) : GameEvent;

public sealed record CardDrawnEvent(int PlayerId, int DefinitionId, CardKind Kind) : GameEvent;

public sealed record LogEvent(string Message) : GameEvent;

public sealed record ErrorEvent(int PlayerId, string Message) : GameEvent;

public sealed record MatchEndedEvent(MatchResult Result) : GameEvent;
