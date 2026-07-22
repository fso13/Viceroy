using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Laws;
using Namestnik.Core.Models;
using Namestnik.Core.Rewards;

namespace Namestnik.Core;

public sealed partial class GameEngine
{
	RewardApplicator Rewards => new(State, _db, Raise, CreateInstance, p => Laws.NotifyCardTaken(p));

	LawEffects Laws => new(State, _db, Raise, CreateInstance, Rewards);

	void SubmitDevelopmentAction(int playerId, DevelopmentAction action)
	{
		EnsurePhase(TurnPhase.Development);
		if (State.DevelopmentSubPhase != DevelopmentSubPhase.CollectingActions)
			throw new InvalidOperationException("Сейчас нельзя выбрать действие развития.");

		var player = State.GetPlayer(playerId);
		if (player.HasPassedDevelopment)
			throw new InvalidOperationException("Вы уже спасовали в этой фазе.");
		if (player.ActedThisDevelopmentRound || State.SealedDevActions.ContainsKey(playerId))
			throw new InvalidOperationException("Действие в этом раунде уже выбрано.");

		ValidateDevAction(player, action);
		State.SealedDevActions[playerId] = action;
		Raise(new LogEvent($"{player.DisplayName} выбрал действие развития (скрыто)"));
		TryResolveDevelopmentRound();
	}

	void ValidateDevAction(PlayerState player, DevelopmentAction action)
	{
		switch (action)
		{
			case PassDevelopmentAction:
				return;
			case DiscardDevelopmentAction discard:
				if (discard.HandIndex < 0 || discard.HandIndex >= player.Hand.Count)
					throw new InvalidOperationException("Неверный индекс карты в руке.");
				return;
			case PlayDevelopmentAction play:
				if (play.HandIndex < 0 || play.HandIndex >= player.Hand.Count)
					throw new InvalidOperationException("Неверный индекс карты в руке.");
				var card = player.Hand[play.HandIndex];
				if (card.Kind == CardKind.Law)
					ValidateLawPlay(player, card, play);
				else
				{
					if (play.Level is < 1 or > GameState.MaxPyramidLevels)
						throw new InvalidOperationException("Уровень 1..5.");
					if (!player.Pyramid.LegalPlacements().Contains((play.Level, play.Index)))
						throw new InvalidOperationException("Недопустимое место в пирамиде.");
					var def = _db.GetCharacter(card.DefinitionId);
					if (!PlayCostHelper.CanAfford(player, def, play.Level, play.UseInfinites))
						throw new InvalidOperationException("Не хватает камней для оплаты.");
				}

				return;
			default:
				throw new InvalidOperationException("Неизвестное действие.");
		}
	}

	void TryResolveDevelopmentRound()
	{
		var need = State.Players
			.Where(p => p.Role != SessionRole.VirtualOpponent && !p.HasPassedDevelopment)
			.Where(p => !p.ActedThisDevelopmentRound)
			.Select(p => p.PlayerId)
			.ToList();

		if (need.Any(id => !State.SealedDevActions.ContainsKey(id)))
			return;

		ResolveDevelopmentActions();
	}

	void ResolveDevelopmentActions()
	{
		var actions = State.SealedDevActions.ToList();
		State.SealedDevActions.Clear();
		Raise(new LogEvent($"=== Развитие, раунд {State.DevelopmentRound}: розыгрыш ==="));

		// Passes first (no order dependency).
		foreach (var (playerId, action) in actions.Where(a => a.Value is PassDevelopmentAction))
		{
			var player = State.GetPlayer(playerId);
			player.HasPassedDevelopment = true;
			player.ActedThisDevelopmentRound = true;
			Raise(new LogEvent($"{player.DisplayName} пасует развитие"));
		}

		// Discards — order by priority.
		foreach (var playerId in OrderByPriority(actions.Where(a => a.Value is DiscardDevelopmentAction).Select(a => a.Key)))
		{
			var discard = (DiscardDevelopmentAction)actions.First(a => a.Key == playerId).Value;
			ApplyDiscardForGems(State.GetPlayer(playerId), discard.HandIndex);
		}

		// Plays — ascending card order id among chosen cards.
		var plays = actions
			.Where(a => a.Value is PlayDevelopmentAction)
			.Select(a =>
			{
				var play = (PlayDevelopmentAction)a.Value;
				var player = State.GetPlayer(a.Key);
				var card = player.Hand[play.HandIndex];
				return (PlayerId: a.Key, Play: play, Order: card.OrderId);
			})
			.OrderBy(x => x.Order)
			.ThenBy(x => x.PlayerId)
			.ToList();

		ProcessPlayQueue(plays.Select(e => (e.PlayerId, e.Play)).ToList());
	}

	void ProcessPlayQueue(List<(int PlayerId, PlayDevelopmentAction Play)> queue)
	{
		while (queue.Count > 0)
		{
			var entry = queue[0];
			queue.RemoveAt(0);
			ApplyPlayCard(State.GetPlayer(entry.PlayerId), entry.Play);
			if (HasBlockingPrompt())
			{
				State.DeferredDevPlays.Clear();
				State.DeferredDevPlays.AddRange(queue);
				EnterPromptSubPhase();
				return;
			}
		}

		AdvanceDevelopmentAfterResolve();
	}

	bool HasBlockingPrompt() =>
		State.PendingLaw is not null
		|| State.PendingLevel5 is not null
		|| State.PendingRewardChoice is not null
		|| State.PendingDeckDraw is not null
		|| State.PendingTokenSwap is not null;

	void EnterPromptSubPhase()
	{
		if (State.PendingLaw is not null || State.PendingTokenSwap is not null)
		{
			State.DevelopmentSubPhase = DevelopmentSubPhase.ResolvingLaw;
			return;
		}

		if (State.PendingRewardChoice is not null)
		{
			State.DevelopmentSubPhase = DevelopmentSubPhase.ChoosingReward;
			var pending = State.PendingRewardChoice;
			Raise(new RewardChoiceRequiredEvent(pending.PlayerId, pending.OptionLabels));
			Raise(new LogEvent("Выберите награду карты"));
			return;
		}

		if (State.PendingLevel5 is not null)
		{
			State.DevelopmentSubPhase = DevelopmentSubPhase.ChoosingLevel5Reward;
			Raise(new Level5ChoiceRequiredEvent(
				State.PendingLevel5.PlayerId,
				State.PendingLevel5.CharacterDefinitionId));
			Raise(new LogEvent("Выберите награду 5-го уровня: пакет 1–3 или 15 VP"));
			return;
		}

		if (State.PendingDeckDraw is not null)
		{
			State.DevelopmentSubPhase = DevelopmentSubPhase.ChoosingDeckDraw;
			var pending = State.PendingDeckDraw;
			Raise(new DeckDrawRequiredEvent(pending.PlayerId, pending.Remaining));
			Raise(new LogEvent("Выберите колоду: законов или малую"));
		}
	}

	void ContinueAfterPrompt()
	{
		if (HasBlockingPrompt())
		{
			EnterPromptSubPhase();
			return;
		}

		if (State.DeferredDevPlays.Count > 0)
		{
			var rest = State.DeferredDevPlays.ToList();
			State.DeferredDevPlays.Clear();
			ProcessPlayQueue(rest);
			return;
		}

		AdvanceDevelopmentAfterResolve();
	}

	void ValidateLawPlay(PlayerState player, CardInstance card, PlayDevelopmentAction play)
	{
		var lawId = card.DefinitionId;
		if (lawId is LawIds.Replace or LawIds.TuckUnderCharacter)
		{
			if (play.LawTargetInstanceId is not int targetId)
				throw new InvalidOperationException("Нужна целевая карта в пирамиде.");
			var target = player.Pyramid.FindByInstanceId(targetId)
				?? throw new InvalidOperationException("Цель не найдена.");
			if (lawId == LawIds.TuckUnderCharacter && target.Card.Kind != CardKind.Character)
				throw new InvalidOperationException("Закон #66 только под персонажа.");
			return;
		}

		if (play.Level is < 1 or > GameState.MaxPyramidLevels)
			throw new InvalidOperationException("Уровень 1..5.");
		if (play.Level == 5)
			throw new InvalidOperationException("Закон нельзя на 5-й уровень.");
		if (!player.Pyramid.LegalPlacements().Contains((play.Level, play.Index)))
			throw new InvalidOperationException("Недопустимое место в пирамиде.");

		if (lawId == LawIds.TuckFreeCard)
		{
			if (play.LawTargetInstanceId is not int freeId)
				throw new InvalidOperationException("Нужна свободная карта для подкладывания.");
			var free = player.Pyramid.FindByInstanceId(freeId)
				?? throw new InvalidOperationException("Свободная карта не найдена.");
			if (!player.Pyramid.IsFree(free))
				throw new InvalidOperationException("Карта не свободная.");
		}

		if (play.ExtraHandIndices is { } extras)
		{
			foreach (var idx in extras)
			{
				if (idx < 0 || idx >= player.Hand.Count || idx == play.HandIndex)
					throw new InvalidOperationException("Неверный индекс для подкладывания с руки.");
			}
		}
	}

	void AdvanceDevelopmentAfterResolve()
	{
		foreach (var p in State.Players)
			p.ActedThisDevelopmentRound = false;

		var humansLeft = State.Players
			.Where(p => p.Role != SessionRole.VirtualOpponent && !p.HasPassedDevelopment)
			.ToList();

		if (humansLeft.Count == 0 || State.DevelopmentRound >= GameState.MaxDevelopmentRounds)
		{
			State.DevelopmentSubPhase = DevelopmentSubPhase.Done;
			BeginNextTurnOrEnd();
			return;
		}

		State.DevelopmentRound++;
		State.DevelopmentSubPhase = DevelopmentSubPhase.CollectingActions;
		Raise(new LogEvent($"Развитие, раунд {State.DevelopmentRound}"));
	}

	void ApplyDiscardForGems(PlayerState player, int handIndex)
	{
		if (handIndex < 0 || handIndex >= player.Hand.Count)
			throw new InvalidOperationException("Нет такой карты.");

		var card = player.Hand[handIndex];
		player.Hand.RemoveAt(handIndex);
		State.Discard.Add(card.DefinitionId);
		player.ActedThisDevelopmentRound = true;
		Rewards.TakeAnyGems(player, 2);
		Raise(new LogEvent($"{player.DisplayName} сбрасывает карту #{card.DefinitionId} за 2 камня"));
	}

	void ApplyPlayCard(PlayerState player, PlayDevelopmentAction play)
	{
		if (play.HandIndex < 0 || play.HandIndex >= player.Hand.Count)
			throw new InvalidOperationException("Карта уже недоступна.");

		var card = player.Hand[play.HandIndex];
		// Adjust extra indices after removal of played card.
		var extras = play.ExtraHandIndices?
			.Select(i => i > play.HandIndex ? i - 1 : i)
			.Where(i => i != play.HandIndex)
			.Distinct()
			.OrderByDescending(i => i)
			.ToList();

		player.Hand.RemoveAt(play.HandIndex);
		player.ActedThisDevelopmentRound = true;

		if (card.Kind == CardKind.Character)
		{
			var placed = new PyramidCard
			{
				Card = card,
				Level = play.Level,
				Index = play.Index
			};
			var def = _db.GetCharacter(card.DefinitionId);
			PayForCharacter(player, def, play.Level, play.UseInfinites);
			player.Pyramid.Place(placed);
			TryGrantMonochromeCircle(player, placed);
			var circleNote = State.LastRewardSummary;
			var rewardDesc = play.Level == 5
				? "выбор награды 5-го уровня"
				: DescribeRewardShort(def.GetLevel(play.Level).Reward);
			GrantCharacterReward(player, placed, def, play.Level);
			if (!string.IsNullOrEmpty(circleNote) && circleNote.Contains("одноцветный круг", StringComparison.Ordinal))
			{
				State.LastRewardSummary = string.IsNullOrEmpty(State.LastRewardSummary)
					? circleNote
					: $"{State.LastRewardSummary}; {circleNote}";
			}

			Raise(new LogEvent($"{player.DisplayName} играет «{def.Name}» на ур.{play.Level} → {rewardDesc}"));
			return;
		}

		ApplyPlayLaw(player, card, play, extras);
	}

	void ApplyPlayLaw(
		PlayerState player,
		CardInstance card,
		PlayDevelopmentAction play,
		List<int>? extrasAfterRemove)
	{
		var law = _db.GetLaw(card.DefinitionId);
		Raise(new LogEvent($"{player.DisplayName} издаёт закон #{law.Id}"));
		Raise(new LogEvent($"Закон: {Truncate(law.Text, 120)}"));

		PyramidCard host;
		switch (card.DefinitionId)
		{
			case LawIds.Replace:
			{
				var target = player.Pyramid.FindByInstanceId(play.LawTargetInstanceId!.Value)!;
				var old = target.Card;
				player.Pyramid.ReplaceCard(target, card);
				player.Hand.Add(old);
				host = target;
				Raise(new LogEvent($"#{LawIds.Replace}: замена «{CardName(old.DefinitionId)}» → закон, карта на руку"));
				break;
			}
			case LawIds.TuckUnderCharacter:
			{
				var target = player.Pyramid.FindByInstanceId(play.LawTargetInstanceId!.Value)!;
				target.TuckedCards.Add(card);
				LawEffects.DuplicateTokens(target);
				host = target;
				Raise(new LogEvent($"#{LawIds.TuckUnderCharacter}: подложен под «{CardName(target.Card.DefinitionId)}», жетоны ×2"));
				break;
			}
			default:
			{
				host = new PyramidCard
				{
					Card = card,
					Level = play.Level,
					Index = play.Index
				};
				player.Pyramid.Place(host);
				TryGrantMonochromeCircle(player, host);

				if (card.DefinitionId == LawIds.TuckFromHand && extrasAfterRemove is { Count: > 0 })
				{
					foreach (var idx in extrasAfterRemove)
					{
						if (idx < 0 || idx >= player.Hand.Count)
							continue;
						host.TuckedCards.Add(player.Hand[idx]);
						player.Hand.RemoveAt(idx);
					}
				}

				if (card.DefinitionId == LawIds.TuckFreeCard)
				{
					var free = player.Pyramid.FindByInstanceId(play.LawTargetInstanceId!.Value)!;
					Laws.TransferTokensSafe(free, host);
					var freeInst = free.Card;
					player.Pyramid.RemoveFreeCard(free);
					host.TuckedCards.Add(freeInst);
					Raise(new LogEvent($"#{LawIds.TuckFreeCard}: подложена свободная карта #{freeInst.DefinitionId}"));
				}

				break;
			}
		}

		if (Laws.BeginAfterPlay(player, host, card.DefinitionId))
			State.DevelopmentSubPhase = DevelopmentSubPhase.ResolvingLaw;
	}

	void ApplyResolveLaw(ResolveLawCommand cmd)
	{
		EnsurePhase(TurnPhase.Development);
		if (State.DevelopmentSubPhase != DevelopmentSubPhase.ResolvingLaw && State.PendingLaw is null)
			throw new InvalidOperationException("Сейчас нет эффекта закона.");

		var player = State.GetPlayer(cmd.PlayerId);
		Laws.ResolvePrompt(player, new ResolveLawCommandData
		{
			OptionIndex = cmd.OptionIndex,
			GemColor = cmd.GemColor,
			HandIndices = cmd.HandIndices,
			CharacterDefinitionId = cmd.CharacterDefinitionId
		});

		ContinueAfterPrompt();
	}

	void PayForCharacter(PlayerState player, CharacterCard def, int level, bool useInfinites)
	{
		var cost = PlayCostHelper.BaseCost(def, level);
		if (useInfinites)
			PlayCostHelper.ApplyInfinites(player, cost, markUsed: true);

		player.Screen.Spend(cost);
		foreach (var (color, amount) in cost)
		{
			if (amount > 0)
				State.Reserve.Add(color, amount);
		}
	}

	void TryGrantMonochromeCircle(PlayerState player, PyramidCard placed)
	{
		var supports = player.Pyramid.SupportsOf(placed);
		if (supports is null)
			return;

		var (left, right) = supports.Value;
		GemColor[] sectors =
		[
			SectorOf(left, "tr"),
			SectorOf(right, "tl"),
			SectorOf(placed, "bl"),
			SectorOf(placed, "br")
		];
		if (sectors.Distinct().Count() != 1)
			return;

		var color = sectors[0];
		if (State.Reserve.TrySpend(color))
		{
			player.Screen.Add(color);
			State.LastRewardSummary = $"{player.DisplayName}: одноцветный круг → +1 {ColorRu(color)}";
			Raise(new LogEvent($"{player.DisplayName}: одноцветный круг {ColorRu(color)} → +1 камень за ширму"));
		}
		else
		{
			State.LastRewardSummary = $"{player.DisplayName}: одноцветный круг {ColorRu(color)}, но в резерве пусто";
			Raise(new LogEvent(State.LastRewardSummary));
		}
	}

	GemColor SectorOf(PyramidCard card, string corner)
	{
		var printed = GetSectors(card.Card);
		return card.EffectiveSector(printed, corner);
	}

	SectorColors GetSectors(CardInstance card) =>
		card.Kind == CardKind.Character
			? _db.GetCharacter(card.DefinitionId).Sectors
			: _db.GetLaw(card.DefinitionId).Sectors;

	void GrantCharacterReward(PlayerState player, PyramidCard host, CharacterCard def, int level)
	{
		var needsLevel5 = false;
		var pendingDraws = 0;

		if (level == 5)
		{
			State.PendingLevel5 = new PendingLevel5Choice
			{
				PlayerId = player.PlayerId,
				PyramidCardInstanceId = host.Card.InstanceId,
				CharacterDefinitionId = def.Id
			};
			needsLevel5 = true;
			State.LastRewardSummary = $"{player.DisplayName}: выберите награду 5-го уровня";
			return;
		}

		var reward = def.GetLevel(level).Reward;
		State.LastRewardSummary = $"{player.DisplayName} ← {DescribeRewardShort(reward)} (ур.{level})";
		Rewards.Apply(player, host, reward, ref needsLevel5, ref pendingDraws);
		if (State.PendingRewardChoice is not null)
		{
			State.LastRewardSummary = $"{player.DisplayName}: выберите вариант награды";
			return;
		}

		if (pendingDraws > 0)
			Rewards.RequestCardDraws(player, pendingDraws);
	}

	void ApplyLevel5Choice(int playerId, bool takeFifteenVp)
	{
		EnsurePhase(TurnPhase.Development);
		var pending = State.PendingLevel5
			?? throw new InvalidOperationException("Нет выбора 5-го уровня.");
		if (pending.PlayerId != playerId)
			throw new InvalidOperationException("Выбор не ваш.");

		var player = State.GetPlayer(playerId);
		var host = player.Pyramid.AllCards.First(c => c.Card.InstanceId == pending.PyramidCardInstanceId);
		var def = _db.GetCharacter(pending.CharacterDefinitionId);
		var needsLevel5 = false;
		var pendingDraws = 0;

		if (takeFifteenVp)
		{
			host.VictoryPoints += 15;
			Raise(new LogEvent($"{player.DisplayName}: 5-й уровень → 15 VP"));
		}
		else
		{
			for (var l = 1; l <= 3; l++)
				Rewards.Apply(player, host, def.GetLevel(l).Reward, ref needsLevel5, ref pendingDraws);
			if (pendingDraws > 0)
				Rewards.RequestCardDraws(player, pendingDraws);
			Raise(new LogEvent($"{player.DisplayName}: 5-й уровень → награды 1–3"));
		}

		State.PendingLevel5 = null;
		ContinueAfterPrompt();
	}

	void ApplyRewardChoice(int playerId, int optionIndex)
	{
		EnsurePhase(TurnPhase.Development);
		var pending = State.PendingRewardChoice
			?? throw new InvalidOperationException("Нет выбора награды.");
		if (pending.PlayerId != playerId)
			throw new InvalidOperationException("Выбор не ваш.");
		if (optionIndex < 0 || optionIndex >= pending.Options.Count)
			throw new InvalidOperationException("Неверный вариант награды.");

		var player = State.GetPlayer(playerId);
		var host = player.Pyramid.FindByInstanceId(pending.HostInstanceId)
			?? throw new InvalidOperationException("Карта награды не найдена.");
		var needsLevel5 = false;
		var pendingDraws = 0;
		Rewards.Apply(player, host, pending.Options[optionIndex], ref needsLevel5, ref pendingDraws,
			suppressChoicePrompt: true);
		if (pendingDraws > 0)
			Rewards.RequestCardDraws(player, pendingDraws);

		State.PendingRewardChoice = null;
		ContinueAfterPrompt();
	}

	void ApplyDeckDrawChoice(int playerId, bool fromLawDeck)
	{
		EnsurePhase(TurnPhase.Development);
		var pending = State.PendingDeckDraw
			?? throw new InvalidOperationException("Нет выбора колоды.");
		if (pending.PlayerId != playerId)
			throw new InvalidOperationException("Выбор колоды не ваш.");

		var player = State.GetPlayer(playerId);
		Rewards.ResolveDeckDrawChoice(player, fromLawDeck);
		ContinueAfterPrompt();
	}

	void ApplyTokenSwap(ResolveTokenSwapCommand cmd)
	{
		EnsurePhase(TurnPhase.Development);
		var pending = State.PendingTokenSwap
			?? throw new InvalidOperationException("Нет обмена (#67).");
		if (pending.PlayerId != cmd.PlayerId)
			throw new InvalidOperationException("Обмен не ваш.");

		if (cmd.Decline)
		{
			State.PendingTokenSwap = null;
			State.PendingLaw = null;
			Raise(new LogEvent($"{State.GetPlayer(cmd.PlayerId).DisplayName} отказывается от обмена (#67)"));
			ContinueAfterPrompt();
			return;
		}

		if (cmd.OwnCardInstanceId is int ownId)
			pending.OwnCardInstanceId = ownId;
		if (cmd.OwnToken is TokenKind ownTok)
			pending.OwnToken = ownTok;
		if (cmd.OtherPlayerId is int op)
			pending.OtherPlayerId = op;
		if (cmd.OtherCardInstanceId is int oc)
			pending.OtherCardInstanceId = oc;
		if (cmd.OtherToken is TokenKind ot)
			pending.OtherToken = ot;
		if (cmd.PayGem is GemColor pay)
		{
			if (pending.Payment.Count >= 3)
				throw new InvalidOperationException("Уже выбрано 3 камня.");
			pending.Payment.Add(pay);
		}

		if (!cmd.Confirm)
			return;

		if (pending.OwnCardInstanceId is null || pending.OwnToken is null
		    || pending.OtherPlayerId is null || pending.OtherCardInstanceId is null
		    || pending.OtherToken is null || pending.Payment.Count != 3)
			throw new InvalidOperationException("Неполный обмен (#67).");

		var self = State.GetPlayer(pending.PlayerId);
		var other = State.GetPlayer(pending.OtherPlayerId.Value);
		var ownCard = self.Pyramid.FindByInstanceId(pending.OwnCardInstanceId.Value)
			?? throw new InvalidOperationException("Своя карта не найдена.");
		var otherCard = other.Pyramid.FindByInstanceId(pending.OtherCardInstanceId.Value)
			?? throw new InvalidOperationException("Чужая карта не найдена.");

		if (TokenAmount(ownCard, pending.OwnToken.Value) <= 0
		    || TokenAmount(otherCard, pending.OtherToken.Value) <= 0)
			throw new InvalidOperationException("На карте нет выбранного жетона.");

		var cost = pending.Payment.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
		if (!self.Screen.CanAfford(cost))
			throw new InvalidOperationException("Не хватает камней для оплаты обмена.");
		self.Screen.Spend(cost);
		foreach (var (color, amount) in cost)
			other.Screen.Add(color, amount);

		AdjustToken(ownCard, pending.OwnToken.Value, -1);
		AdjustToken(otherCard, pending.OtherToken.Value, -1);
		AdjustToken(ownCard, pending.OtherToken.Value, +1);
		AdjustToken(otherCard, pending.OwnToken.Value, +1);

		State.PendingTokenSwap = null;
		State.PendingLaw = null;
		Raise(new LogEvent($"{self.DisplayName} обменял жетон с {other.DisplayName} (#67)"));
		ContinueAfterPrompt();
	}

	static int TokenAmount(PyramidCard card, TokenKind kind) => kind switch
	{
		TokenKind.VictoryPoints => card.VictoryPoints,
		TokenKind.Science => card.Science,
		TokenKind.Magic => card.Magic,
		TokenKind.Defense => card.Defense,
		_ => 0
	};

	static string DescribeRewardShort(Reward reward) => RewardApplicator.Describe(reward);

	static void AdjustToken(PyramidCard card, TokenKind kind, int delta)
	{
		switch (kind)
		{
			case TokenKind.VictoryPoints: card.VictoryPoints += delta; break;
			case TokenKind.Science: card.Science += delta; break;
			case TokenKind.Magic: card.Magic += delta; break;
			case TokenKind.Defense: card.Defense += delta; break;
		}
	}

	static string Truncate(string text, int max) =>
		text.Length <= max ? text : text[..max] + "…";
}
