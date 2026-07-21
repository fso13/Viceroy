using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Models;

namespace Namestnik.Core;

public sealed partial class GameEngine
{
	void SubmitSealedBid(int playerId, AuctionBid bid)
	{
		EnsurePhase(TurnPhase.Auction);
		if (State.AuctionSubPhase != AuctionSubPhase.CollectingBids)
			throw new InvalidOperationException("Сейчас нельзя делать ставки (ожидается выбор карты).");

		var player = State.GetPlayer(playerId);
		if (player.HasPassedAuction || player.AcquiredAuctionCardThisTurn)
			throw new InvalidOperationException("Вы уже закончили аукцион в этом ходу.");
		if (State.SealedBids.ContainsKey(playerId))
			throw new InvalidOperationException("Ставка уже принята в этом круге.");

		ValidateBid(player, bid);
		State.SealedBids[playerId] = bid;
		Raise(new BidAcceptedEvent(playerId));
		Raise(new LogEvent($"{player.DisplayName} сделал ставку (скрыто)"));

		TryResolveAuctionRound();
	}

	void ValidateBid(PlayerState player, AuctionBid bid)
	{
		switch (bid)
		{
			case PassAuctionBid:
				return;
			case GemAuctionBid gem:
				if (State.Slot(gem.Color).AvailableCount == 0)
					throw new InvalidOperationException($"Нет карт цвета {gem.Color}.");
				if (player.Role == SessionRole.VirtualOpponent)
				{
					if (State.VirtualBox[gem.Color] <= 0)
						throw new InvalidOperationException("В коробке виртуала нет такого камня.");
				}
				else if (!player.Screen.CanPay(gem.Color))
				{
					throw new InvalidOperationException($"Нет камня {gem.Color} за ширмой.");
				}

				return;
			case AttackAuctionBid:
				if (player.Role == SessionRole.VirtualOpponent)
					return; // virtual "attack" via empty color handled elsewhere
				if (player.Screen.AttackTokens <= 0)
					throw new InvalidOperationException("Нет жетонов атаки.");
				if (!State.AuctionSlots.Any(s => s.AvailableCount > 0))
					throw new InvalidOperationException("На аукционе нет карт.");
				return;
			default:
				throw new InvalidOperationException("Неизвестная ставка.");
		}
	}

	void TryResolveAuctionRound()
	{
		var active = State.ActiveAuctionPlayers().Select(p => p.PlayerId).ToHashSet();
		if (active.Any(id => !State.SealedBids.ContainsKey(id)))
			return;

		ResolveCollectedBids();
	}

	void ResolveCollectedBids()
	{
		var bids = State.SealedBids.ToDictionary(kv => kv.Key, kv => kv.Value);
		State.SealedBids.Clear();
		var round = (int)State.AuctionRound;
		Raise(new LogEvent($"=== Раскрытие ставок (круг {round}) ==="));

		var attackOrder = OrderByPriority(bids.Where(kv => kv.Value is AttackAuctionBid).Select(kv => kv.Key));
		foreach (var playerId in attackOrder)
			ResolveAttack(playerId, (AttackAuctionBid)bids[playerId]);

		var gemBids = bids
			.Where(kv => kv.Value is GemAuctionBid)
			.GroupBy(kv => ((GemAuctionBid)kv.Value).Color)
			.ToList();

		foreach (var group in gemBids)
			ResolveGemColor(group.Key, group.Select(g => g.Key).ToList());

		var passers = OrderByPriority(bids.Where(kv => kv.Value is PassAuctionBid).Select(kv => kv.Key)).ToList();
		State.DeferredPassers.Clear();
		State.DeferredPassers.AddRange(passers);

		if (State.PendingCardChoices.Count > 0)
		{
			State.AuctionSubPhase = AuctionSubPhase.ChoosingCards;
			foreach (var pending in State.PendingCardChoices)
			{
				Raise(new CardChoiceRequiredEvent(pending.PlayerId, pending.Color, pending.Options));
				var names = string.Join(", ", pending.Options.Select(id => CardName(id)));
				var who = State.GetPlayer(pending.PlayerId).DisplayName;
				Raise(new LogEvent($"{who} выбирает карту ({pending.Color}): {names}"));
			}

			Raise(new AuctionRoundResolvedEvent(round, "Ожидание выбора карт"));
			return;
		}

		if (!TryBeginNextPasserGems())
			FinishRoundOrAuction(round);
	}

	bool TryBeginNextPasserGems()
	{
		while (State.DeferredPassers.Count > 0)
		{
			var playerId = State.DeferredPassers[0];
			State.DeferredPassers.RemoveAt(0);
			var player = State.GetPlayer(playerId);
			if (player.HasPassedAuction || player.AcquiredAuctionCardThisTurn)
				continue;

			if (player.Role == SessionRole.VirtualOpponent)
			{
				ApplyAuctionPassAuto(player);
				continue;
			}

			player.HasPassedAuction = true;
			var amount = 3 + player.ScienceTokens;
			State.PendingPassGems = new PendingPassGems { PlayerId = playerId, Amount = amount };
			State.AuctionSubPhase = AuctionSubPhase.ClaimingPassGems;
			Raise(new PassGemsRequiredEvent(playerId, amount));
			Raise(new LogEvent($"{player.DisplayName}: выберите {amount} камней за пас"));
			return true;
		}

		return false;
	}

	void FinishRoundOrAuction(int round)
	{
		var stillActive = State.ActiveAuctionPlayers().ToList();
		if (stillActive.Count == 0)
		{
			CompleteAuctionPhase();
			return;
		}

		if (round >= GameState.MaxAuctionRounds)
		{
			Raise(new LogEvent("3 круга истекли — оставшиеся пасуют автоматически"));
			State.DeferredPassers.Clear();
			State.DeferredPassers.AddRange(OrderByPriority(stillActive.Select(x => x.PlayerId)));
			if (TryBeginNextPasserGems())
				return;
			CompleteAuctionPhase();
			return;
		}

		State.AuctionRound = (AuctionRound)(round + 1);
		State.AuctionSubPhase = AuctionSubPhase.CollectingBids;
		Raise(new AuctionRoundStartedEvent(round + 1));
		Raise(new AuctionRoundResolvedEvent(round, $"Переход к кругу {round + 1}"));
		Raise(new LogEvent($"Аукцион, круг {round + 1} — новые ставки"));
	}

	void ResolveAttack(int playerId, AttackAuctionBid bid)
	{
		var player = State.GetPlayer(playerId);
		if (player.AcquiredAuctionCardThisTurn || player.HasPassedAuction)
			return;

		var options = State.AuctionSlots.SelectMany(s => s.AvailableCards()).ToList();
		if (options.Count == 0)
		{
			Raise(new LogEvent($"{player.DisplayName}: атака, но карт не осталось"));
			return;
		}

		if (player.Role != SessionRole.VirtualOpponent)
		{
			if (player.Screen.AttackTokens <= 0)
				return;
			player.Screen.AttackTokens--;
		}

		var pick = bid.PreferredCharacterId is int pref && options.Contains(pref)
			? pref
			: options[0];

		if (player.Role == SessionRole.VirtualOpponent)
		{
			RemoveAuctionCard(pick);
			State.Discard.Add(pick);
			Raise(new LogEvent($"Виртуал атакой сбрасывает «{CardName(pick)}»"));
			player.AcquiredAuctionCardThisTurn = true; // leaves auction
		}
		else
		{
			GiveAuctionCardToHand(player, pick, color: null);
		}
	}

	void ResolveGemColor(GemColor color, List<int> bidderIds)
	{
		bidderIds = bidderIds
			.Where(id =>
			{
				var p = State.GetPlayer(id);
				return !p.HasPassedAuction && !p.AcquiredAuctionCardThisTurn;
			})
			.ToList();
		if (bidderIds.Count == 0)
			return;

		var slot = State.Slot(color);
		var cards = slot.AvailableCards().ToList();
		var n = cards.Count;
		var k = bidderIds.Count;

		// Spend stakes first (always).
		foreach (var id in bidderIds)
			SpendGemStake(State.GetPlayer(id), color);

		var humans = bidderIds.Where(id => State.GetPlayer(id).Role != SessionRole.VirtualOpponent).ToList();
		var virtuals = bidderIds.Where(id => State.GetPlayer(id).Role == SessionRole.VirtualOpponent).ToList();

		// Solo / virtual interaction shortcuts from the rules.
		if (virtuals.Count > 0 && humans.Count > 0)
		{
			ResolveHumanVsVirtualGem(color, humans, virtuals, cards);
			return;
		}

		if (virtuals.Count > 0 && humans.Count == 0)
		{
			ResolveVirtualOnlyGem(color, virtuals, cards);
			return;
		}

		// Pure human resolution.
		if (k == 1 && n >= 1)
		{
			var winner = State.GetPlayer(bidderIds[0]);
			if (n == 1)
				GiveAuctionCardToHand(winner, cards[0], color);
			else
				QueueChoice(winner.PlayerId, color, cards);
			return;
		}

		if (k == 2 && n == 2)
		{
			// Digital auto-split: lower priority key takes tip, other takes base.
			var ordered = OrderByPriority(bidderIds).ToList();
			var tip = slot.CardAtTip!.Value;
			var bas = slot.CardAtBase!.Value;
			GiveAuctionCardToHand(State.GetPlayer(ordered[0]), tip, color);
			GiveAuctionCardToHand(State.GetPlayer(ordered[1]), bas, color);
			Raise(new LogEvent($"Авто-раздел {color}: тип→{State.GetPlayer(ordered[0]).DisplayName}, база→{State.GetPlayer(ordered[1]).DisplayName}"));
			return;
		}

		// Contested: nobody gets a card, stay for next round.
		Raise(new LogEvent($"Конфликт на {color} (k={k}, n={n}) — карты не розданы"));
	}

	void ResolveHumanVsVirtualGem(GemColor color, List<int> humans, List<int> virtuals, List<int> cards)
	{
		var n = cards.Count;
		var slot = State.Slot(color);

		// n=1: conflict with virtual — card stays, everyone continues next round.
		if (n == 1)
		{
			Raise(new LogEvent($"Конфликт с виртуалом на {color} — карта остаётся, новый круг"));
			return;
		}

		// n=2: human takes tip (or chooses if several humans — rare); virtual discards base.
		if (n == 2)
		{
			var orderedHumans = OrderByPriority(humans).ToList();
			var tip = slot.CardAtTip!.Value;
			var bas = slot.CardAtBase!.Value;

			if (orderedHumans.Count == 1)
			{
				GiveAuctionCardToHand(State.GetPlayer(orderedHumans[0]), tip, color);
			}
			else
			{
				// Several humans + virtual on 2 cards: first human tip, second base; virtual leaves.
				GiveAuctionCardToHand(State.GetPlayer(orderedHumans[0]), tip, color);
				if (orderedHumans.Count > 1)
					GiveAuctionCardToHand(State.GetPlayer(orderedHumans[1]), bas, color);
				foreach (var v in virtuals)
					State.GetPlayer(v).AcquiredAuctionCardThisTurn = true;
				Raise(new LogEvent($"Раздел {color} между игроками; виртуалы выбывают"));
				return;
			}

			if (slot.CardAtBase == bas)
			{
				RemoveAuctionCard(bas);
				State.Discard.Add(bas);
				Raise(new LogEvent($"Виртуал сбрасывает «{CardName(bas)}» (основание)"));
			}

			foreach (var v in virtuals)
				State.GetPlayer(v).AcquiredAuctionCardThisTurn = true;
			return;
		}

		Raise(new LogEvent($"Конфликт на {color} (люди={humans.Count}, виртуалы={virtuals.Count}, карт={n})"));
	}

	void ResolveVirtualOnlyGem(GemColor color, List<int> virtuals, List<int> cards)
	{
		var n = cards.Count;
		var slot = State.Slot(color);

		if (virtuals.Count >= 2 && n == 1)
		{
			// Card stays; virtuals continue to next round.
			Raise(new LogEvent($"Два виртуала на {color} (1 карта) — карта не сбрасывается"));
			return;
		}

		if (virtuals.Count >= 2 && n == 2)
		{
			foreach (var id in cards.ToList())
			{
				RemoveAuctionCard(id);
				State.Discard.Add(id);
			}

			foreach (var v in virtuals)
				State.GetPlayer(v).AcquiredAuctionCardThisTurn = true;
			Raise(new LogEvent($"Виртуалы сбрасывают обе карты {color}"));
			return;
		}

		if (virtuals.Count == 1 && n >= 1)
		{
			// Prefer discard base if two cards.
			var discardId = slot.CardAtBase ?? slot.CardAtTip!.Value;
			RemoveAuctionCard(discardId);
			State.Discard.Add(discardId);
			State.GetPlayer(virtuals[0]).AcquiredAuctionCardThisTurn = true;
			Raise(new LogEvent($"Виртуал забирает в сброс «{CardName(discardId)}» ({color})"));
		}
	}

	void SpendGemStake(PlayerState player, GemColor color)
	{
		if (player.Role == SessionRole.VirtualOpponent)
		{
			if (State.VirtualBox.TrySpend(color))
				State.VirtualBox.Add(color); // immediately return to box (never enters reserve)
			return;
		}

		if (player.Screen.TrySpend(color))
			State.Reserve.Add(color);
	}

	void QueueChoice(int playerId, GemColor color, List<int> options)
	{
		State.PendingCardChoices.Add(new PendingCardChoice
		{
			PlayerId = playerId,
			Color = color,
			Options = options.ToList()
		});
	}

	void ApplyChooseAuctionCard(ChooseAuctionCardCommand cmd)
	{
		EnsurePhase(TurnPhase.Auction);
		if (State.AuctionSubPhase != AuctionSubPhase.ChoosingCards)
			throw new InvalidOperationException("Сейчас нет выбора карты.");

		var pending = State.PendingCardChoices.FirstOrDefault(p => p.PlayerId == cmd.PlayerId)
			?? throw new InvalidOperationException("Нет ожидающего выбора.");
		if (!pending.Options.Contains(cmd.CharacterId))
			throw new InvalidOperationException("Этой карты нет среди вариантов.");
		if (!State.Slot(pending.Color).AvailableCards().Contains(cmd.CharacterId))
			throw new InvalidOperationException("Карта уже недоступна.");

		var player = State.GetPlayer(cmd.PlayerId);
		GiveAuctionCardToHand(player, cmd.CharacterId, pending.Color);
		State.PendingCardChoices.Remove(pending);

		if (State.PendingCardChoices.Count > 0)
			return;

		if (!TryBeginNextPasserGems())
			FinishRoundOrAuction((int)State.AuctionRound);
	}

	void GiveAuctionCardToHand(PlayerState player, int characterId, GemColor? color)
	{
		if (!RemoveAuctionCard(characterId))
			throw new InvalidOperationException($"Карта {characterId} не на аукционе.");

		player.Hand.Add(CreateInstance(CardKind.Character, characterId));
		player.AcquiredAuctionCardThisTurn = true;
		Raise(new AuctionCardAcquiredEvent(player.PlayerId, characterId, color));
		Raise(new LogEvent($"{player.DisplayName} получает «{CardName(characterId)}» на руку"));
	}

	bool RemoveAuctionCard(int characterId)
	{
		foreach (var slot in State.AuctionSlots)
		{
			if (slot.TryRemoveCard(characterId))
				return true;
		}

		return false;
	}

	void ApplyAuctionPassAuto(PlayerState player)
	{
		if (player.HasPassedAuction || player.AcquiredAuctionCardThisTurn)
			return;

		player.HasPassedAuction = true;
		if (player.Role == SessionRole.VirtualOpponent)
		{
			Raise(new LogEvent($"{player.DisplayName} пасует"));
			return;
		}

		var amount = 3 + player.ScienceTokens;
		var taken = TakeGemsAny(player, amount);
		Raise(new LogEvent($"{player.DisplayName} пасует и берёт {taken} камней (авто, лимит {amount})"));
	}

	void ApplyClaimPassGem(ClaimPassGemsCommand cmd)
	{
		EnsurePhase(TurnPhase.Auction);
		if (State.AuctionSubPhase != AuctionSubPhase.ClaimingPassGems)
			throw new InvalidOperationException("Сейчас не выбор камней за пас.");
		var pending = State.PendingPassGems
			?? throw new InvalidOperationException("Нет ожидающего паса.");
		if (pending.PlayerId != cmd.PlayerId)
			throw new InvalidOperationException("Это не ваш пас.");

		var player = State.GetPlayer(cmd.PlayerId);
		if (cmd.Color is GemColor color)
		{
			if (pending.Picked.Count >= pending.Amount)
				throw new InvalidOperationException("Уже выбрано достаточно камней.");
			if (!State.Reserve.TrySpend(color))
				throw new InvalidOperationException($"В резерве нет камня {color}.");
			player.Screen.Add(color);
			pending.Picked.Add(color);
			Raise(new LogEvent($"{player.DisplayName}: +1 {color} ({pending.Picked.Count}/{pending.Amount})"));
		}

		if (!cmd.Confirm && pending.Picked.Count < pending.Amount)
			return;

		// Confirm early or fill remainder automatically if reserve empty mid-way.
		while (pending.Picked.Count < pending.Amount)
		{
			var progressed = false;
			foreach (GemColor c in Enum.GetValues<GemColor>())
			{
				if (pending.Picked.Count >= pending.Amount)
					break;
				if (!State.Reserve.TrySpend(c))
					continue;
				player.Screen.Add(c);
				pending.Picked.Add(c);
				progressed = true;
			}

			if (!progressed)
				break;
		}

		Raise(new LogEvent($"{player.DisplayName} завершил пас: {pending.Picked.Count} камней"));
		State.PendingPassGems = null;

		if (TryBeginNextPasserGems())
			return;

		if (State.PendingCardChoices.Count > 0)
		{
			State.AuctionSubPhase = AuctionSubPhase.ChoosingCards;
			return;
		}

		FinishRoundOrAuction((int)State.AuctionRound);
	}

	int TakeGemsAny(PlayerState player, int amount)
	{
		var taken = 0;
		// Prefer spreading colors for fairness in auto-claim.
		while (taken < amount)
		{
			var progressed = false;
			foreach (GemColor color in Enum.GetValues<GemColor>())
			{
				if (taken >= amount)
					break;
				if (State.Reserve.TrySpend(color))
				{
					player.Screen.Add(color);
					taken++;
					progressed = true;
				}
			}

			if (!progressed)
				break;
		}

		return taken;
	}

	void CompleteAuctionPhase()
	{
		State.AuctionSubPhase = AuctionSubPhase.Done;
		AdvanceAuctionBoard();
		BeginDevelopment();
	}

	void AdvanceAuctionBoard()
	{
		foreach (var slot in State.AuctionSlots)
		{
			if (slot.CardAtTip is int tip)
				State.Discard.Add(tip);
			slot.CardAtTip = slot.CardAtBase;
			slot.CardAtBase = DrawBig();
		}

		var last = State.BigDeck.Count == 0;
		if (last && !State.FinalTurnInProgress)
			State.NextTurnIsLast = true;

		Raise(new AuctionBoardAdvancedEvent(last));
		Raise(new AuctionResolvedEvent(last
			? "Ряд сдвинут — выложены последние карты (следующий ход финальный)"
			: "Ряд аукциона сдвинут, новые карты у оснований"));
		Raise(new LogEvent(last
			? "Большая колода исчерпана — следующий ход последний"
			: "Аукцион завершён, карты сдвинуты"));
	}

	void BeginDevelopment()
	{
		foreach (var p in State.Players)
		{
			p.HasPassedDevelopment = false;
			p.ActedThisDevelopmentRound = false;
		}

		State.SealedDevActions.Clear();
		State.PendingLevel5 = null;
		State.Phase = TurnPhase.Development;
		State.DevelopmentRound = 1;
		State.DevelopmentSubPhase = DevelopmentSubPhase.CollectingActions;
		State.AuctionRound = AuctionRound.None;
		State.AuctionSubPhase = AuctionSubPhase.CollectingBids;
		Raise(new PhaseChangedEvent(State.Phase, State.Turn));
		Raise(new LogEvent("Фаза развития, раунд 1 — сыграйте / сбросьте карту или пасуйте"));
	}

	IEnumerable<int> OrderByPriority(IEnumerable<int> playerIds) =>
		playerIds
			.Select(id => State.GetPlayer(id))
			.OrderBy(p => p.PriorityKey)
			.ThenBy(p => p.PlayerId)
			.Select(p => p.PlayerId);
}
