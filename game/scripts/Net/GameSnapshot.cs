using System.Text.Json;
using System.Text.Json.Serialization;
using Namestnik.Core.Models;

namespace Namestnik.Net;

/// <summary>
/// Compact host→client state snapshot. Built from <see cref="GameState"/>;
/// restore produces a fresh state suitable for <see cref="Core.GameEngine.LoadState"/>.
/// </summary>
public static class GameSnapshot
{
	static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() }
	};

	public static string Serialize(GameState state) =>
		JsonSerializer.Serialize(FromState(state), Options);

	public static GameState Deserialize(string json, GameMode? modeOverride = null)
	{
		var dto = JsonSerializer.Deserialize<GameStateDto>(json, Options)
			?? throw new JsonException("Empty snapshot");
		if (modeOverride is { } mode)
			dto.Mode = mode;
		return ToState(dto);
	}

	static GameStateDto FromState(GameState s) => new()
	{
		Seed = s.Seed,
		Mode = s.Mode,
		HumanPlayerCount = s.HumanPlayerCount,
		VirtualOpponentCount = s.VirtualOpponentCount,
		Turn = s.Turn,
		Phase = s.Phase,
		AuctionRound = s.AuctionRound,
		AuctionSubPhase = s.AuctionSubPhase,
		DevelopmentRound = s.DevelopmentRound,
		DevelopmentSubPhase = s.DevelopmentSubPhase,
		NextInstanceId = s.NextInstanceId,
		NextTurnIsLast = s.NextTurnIsLast,
		FinalTurnInProgress = s.FinalTurnInProgress,
		Reserve = WalletDto.From(s.Reserve),
		VirtualBox = WalletDto.From(s.VirtualBox),
		BigDeck = s.BigDeck.ToList(),
		SmallDeck = s.SmallDeck.ToList(),
		LawDeck = s.LawDeck.ToList(),
		Discard = s.Discard.ToList(),
		AuctionSlots = s.AuctionSlots.Select(slot => new AuctionSlotDto
		{
			Color = slot.Color,
			CardAtBase = slot.CardAtBase,
			CardAtTip = slot.CardAtTip
		}).ToList(),
		SealedBids = s.SealedBids.ToDictionary(
			kv => kv.Key.ToString(),
			kv => BidDto.From(kv.Value)),
		PendingCardChoices = s.PendingCardChoices.Select(c => new PendingCardChoiceDto
		{
			PlayerId = c.PlayerId,
			Color = c.Color,
			Options = c.Options.ToList()
		}).ToList(),
		SealedDevActions = s.SealedDevActions.ToDictionary(
			kv => kv.Key.ToString(),
			kv => DevActionDto.From(kv.Value)),
		PendingLevel5 = s.PendingLevel5 is { } l5
			? new PendingLevel5Dto
			{
				PlayerId = l5.PlayerId,
				PyramidCardInstanceId = l5.PyramidCardInstanceId,
				CharacterDefinitionId = l5.CharacterDefinitionId
			}
			: null,
		PendingLaw = s.PendingLaw is { } law
			? new PendingLawDto
			{
				PlayerId = law.PlayerId,
				LawInstanceId = law.LawInstanceId,
				LawDefinitionId = law.LawDefinitionId,
				Kind = law.Kind,
				OptionLabels = law.OptionLabels.ToList(),
				DrawnInstanceId = law.DrawnInstanceId,
				AwaitingInfiniteColor = law.AwaitingInfiniteColor
			}
			: null,
		PendingRewardChoice = s.PendingRewardChoice is { } rc
			? new PendingRewardDto
			{
				PlayerId = rc.PlayerId,
				HostInstanceId = rc.HostInstanceId,
				OptionLabels = rc.OptionLabels.ToList()
			}
			: null,
		PendingDeckDraw = s.PendingDeckDraw is { } dd
			? new PendingDeckDrawDto
			{
				PlayerId = dd.PlayerId,
				Remaining = dd.Remaining
			}
			: null,
		PendingPassGems = s.PendingPassGems is { } pg
			? new PendingPassGemsDto
			{
				PlayerId = pg.PlayerId,
				Amount = pg.Amount,
				Picked = pg.Picked.ToList()
			}
			: null,
		PendingTokenSwap = s.PendingTokenSwap is { } ts
			? new PendingTokenSwapDto
			{
				PlayerId = ts.PlayerId,
				LawInstanceId = ts.LawInstanceId,
				OwnCardInstanceId = ts.OwnCardInstanceId,
				OwnToken = ts.OwnToken,
				OtherPlayerId = ts.OtherPlayerId,
				OtherCardInstanceId = ts.OtherCardInstanceId,
				OtherToken = ts.OtherToken,
				Payment = ts.Payment.ToList()
			}
			: null,
		DeferredDevPlays = s.DeferredDevPlays.Select(d => new DeferredPlayDto
		{
			PlayerId = d.PlayerId,
			Play = DevActionDto.From(d.Play)
		}).ToList(),
		DeferredPassers = s.DeferredPassers.ToList(),
		Players = s.Players.Select(PlayerDto.From).ToList(),
		Result = s.Result is { } r
			? new MatchResultDto
			{
				WinnerIds = r.WinnerIds.ToList(),
				Scores = r.Scores.Select(sc => new ScoreDto
				{
					PlayerId = sc.PlayerId,
					DisplayName = sc.DisplayName,
					Circles = sc.Circles,
					Infinites = sc.Infinites,
					Laws = sc.Laws,
					VpTokens = sc.VpTokens,
					Magic = sc.Magic,
					Sets = sc.Sets,
					AttackPenalty = sc.AttackPenalty,
					Notes = sc.Notes.ToList()
				}).ToList()
			}
			: null
	};

	static GameState ToState(GameStateDto dto)
	{
		var state = new GameState
		{
			Seed = dto.Seed,
			Mode = dto.Mode,
			HumanPlayerCount = dto.HumanPlayerCount,
			VirtualOpponentCount = dto.VirtualOpponentCount,
			Turn = dto.Turn,
			Phase = dto.Phase,
			AuctionRound = dto.AuctionRound,
			AuctionSubPhase = dto.AuctionSubPhase,
			DevelopmentRound = dto.DevelopmentRound,
			DevelopmentSubPhase = dto.DevelopmentSubPhase,
			NextInstanceId = dto.NextInstanceId,
			NextTurnIsLast = dto.NextTurnIsLast,
			FinalTurnInProgress = dto.FinalTurnInProgress
		};

		dto.Reserve.ApplyTo(state.Reserve);
		dto.VirtualBox.ApplyTo(state.VirtualBox);
		state.BigDeck.AddRange(dto.BigDeck);
		state.SmallDeck.AddRange(dto.SmallDeck);
		state.LawDeck.AddRange(dto.LawDeck);
		state.Discard.AddRange(dto.Discard);

		foreach (var slotDto in dto.AuctionSlots)
		{
			var slot = state.Slot(slotDto.Color);
			slot.CardAtBase = slotDto.CardAtBase;
			slot.CardAtTip = slotDto.CardAtTip;
		}

		foreach (var (key, bid) in dto.SealedBids)
			state.SealedBids[int.Parse(key)] = bid.ToBid();

		foreach (var c in dto.PendingCardChoices)
		{
			state.PendingCardChoices.Add(new PendingCardChoice
			{
				PlayerId = c.PlayerId,
				Color = c.Color,
				Options = c.Options.ToList()
			});
		}

		foreach (var (key, action) in dto.SealedDevActions)
			state.SealedDevActions[int.Parse(key)] = action.ToAction();

		if (dto.PendingLevel5 is { } l5)
		{
			state.PendingLevel5 = new PendingLevel5Choice
			{
				PlayerId = l5.PlayerId,
				PyramidCardInstanceId = l5.PyramidCardInstanceId,
				CharacterDefinitionId = l5.CharacterDefinitionId
			};
		}

		if (dto.PendingLaw is { } law)
		{
			state.PendingLaw = new PendingLawResolution
			{
				PlayerId = law.PlayerId,
				LawInstanceId = law.LawInstanceId,
				LawDefinitionId = law.LawDefinitionId,
				Kind = law.Kind,
				OptionLabels = law.OptionLabels.ToList(),
				DrawnInstanceId = law.DrawnInstanceId,
				AwaitingInfiniteColor = law.AwaitingInfiniteColor
			};
		}

		if (dto.PendingRewardChoice is { } rc)
		{
			state.PendingRewardChoice = new PendingRewardChoice
			{
				PlayerId = rc.PlayerId,
				HostInstanceId = rc.HostInstanceId,
				Options = Array.Empty<Reward>(),
				OptionLabels = rc.OptionLabels.ToList()
			};
		}

		if (dto.PendingDeckDraw is { } dd)
		{
			state.PendingDeckDraw = new PendingDeckDraw
			{
				PlayerId = dd.PlayerId,
				Remaining = dd.Remaining
			};
		}

		if (dto.PendingPassGems is { } pg)
		{
			state.PendingPassGems = new PendingPassGems
			{
				PlayerId = pg.PlayerId,
				Amount = pg.Amount,
				Picked = pg.Picked.ToList()
			};
		}

		if (dto.PendingTokenSwap is { } ts)
		{
			state.PendingTokenSwap = new PendingTokenSwap
			{
				PlayerId = ts.PlayerId,
				LawInstanceId = ts.LawInstanceId,
				OwnCardInstanceId = ts.OwnCardInstanceId,
				OwnToken = ts.OwnToken,
				OtherPlayerId = ts.OtherPlayerId,
				OtherCardInstanceId = ts.OtherCardInstanceId,
				OtherToken = ts.OtherToken
			};
			state.PendingTokenSwap.Payment.AddRange(ts.Payment);
		}

		foreach (var d in dto.DeferredDevPlays)
		{
			if (d.Play.ToAction() is PlayDevelopmentAction play)
				state.DeferredDevPlays.Add((d.PlayerId, play));
		}

		state.DeferredPassers.AddRange(dto.DeferredPassers);

		foreach (var p in dto.Players)
			state.Players.Add(p.ToPlayer());

		if (dto.Result is { } r)
		{
			var scores = r.Scores.Select(sc =>
			{
				var breakdown = new ScoreBreakdown
				{
					PlayerId = sc.PlayerId,
					DisplayName = sc.DisplayName,
					Circles = sc.Circles,
					Infinites = sc.Infinites,
					Laws = sc.Laws,
					VpTokens = sc.VpTokens,
					Magic = sc.Magic,
					Sets = sc.Sets,
					AttackPenalty = sc.AttackPenalty
				};
				breakdown.Notes.AddRange(sc.Notes);
				return breakdown;
			}).ToList();

			state.Result = new MatchResult
			{
				Scores = scores,
				WinnerIds = r.WinnerIds.ToList()
			};
		}

		return state;
	}

	sealed class GameStateDto
	{
		public int Seed { get; set; }
		public GameMode Mode { get; set; }
		public int HumanPlayerCount { get; set; }
		public int VirtualOpponentCount { get; set; }
		public int Turn { get; set; }
		public TurnPhase Phase { get; set; }
		public AuctionRound AuctionRound { get; set; }
		public AuctionSubPhase AuctionSubPhase { get; set; }
		public int DevelopmentRound { get; set; }
		public DevelopmentSubPhase DevelopmentSubPhase { get; set; }
		public int NextInstanceId { get; set; }
		public bool NextTurnIsLast { get; set; }
		public bool FinalTurnInProgress { get; set; }
		public WalletDto Reserve { get; set; } = new();
		public WalletDto VirtualBox { get; set; } = new();
		public List<int> BigDeck { get; set; } = new();
		public List<int> SmallDeck { get; set; } = new();
		public List<int> LawDeck { get; set; } = new();
		public List<int> Discard { get; set; } = new();
		public List<AuctionSlotDto> AuctionSlots { get; set; } = new();
		public Dictionary<string, BidDto> SealedBids { get; set; } = new();
		public List<PendingCardChoiceDto> PendingCardChoices { get; set; } = new();
		public Dictionary<string, DevActionDto> SealedDevActions { get; set; } = new();
		public PendingLevel5Dto? PendingLevel5 { get; set; }
		public PendingLawDto? PendingLaw { get; set; }
		public PendingRewardDto? PendingRewardChoice { get; set; }
		public PendingDeckDrawDto? PendingDeckDraw { get; set; }
		public PendingPassGemsDto? PendingPassGems { get; set; }
		public PendingTokenSwapDto? PendingTokenSwap { get; set; }
		public List<DeferredPlayDto> DeferredDevPlays { get; set; } = new();
		public List<int> DeferredPassers { get; set; } = new();
		public List<PlayerDto> Players { get; set; } = new();
		public MatchResultDto? Result { get; set; }
	}

	sealed class WalletDto
	{
		public int Blue { get; set; }
		public int Red { get; set; }
		public int Green { get; set; }
		public int Yellow { get; set; }
		public int AttackTokens { get; set; }

		public static WalletDto From(GemWallet w) => new()
		{
			Blue = w[GemColor.Blue],
			Red = w[GemColor.Red],
			Green = w[GemColor.Green],
			Yellow = w[GemColor.Yellow],
			AttackTokens = w.AttackTokens
		};

		public void ApplyTo(GemWallet w)
		{
			w[GemColor.Blue] = Blue;
			w[GemColor.Red] = Red;
			w[GemColor.Green] = Green;
			w[GemColor.Yellow] = Yellow;
			w.AttackTokens = AttackTokens;
		}
	}

	sealed class AuctionSlotDto
	{
		public GemColor Color { get; set; }
		public int? CardAtBase { get; set; }
		public int? CardAtTip { get; set; }
	}

	sealed class BidDto
	{
		public string Type { get; set; } = "";
		public GemColor? Color { get; set; }
		public int? PreferredCharacterId { get; set; }

		public static BidDto From(AuctionBid bid) => bid switch
		{
			GemAuctionBid g => new BidDto { Type = "gem", Color = g.Color },
			AttackAuctionBid a => new BidDto
			{
				Type = "attack",
				PreferredCharacterId = a.PreferredCharacterId
			},
			PassAuctionBid => new BidDto { Type = "pass" },
			_ => throw new NotSupportedException(bid.GetType().Name)
		};

		public AuctionBid ToBid() => Type switch
		{
			"gem" => new GemAuctionBid(Color ?? GemColor.Blue),
			"attack" => new AttackAuctionBid(PreferredCharacterId),
			"pass" => new PassAuctionBid(),
			_ => throw new JsonException($"Unknown bid type: {Type}")
		};
	}

	sealed class DevActionDto
	{
		public string Type { get; set; } = "";
		public int HandIndex { get; set; }
		public int Level { get; set; }
		public int Index { get; set; }
		public bool UseInfinites { get; set; } = true;
		public int? LawTargetInstanceId { get; set; }
		public List<int>? ExtraHandIndices { get; set; }

		public static DevActionDto From(DevelopmentAction action) => action switch
		{
			PlayDevelopmentAction p => new DevActionDto
			{
				Type = "play",
				HandIndex = p.HandIndex,
				Level = p.Level,
				Index = p.Index,
				UseInfinites = p.UseInfinites,
				LawTargetInstanceId = p.LawTargetInstanceId,
				ExtraHandIndices = p.ExtraHandIndices?.ToList()
			},
			DiscardDevelopmentAction d => new DevActionDto
			{
				Type = "discard",
				HandIndex = d.HandIndex
			},
			PassDevelopmentAction => new DevActionDto { Type = "pass" },
			_ => throw new NotSupportedException(action.GetType().Name)
		};

		public DevelopmentAction ToAction() => Type switch
		{
			"play" => new PlayDevelopmentAction(
				HandIndex, Level, Index, UseInfinites, LawTargetInstanceId, ExtraHandIndices),
			"discard" => new DiscardDevelopmentAction(HandIndex),
			"pass" => new PassDevelopmentAction(),
			_ => throw new JsonException($"Unknown dev action: {Type}")
		};
	}

	sealed class PendingCardChoiceDto
	{
		public int PlayerId { get; set; }
		public GemColor Color { get; set; }
		public List<int> Options { get; set; } = new();
	}

	sealed class PendingLevel5Dto
	{
		public int PlayerId { get; set; }
		public int PyramidCardInstanceId { get; set; }
		public int CharacterDefinitionId { get; set; }
	}

	sealed class PendingLawDto
	{
		public int PlayerId { get; set; }
		public int LawInstanceId { get; set; }
		public int LawDefinitionId { get; set; }
		public LawPromptKind Kind { get; set; }
		public List<string> OptionLabels { get; set; } = new();
		public int? DrawnInstanceId { get; set; }
		public bool AwaitingInfiniteColor { get; set; }
	}

	sealed class PendingRewardDto
	{
		public int PlayerId { get; set; }
		public int HostInstanceId { get; set; }
		public List<string> OptionLabels { get; set; } = new();
	}

	sealed class PendingDeckDrawDto
	{
		public int PlayerId { get; set; }
		public int Remaining { get; set; }
	}

	sealed class PendingPassGemsDto
	{
		public int PlayerId { get; set; }
		public int Amount { get; set; }
		public List<GemColor> Picked { get; set; } = new();
	}

	sealed class PendingTokenSwapDto
	{
		public int PlayerId { get; set; }
		public int LawInstanceId { get; set; }
		public int? OwnCardInstanceId { get; set; }
		public TokenKind? OwnToken { get; set; }
		public int? OtherPlayerId { get; set; }
		public int? OtherCardInstanceId { get; set; }
		public TokenKind? OtherToken { get; set; }
		public List<GemColor> Payment { get; set; } = new();
	}

	sealed class DeferredPlayDto
	{
		public int PlayerId { get; set; }
		public DevActionDto Play { get; set; } = new();
	}

	sealed class PlayerDto
	{
		public int PlayerId { get; set; }
		public string DisplayName { get; set; } = "";
		public SessionRole Role { get; set; }
		public WalletDto Screen { get; set; } = new();
		public List<CardDto> Hand { get; set; } = new();
		public List<PyramidRowDto> PyramidRows { get; set; } = new();
		public bool HasPassedAuction { get; set; }
		public bool HasPassedDevelopment { get; set; }
		public bool AcquiredAuctionCardThisTurn { get; set; }
		public bool ActedThisDevelopmentRound { get; set; }
		public bool SkipNextAuction { get; set; }
		public bool HasFinishedRecolor { get; set; }

		public static PlayerDto From(PlayerState p) => new()
		{
			PlayerId = p.PlayerId,
			DisplayName = p.DisplayName,
			Role = p.Role,
			Screen = WalletDto.From(p.Screen),
			Hand = p.Hand.Select(CardDto.From).ToList(),
			PyramidRows = p.Pyramid.Rows.Select(kv => new PyramidRowDto
			{
				Level = kv.Key,
				Cards = kv.Value.Select(PyramidCardDto.From).ToList()
			}).ToList(),
			HasPassedAuction = p.HasPassedAuction,
			HasPassedDevelopment = p.HasPassedDevelopment,
			AcquiredAuctionCardThisTurn = p.AcquiredAuctionCardThisTurn,
			ActedThisDevelopmentRound = p.ActedThisDevelopmentRound,
			SkipNextAuction = p.SkipNextAuction,
			HasFinishedRecolor = p.HasFinishedRecolor
		};

		public PlayerState ToPlayer()
		{
			var p = new PlayerState
			{
				PlayerId = PlayerId,
				DisplayName = DisplayName,
				Role = Role,
				HasPassedAuction = HasPassedAuction,
				HasPassedDevelopment = HasPassedDevelopment,
				AcquiredAuctionCardThisTurn = AcquiredAuctionCardThisTurn,
				ActedThisDevelopmentRound = ActedThisDevelopmentRound,
				SkipNextAuction = SkipNextAuction,
				HasFinishedRecolor = HasFinishedRecolor
			};
			Screen.ApplyTo(p.Screen);
			foreach (var c in Hand)
				p.Hand.Add(c.ToCard());
			foreach (var row in PyramidRows)
				p.Pyramid.Rows[row.Level] = row.Cards.Select(c => c.ToPyramidCard()).ToList();
			return p;
		}
	}

	sealed class CardDto
	{
		public int InstanceId { get; set; }
		public CardKind Kind { get; set; }
		public int DefinitionId { get; set; }

		public static CardDto From(CardInstance c) => new()
		{
			InstanceId = c.InstanceId,
			Kind = c.Kind,
			DefinitionId = c.DefinitionId
		};

		public CardInstance ToCard() => new()
		{
			InstanceId = InstanceId,
			Kind = Kind,
			DefinitionId = DefinitionId
		};
	}

	sealed class PyramidRowDto
	{
		public int Level { get; set; }
		public List<PyramidCardDto> Cards { get; set; } = new();
	}

	sealed class PyramidCardDto
	{
		public CardDto Card { get; set; } = new();
		public int Level { get; set; }
		public int Index { get; set; }
		public int VictoryPoints { get; set; }
		public int Science { get; set; }
		public int Magic { get; set; }
		public int Defense { get; set; }
		public GemColor? InfiniteGem { get; set; }
		public bool InfiniteUsedThisTurn { get; set; }
		public int BonusMagic { get; set; }
		public List<BonusCircleDto> BonusCircles { get; set; } = new();
		public List<CardDto> TuckedCards { get; set; } = new();
		public int ParkedGems { get; set; }
		public Dictionary<string, GemColor> SectorOverrides { get; set; } = new();

		public static PyramidCardDto From(PyramidCard c) => new()
		{
			Card = CardDto.From(c.Card),
			Level = c.Level,
			Index = c.Index,
			VictoryPoints = c.VictoryPoints,
			Science = c.Science,
			Magic = c.Magic,
			Defense = c.Defense,
			InfiniteGem = c.InfiniteGem,
			InfiniteUsedThisTurn = c.InfiniteUsedThisTurn,
			BonusMagic = c.BonusMagic,
			BonusCircles = c.BonusCircles.Select(b => new BonusCircleDto
			{
				Color = b.Color,
				Amount = b.Amount
			}).ToList(),
			TuckedCards = c.TuckedCards.Select(CardDto.From).ToList(),
			ParkedGems = c.ParkedGems,
			SectorOverrides = new Dictionary<string, GemColor>(c.SectorOverrides)
		};

		public PyramidCard ToPyramidCard()
		{
			var card = new PyramidCard
			{
				Card = Card.ToCard(),
				Level = Level,
				Index = Index,
				VictoryPoints = VictoryPoints,
				Science = Science,
				Magic = Magic,
				Defense = Defense,
				InfiniteGem = InfiniteGem,
				InfiniteUsedThisTurn = InfiniteUsedThisTurn,
				BonusMagic = BonusMagic,
				ParkedGems = ParkedGems
			};
			foreach (var b in BonusCircles)
				card.BonusCircles.Add((b.Color, b.Amount));
			foreach (var t in TuckedCards)
				card.TuckedCards.Add(t.ToCard());
			foreach (var (k, v) in SectorOverrides)
				card.SectorOverrides[k] = v;
			return card;
		}
	}

	sealed class BonusCircleDto
	{
		public GemColor Color { get; set; }
		public int Amount { get; set; }
	}

	sealed class MatchResultDto
	{
		public List<int> WinnerIds { get; set; } = new();
		public List<ScoreDto> Scores { get; set; } = new();
	}

	sealed class ScoreDto
	{
		public int PlayerId { get; set; }
		public string DisplayName { get; set; } = "";
		public int Circles { get; set; }
		public int Infinites { get; set; }
		public int Laws { get; set; }
		public int VpTokens { get; set; }
		public int Magic { get; set; }
		public int Sets { get; set; }
		public int AttackPenalty { get; set; }
		public List<string> Notes { get; set; } = new();
	}
}
