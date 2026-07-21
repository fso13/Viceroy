using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Models;
using Namestnik.Core.Scoring;

namespace Namestnik.Core;

/// <summary>
/// Pure rules engine. No Godot Node dependencies — safe to unit-test and run on host only.
/// </summary>
public sealed partial class GameEngine
{
	readonly CardDatabase _db;
	readonly Random _rng;
	readonly Stack<GameState> _undoStack = new();

	public GameState State { get; private set; }
	public bool CanUndo => State.Mode == GameMode.Solo && _undoStack.Count > 0;
	public event Action<GameEvent>? EventRaised;

	public GameEngine(CardDatabase db, GameState state)
	{
		_db = db;
		State = state;
		_rng = new Random(state.Seed);
	}

	/// <summary>Solo only: snapshot before a player-facing command.</summary>
	public void PushUndoCheckpoint()
	{
		if (State.Mode != GameMode.Solo)
			return;
		_undoStack.Push(GameStateClone.Clone(State));
		const int max = 40;
		if (_undoStack.Count <= max)
			return;
		var newest = _undoStack.ToArray().Take(max).Reverse();
		_undoStack.Clear();
		foreach (var s in newest)
			_undoStack.Push(s);
	}

	public bool TryUndo(out string error)
	{
		error = string.Empty;
		if (State.Mode != GameMode.Solo)
		{
			error = "Отмена только в соло.";
			return false;
		}

		if (_undoStack.Count == 0)
		{
			error = "Нечего отменять.";
			return false;
		}

		State = _undoStack.Pop();
		Raise(new LogEvent("↩ Отмена последнего действия"));
		Raise(new PhaseChangedEvent(State.Phase, State.Turn));
		return true;
	}

	public static GameEngine CreateNew(
		CardDatabase db,
		GameMode mode,
		int humanCount,
		int virtualCount,
		int? seed = null)
	{
		var actualSeed = seed ?? Random.Shared.Next();
		var state = new GameState
		{
			Seed = actualSeed,
			Mode = mode,
			HumanPlayerCount = humanCount,
			VirtualOpponentCount = virtualCount
		};

		var engine = new GameEngine(db, state);
		engine.SetupMatch();
		return engine;
	}

	/// <summary>
	/// Replace authoritative state (network snapshot / restore). Clears undo history.
	/// Client mirrors use this; host should not need it during normal play.
	/// </summary>
	public void LoadState(GameState state)
	{
		_undoStack.Clear();
		State = state;
		Raise(new PhaseChangedEvent(State.Phase, State.Turn));
	}

	void Raise(GameEvent e) => EventRaised?.Invoke(e);

	public void SetupMatch()
	{
		InitPlayers();
		InitReserveAndBox();
		InitDecksAndStartingHands();
		DealAuctionRow();

		State.Phase = TurnPhase.Auction;
		State.AuctionRound = AuctionRound.First;
		State.AuctionSubPhase = AuctionSubPhase.CollectingBids;
		Raise(new MatchStartedEvent(State.Seed, State.Mode, State.Players.Count));
		Raise(new PhaseChangedEvent(State.Phase, State.Turn));
		Raise(new AuctionRoundStartedEvent(1));
		Raise(new LogEvent($"Ход {State.Turn}: аукцион, круг 1 — сделайте ставку"));
	}

	void InitPlayers()
	{
		var id = 0;
		for (var i = 0; i < State.HumanPlayerCount; i++)
		{
			State.Players.Add(new PlayerState
			{
				PlayerId = id++,
				DisplayName = State.HumanPlayerCount == 1 ? "Игрок" : $"Игрок {i + 1}",
				Role = SessionRole.LocalHuman
			});
		}

		for (var i = 0; i < State.VirtualOpponentCount; i++)
		{
			State.Players.Add(new PlayerState
			{
				PlayerId = id++,
				DisplayName = $"Виртуал {i + 1}",
				Role = SessionRole.VirtualOpponent
			});
		}
	}

	void InitReserveAndBox()
	{
		// Reserve sized by human players only (virtual uses the box).
		var humans = Math.Max(1, State.HumanPlayerCount);
		var perColorReserve = 4 * humans;
		foreach (GemColor color in Enum.GetValues<GemColor>())
		{
			State.Reserve[color] = perColorReserve;
			State.VirtualBox[color] = GameState.PhysicalGemsPerColor - perColorReserve;
		}
	}

	void InitDecksAndStartingHands()
	{
		var characterIds = _db.Characters.Keys.OrderBy(_ => _rng.Next()).ToList();
		var lawIds = _db.Laws.Keys.OrderBy(_ => _rng.Next()).ToList();

		var humans = State.Players.Where(p => p.Role != SessionRole.VirtualOpponent).ToList();
		foreach (var player in humans)
		{
			var dealt = characterIds.Take(4).ToList();
			characterIds.RemoveRange(0, Math.Min(4, characterIds.Count));
			if (dealt.Count == 0)
				continue;

			var starterId = dealt[0];
			var starterInstance = CreateInstance(CardKind.Character, starterId);
			var starterCard = new PyramidCard
			{
				Card = starterInstance,
				Level = 1,
				Index = 0
			};
			player.Pyramid.PlaceStarter(starterCard);
			var starterDef = _db.GetCharacter(starterId);
			Raise(new LogEvent($"{player.DisplayName} начинает с «{starterDef.Name}»"));
			var needsL5 = false;
			var draws = 0;
			Rewards.Apply(player, starterCard, starterDef.GetLevel(1).Reward, ref needsL5, ref draws,
				suppressChoicePrompt: true);
			if (draws > 0)
				Rewards.DrawCardsAuto(player, draws);

			if (dealt.Count > 1)
				player.Hand.Add(CreateInstance(CardKind.Character, dealt[1]));

			for (var i = 2; i < dealt.Count; i++)
				characterIds.Add(dealt[i]);
		}

		characterIds = characterIds.OrderBy(_ => _rng.Next()).ToList();
		const int bigSize = 48;
		State.BigDeck.AddRange(characterIds.Take(bigSize));
		State.SmallDeck.AddRange(characterIds.Skip(bigSize));

		foreach (var player in humans)
		{
			for (var i = 0; i < 3 && lawIds.Count > 0; i++)
			{
				var lawId = lawIds[0];
				lawIds.RemoveAt(0);
				player.Hand.Add(CreateInstance(CardKind.Law, lawId));
			}
		}

		State.LawDeck.AddRange(lawIds);
		GiveStartingGems(humans);
	}

	void GiveStartingGems(List<PlayerState> humans)
	{
		foreach (var player in humans)
		{
			var pool = new List<GemColor>();
			foreach (GemColor color in Enum.GetValues<GemColor>())
			{
				for (var i = 0; i < 2; i++)
				{
					if (State.Reserve.TrySpend(color))
						pool.Add(color);
				}
			}

			pool = pool.OrderBy(_ => _rng.Next()).ToList();
			for (var i = 0; i < 2 && pool.Count > 0; i++)
			{
				var returned = pool[^1];
				pool.RemoveAt(pool.Count - 1);
				State.Reserve.Add(returned);
			}

			foreach (var gem in pool)
				player.Screen.Add(gem);
		}
	}

	void DealAuctionRow()
	{
		foreach (var slot in State.AuctionSlots)
		{
			slot.CardAtTip = null;
			slot.CardAtBase = DrawBig();
		}
	}

	int? DrawBig()
	{
		if (State.BigDeck.Count == 0)
			return null;
		var id = State.BigDeck[0];
		State.BigDeck.RemoveAt(0);
		return id;
	}

	CardInstance CreateInstance(CardKind kind, int definitionId) => new()
	{
		InstanceId = State.NextInstanceId++,
		Kind = kind,
		DefinitionId = definitionId
	};

	public bool TryApply(GameCommand command, out string error)
	{
		error = string.Empty;
		try
		{
			switch (command)
			{
				case SubmitAuctionBidCommand submit:
					SubmitSealedBid(submit.PlayerId, submit.Bid);
					break;
				case PassAuctionCommand pass:
					SubmitSealedBid(pass.PlayerId, new PassAuctionBid());
					break;
				case BidGemCommand bid:
					SubmitSealedBid(bid.PlayerId, new GemAuctionBid(bid.Color));
					break;
				case BidAttackCommand atk:
					SubmitSealedBid(atk.PlayerId, new AttackAuctionBid(atk.PreferredCharacterId));
					break;
				case ChooseAuctionCardCommand choose:
					ApplyChooseAuctionCard(choose);
					break;
				case PassDevelopmentCommand devPass:
					SubmitDevelopmentAction(devPass.PlayerId, new PassDevelopmentAction());
					break;
				case PlayCardCommand play:
					SubmitDevelopmentAction(play.PlayerId,
						new PlayDevelopmentAction(
							play.HandIndex,
							play.PyramidLevel,
							play.SlotHint,
							LawTargetInstanceId: play.LawTargetInstanceId,
							ExtraHandIndices: play.ExtraHandIndices));
					break;
				case DiscardForGemsCommand discard:
					SubmitDevelopmentAction(discard.PlayerId, new DiscardDevelopmentAction(discard.HandIndex));
					break;
				case ChooseLevel5RewardCommand level5:
					ApplyLevel5Choice(level5.PlayerId, level5.TakeFifteenVp);
					break;
				case ResolveLawCommand resolveLaw:
					ApplyResolveLaw(resolveLaw);
					break;
				case ChooseRewardCommand chooseReward:
					ApplyRewardChoice(chooseReward.PlayerId, chooseReward.OptionIndex);
					break;
				case ClaimPassGemsCommand claimGems:
					ApplyClaimPassGem(claimGems);
					break;
				case ResolveTokenSwapCommand swap:
					ApplyTokenSwap(swap);
					break;
				case UndoCommand:
					error = "Undo обрабатывается сессией.";
					return false;
				default:
					error = $"Command not implemented yet: {command.GetType().Name}";
					Raise(new ErrorEvent(command.PlayerId, error));
					return false;
			}

			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			Raise(new ErrorEvent(command.PlayerId, error));
			return false;
		}
	}

	void BeginNextTurnOrEnd()
	{
		// End after the final turn finishes, or after turn 12.
		if (State.FinalTurnInProgress || State.Turn >= GameState.MaxTurns)
		{
			FinalizeMatch();
			return;
		}

		State.Turn++;
		if (State.NextTurnIsLast)
		{
			State.NextTurnIsLast = false;
			State.FinalTurnInProgress = true;
			Raise(new LogEvent("=== Финальный ход ==="));
		}

		foreach (var p in State.Players)
		{
			p.HasPassedAuction = false;
			p.AcquiredAuctionCardThisTurn = false;
			p.HasPassedDevelopment = false;
			p.ActedThisDevelopmentRound = false;
			p.ResetInfiniteUses();
		}

		State.SealedBids.Clear();
		State.PendingCardChoices.Clear();
		State.SealedDevActions.Clear();
		State.PendingLevel5 = null;
		State.PendingLaw = null;
		State.PendingRewardChoice = null;
		State.PendingPassGems = null;
		State.PendingTokenSwap = null;
		State.DeferredDevPlays.Clear();
		State.DeferredPassers.Clear();
		State.Phase = TurnPhase.Auction;
		State.AuctionRound = AuctionRound.First;
		State.AuctionSubPhase = AuctionSubPhase.CollectingBids;

		foreach (var p in State.Players.Where(p => p.SkipNextAuction))
		{
			p.SkipNextAuction = false;
			p.HasPassedAuction = true;
			Raise(new LogEvent($"{p.DisplayName} пропускает аукцион (закон #69)"));
		}

		Raise(new PhaseChangedEvent(State.Phase, State.Turn));
		Raise(new AuctionRoundStartedEvent(1));
		Raise(new LogEvent($"Ход {State.Turn}: аукцион, круг 1"));

		if (!State.ActiveAuctionPlayers().Any())
			CompleteAuctionPhase();
	}

	void FinalizeMatch()
	{
		Raise(new LogEvent("=== Финальный подсчёт ==="));
		var scorer = new FinalScorer(_db);
		var result = scorer.ScoreMatch(State);
		State.Result = result;
		State.Phase = TurnPhase.GameOver;
		Raise(new PhaseChangedEvent(State.Phase, State.Turn));

		foreach (var score in result.Scores)
		{
			Raise(new LogEvent(score.ToString()));
			foreach (var note in score.Notes)
				Raise(new LogEvent($"  • {note}"));
		}

		if (result.IsDraw)
		{
			var names = string.Join(", ",
				result.WinnerIds.Select(id => State.GetPlayer(id).DisplayName));
			Raise(new LogEvent($"Ничья: {names}"));
		}
		else
		{
			var winner = State.GetPlayer(result.WinnerIds[0]);
			Raise(new LogEvent($"Победитель: {winner.DisplayName}"));
		}

		Raise(new MatchEndedEvent(result));
	}

	void EnsurePhase(TurnPhase phase)
	{
		if (State.Phase != phase)
			throw new InvalidOperationException($"Ожидалась фаза {phase}, сейчас {State.Phase}");
	}

	string CardName(int id) =>
		_db.Characters.TryGetValue(id, out var c) ? c.Name : $"#{id}";
}
