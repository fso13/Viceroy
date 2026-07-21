namespace Namestnik.Core.Models;

/// <summary>Authoritative match state. Mutated only by <see cref="GameEngine"/>.</summary>
public sealed class GameState
{
	public const int MaxTurns = 12;
	public const int MaxPyramidLevels = 5;
	public const int MaxAuctionRounds = 3;
	public const int MaxDevelopmentRounds = 3;
	public const int PhysicalGemsPerColor = 16;

	public required int Seed { get; init; }
	public required GameMode Mode { get; init; }
	public required int HumanPlayerCount { get; init; }
	public required int VirtualOpponentCount { get; init; }

	public int Turn { get; set; } = 1;
	public TurnPhase Phase { get; set; } = TurnPhase.Setup;
	public AuctionRound AuctionRound { get; set; } = AuctionRound.None;
	public AuctionSubPhase AuctionSubPhase { get; set; } = AuctionSubPhase.CollectingBids;
	public int DevelopmentRound { get; set; }
	public DevelopmentSubPhase DevelopmentSubPhase { get; set; } = DevelopmentSubPhase.CollectingActions;

	public List<PlayerState> Players { get; } = new();

	public List<int> BigDeck { get; } = new();
	public List<int> SmallDeck { get; } = new();
	public List<int> LawDeck { get; } = new();
	public List<int> Discard { get; } = new();

	public AuctionSlot[] AuctionSlots { get; } =
	[
		new(GemColor.Blue),
		new(GemColor.Red),
		new(GemColor.Green),
		new(GemColor.Yellow)
	];

	public GemWallet Reserve { get; } = new();

	/// <summary>Solo virtual opponent gem "box" — never mixed into reserve.</summary>
	public GemWallet VirtualBox { get; } = new();

	public Dictionary<int, AuctionBid> SealedBids { get; } = new();
	public List<PendingCardChoice> PendingCardChoices { get; } = new();

	public Dictionary<int, DevelopmentAction> SealedDevActions { get; } = new();
	public PendingLevel5Choice? PendingLevel5 { get; set; }
	public PendingLawResolution? PendingLaw { get; set; }
	public PendingRewardChoice? PendingRewardChoice { get; set; }
	public PendingPassGems? PendingPassGems { get; set; }
	public PendingTokenSwap? PendingTokenSwap { get; set; }

	/// <summary>Plays deferred while a prompt (law / L5 / reward / pass gems) is open.</summary>
	public List<(int PlayerId, PlayDevelopmentAction Play)> DeferredDevPlays { get; } = new();

	/// <summary>Auction passers waiting to claim gems after higher-priority passes.</summary>
	public List<int> DeferredPassers { get; } = new();

	public int NextInstanceId { get; set; } = 1;

	/// <summary>Set when the last 4 big-deck cards are dealt; the following turn is final.</summary>
	public bool NextTurnIsLast { get; set; }

	/// <summary>True while playing the final turn after <see cref="NextTurnIsLast"/>.</summary>
	public bool FinalTurnInProgress { get; set; }

	public MatchResult? Result { get; set; }
	public bool IsGameOver => Phase == TurnPhase.GameOver;

	public PlayerState GetPlayer(int playerId) =>
		Players.First(p => p.PlayerId == playerId);

	public IEnumerable<PlayerState> ActiveAuctionPlayers() =>
		Players.Where(p => !p.HasPassedAuction && !p.AcquiredAuctionCardThisTurn);

	public IEnumerable<PlayerState> ActiveDevelopmentPlayers() =>
		Players.Where(p =>
			p.Role != SessionRole.VirtualOpponent &&
			!p.HasPassedDevelopment &&
			!p.ActedThisDevelopmentRound);

	public AuctionSlot Slot(GemColor color) =>
		AuctionSlots.First(s => s.Color == color);

	public bool HasPendingChoice(int playerId) =>
		PendingCardChoices.Any(c => c.PlayerId == playerId);
}

public sealed class AuctionSlot(GemColor color)
{
	public GemColor Color { get; } = color;
	public int? CardAtBase { get; set; }
	public int? CardAtTip { get; set; }

	public IEnumerable<int> AvailableCards()
	{
		if (CardAtTip is int tip)
			yield return tip;
		if (CardAtBase is int bas)
			yield return bas;
	}

	public int AvailableCount => AvailableCards().Count();

	public bool TryRemoveCard(int characterId)
	{
		if (CardAtTip == characterId)
		{
			CardAtTip = null;
			return true;
		}

		if (CardAtBase == characterId)
		{
			CardAtBase = null;
			return true;
		}

		return false;
	}
}
