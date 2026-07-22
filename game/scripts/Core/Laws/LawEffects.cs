using Namestnik.Core.Events;
using Namestnik.Core.Models;
using Namestnik.Core.Rewards;

namespace Namestnik.Core.Laws;

/// <summary>On-play and triggered law resolution.</summary>
public sealed class LawEffects
{
	readonly GameState _state;
	readonly CardDatabase _db;
	readonly Action<GameEvent> _raise;
	readonly Func<CardKind, int, CardInstance> _create;
	readonly RewardApplicator _rewards;

	public LawEffects(
		GameState state,
		CardDatabase db,
		Action<GameEvent> raise,
		Func<CardKind, int, CardInstance> create,
		RewardApplicator rewards)
	{
		_state = state;
		_db = db;
		_raise = raise;
		_create = create;
		_rewards = rewards;
	}

	public static List<string> OptionLabelsFor(int lawId) => lawId switch
	{
		LawIds.ChooseAtkVpGems => ["Атака на карту", "6 VP на карту", "4 камня"],
		LawIds.ChooseDefInfGems => ["Защита на карту", "Неисчерпаемый (цвет…)", "4 камня"],
		LawIds.ChooseAtkCardGems => ["Атака на карту", "Взять 1 карту", "4 камня"],
		LawIds.ChooseDefVpGems => ["Защита на карту", "6 VP на карту", "4 камня"],
		LawIds.ChooseMagSciGems => ["Магия на карту", "Наука на карту", "4 камня"],
		LawIds.ChooseSciCardGems => ["Наука на карту", "Взять 1 карту", "4 камня"],
		LawIds.ChooseMagBonusInfGems => ["Магия + бонус магии +2", "Неисчерпаемый (цвет…)", "4 камня"],
		LawIds.ChooseBonusCardGems => ["Бонус магии +3", "Взять 1 карту", "4 камня"],
		_ => []
	};

	/// <summary>
	/// Apply immediate law effect after the law card is in play (or tucked).
	/// Returns true if a pending prompt was created (caller must wait).
	/// </summary>
	public bool BeginAfterPlay(PlayerState player, PyramidCard lawHost, int lawId)
	{
		if (LawIds.IsPassiveEndGameOnly(lawId) || lawId == LawIds.TuckOnDraw)
		{
			_raise(new LogEvent($"Закон #{lawId}: постоянный/финальный эффект"));
			return false;
		}

		if (LawIds.IsChoiceLaw(lawId))
		{
			_state.PendingLaw = new PendingLawResolution
			{
				PlayerId = player.PlayerId,
				LawInstanceId = lawHost.Card.InstanceId,
				LawDefinitionId = lawId,
				Kind = LawPromptKind.ChooseOption,
				OptionLabels = OptionLabelsFor(lawId)
			};
			_raise(new LawPromptEvent(player.PlayerId, lawId, LawPromptKind.ChooseOption,
				_state.PendingLaw.OptionLabels));
			return true;
		}

		switch (lawId)
		{
			case LawIds.OptionalSwap:
			{
				var others = _state.Players
					.Where(p => p.PlayerId != player.PlayerId
					            && p.Role != SessionRole.VirtualOpponent
					            && p.Pyramid.AllCards.Any())
					.ToList();
				if (others.Count == 0)
				{
					_raise(new LogEvent("Закон #67: нет соперника с пирамидой — обмен пропущен"));
					return false;
				}

				_state.PendingTokenSwap = new PendingTokenSwap
				{
					PlayerId = player.PlayerId,
					LawInstanceId = lawHost.Card.InstanceId
				};
				_state.PendingLaw = new PendingLawResolution
				{
					PlayerId = player.PlayerId,
					LawInstanceId = lawHost.Card.InstanceId,
					LawDefinitionId = lawId,
					Kind = LawPromptKind.OptionalSwapDecline,
					OptionLabels = ["Отказаться от обмена", "Начать обмен"]
				};
				_raise(new LawPromptEvent(player.PlayerId, lawId, LawPromptKind.OptionalSwapDecline,
					_state.PendingLaw.OptionLabels));
				_raise(new LogEvent("Закон #67: обмен жетона (3 камня сопернику) или отказ"));
				return true;
			}

			case LawIds.SkipAuction:
				player.SkipNextAuction = true;
				_state.PendingLaw = new PendingLawResolution
				{
					PlayerId = player.PlayerId,
					LawInstanceId = lawHost.Card.InstanceId,
					LawDefinitionId = lawId,
					Kind = LawPromptKind.TakeAuctionCard
				};
				_raise(new LawPromptEvent(player.PlayerId, lawId, LawPromptKind.TakeAuctionCard, []));
				_raise(new LogEvent("Закон #69: выберите карту с аукциона (следующий аукцион будет пропущен)"));
				return true;

			case LawIds.DrawLawsReturn2:
				DrawLaws(player, 3);
				_state.PendingLaw = new PendingLawResolution
				{
					PlayerId = player.PlayerId,
					LawInstanceId = lawHost.Card.InstanceId,
					LawDefinitionId = lawId,
					Kind = LawPromptKind.ReturnLawsToDeck
				};
				_raise(new LawPromptEvent(player.PlayerId, lawId, LawPromptKind.ReturnLawsToDeck, []));
				_raise(new LogEvent("Закон #70: верните 2 карты законов на верх колоды"));
				return true;

			case LawIds.TuckFromHand:
				// Extra tucks already applied at play time.
				_raise(new LogEvent($"Закон #72: подложено карт: {lawHost.TuckedCards.Count}"));
				return false;

			case LawIds.ParkGems:
				_state.PendingLaw = new PendingLawResolution
				{
					PlayerId = player.PlayerId,
					LawInstanceId = lawHost.Card.InstanceId,
					LawDefinitionId = lawId,
					Kind = LawPromptKind.ParkGems,
					OptionLabels = Enumerable.Range(0, Math.Min(9, player.Screen.TotalGems + 1))
						.Select(n => $"{n} камней")
						.ToList()
				};
				_raise(new LawPromptEvent(player.PlayerId, lawId, LawPromptKind.ParkGems,
					_state.PendingLaw.OptionLabels));
				return true;

			default:
				_raise(new LogEvent($"Закон #{lawId}: нет on-play обработчика"));
				return false;
		}
	}

	public void ResolvePrompt(PlayerState player, ResolveLawCommandData data)
	{
		var pending = _state.PendingLaw
			?? throw new InvalidOperationException("Нет активного эффекта закона.");
		if (pending.PlayerId != player.PlayerId)
			throw new InvalidOperationException("Это не ваш закон.");

		var host = player.Pyramid.FindByInstanceId(pending.LawInstanceId)
			?? throw new InvalidOperationException("Карта закона не найдена в пирамиде.");

		switch (pending.Kind)
		{
			case LawPromptKind.ChooseOption:
				ApplyChoiceOption(player, host, pending.LawDefinitionId, data.OptionIndex
					?? throw new InvalidOperationException("Нужен OptionIndex"));
				break;
			case LawPromptKind.ChooseInfiniteColor:
				ClearPending();
				PlaceInfiniteOn(player, host, data.GemColor
					?? throw new InvalidOperationException("Нужен цвет"));
				break;
			case LawPromptKind.ReturnLawsToDeck:
				ReturnTwoLaws(player, data.HandIndices
					?? throw new InvalidOperationException("Нужны индексы карт"));
				ClearPending();
				break;
			case LawPromptKind.TakeAuctionCard:
			{
				var charId = data.CharacterDefinitionId
					?? throw new InvalidOperationException("Нужна карта аукциона");
				ClearPending();
				TakeAuction(player, charId);
				break;
			}
			case LawPromptKind.ParkGems:
				ParkGems(player, host, data.OptionIndex
					?? throw new InvalidOperationException("Нужно число камней"));
				ClearPending();
				break;
			case LawPromptKind.OptionalSwapDecline:
				ClearPending();
				if (data.OptionIndex is null or 0)
				{
					_state.PendingTokenSwap = null;
					_raise(new LogEvent($"{player.DisplayName} отказывается от обмена (#67)"));
				}
				else
					_raise(new LogEvent($"{player.DisplayName}: настройте обмен (#67) через UI и подтвердите"));
				break;
			case LawPromptKind.OfferTuckDrawnCard:
			{
				var drawnId = pending.DrawnInstanceId;
				var tuck = data.OptionIndex == 1;
				ClearPending();
				if (tuck)
					TuckDrawn(player, host, drawnId
						?? throw new InvalidOperationException("Нет drawn id"));
				else
					_raise(new LogEvent($"{player.DisplayName} оставляет карту на руке (#78)"));
				break;
			}
			default:
				throw new InvalidOperationException($"Неизвестный prompt {pending.Kind}");
		}
	}

	void ApplyChoiceOption(PlayerState player, PyramidCard host, int lawId, int option)
	{
		switch (lawId)
		{
			case LawIds.ChooseAtkVpGems:
				ClearPending();
				if (option == 0) { player.Screen.AttackTokens++; _raise(new LogEvent("+1 атака")); }
				else if (option == 1) host.VictoryPoints += 6;
				else _rewards.TakeAnyGems(player, 4);
				break;
			case LawIds.ChooseDefInfGems:
				if (option == 1) { AskInfiniteColor(player, host, lawId); break; }
				ClearPending();
				if (option == 0) host.Defense++;
				else _rewards.TakeAnyGems(player, 4);
				break;
			case LawIds.ChooseAtkCardGems:
				ClearPending();
				if (option == 0) player.Screen.AttackTokens++;
				else if (option == 1) DrawOneToHand(player);
				else _rewards.TakeAnyGems(player, 4);
				break;
			case LawIds.ChooseDefVpGems:
				ClearPending();
				if (option == 0) host.Defense++;
				else if (option == 1) host.VictoryPoints += 6;
				else _rewards.TakeAnyGems(player, 4);
				break;
			case LawIds.ChooseMagSciGems:
				ClearPending();
				if (option == 0) host.Magic++;
				else if (option == 1) host.Science++;
				else _rewards.TakeAnyGems(player, 4);
				break;
			case LawIds.ChooseSciCardGems:
				ClearPending();
				if (option == 0) host.Science++;
				else if (option == 1) DrawOneToHand(player);
				else _rewards.TakeAnyGems(player, 4);
				break;
			case LawIds.ChooseMagBonusInfGems:
				if (option == 1) { AskInfiniteColor(player, host, lawId); break; }
				ClearPending();
				if (option == 0) { host.Magic++; host.BonusMagic += 2; }
				else _rewards.TakeAnyGems(player, 4);
				break;
			case LawIds.ChooseBonusCardGems:
				ClearPending();
				if (option == 0) host.BonusMagic += 3;
				else if (option == 1) DrawOneToHand(player);
				else _rewards.TakeAnyGems(player, 4);
				break;
			default:
				throw new InvalidOperationException($"Закон {lawId} не choice");
		}
	}

	void AskInfiniteColor(PlayerState player, PyramidCard host, int lawId)
	{
		_state.PendingLaw = new PendingLawResolution
		{
			PlayerId = player.PlayerId,
			LawInstanceId = host.Card.InstanceId,
			LawDefinitionId = lawId,
			Kind = LawPromptKind.ChooseInfiniteColor,
			OptionLabels = ["Синий", "Красный", "Зелёный", "Жёлтый"],
			AwaitingInfiniteColor = true
		};
		_raise(new LawPromptEvent(player.PlayerId, lawId, LawPromptKind.ChooseInfiniteColor,
			_state.PendingLaw.OptionLabels));
	}

	void PlaceInfiniteOn(PlayerState player, PyramidCard host, GemColor color)
	{
		var needs = false;
		var draws = 0;
		_rewards.Apply(player, host, new InfiniteReward(color), ref needs, ref draws);
	}

	void DrawLaws(PlayerState player, int n)
	{
		for (var i = 0; i < n; i++)
		{
			if (_state.LawDeck.Count == 0)
				break;
			var id = _state.LawDeck[0];
			_state.LawDeck.RemoveAt(0);
			player.Hand.Add(_create(CardKind.Law, id));
			_raise(new CardDrawnEvent(player.PlayerId, id, CardKind.Law));
		}
	}

	void ReturnTwoLaws(PlayerState player, IReadOnlyList<int> handIndices)
	{
		if (handIndices.Count != 2)
			throw new InvalidOperationException("Нужно ровно 2 карты.");
		var ordered = handIndices.Distinct().OrderByDescending(i => i).ToList();
		if (ordered.Count != 2)
			throw new InvalidOperationException("Индексы должны отличаться.");

		var cards = new List<CardInstance>();
		foreach (var idx in ordered)
		{
			if (idx < 0 || idx >= player.Hand.Count)
				throw new InvalidOperationException("Неверный индекс руки.");
			if (player.Hand[idx].Kind != CardKind.Law)
				throw new InvalidOperationException("Можно возвращать только законы.");
			cards.Add(player.Hand[idx]);
			player.Hand.RemoveAt(idx);
		}

		// Put back on top in given order (first selected ends deeper — use ascending original order).
		foreach (var card in cards.AsEnumerable().Reverse())
			_state.LawDeck.Insert(0, card.DefinitionId);
		_raise(new LogEvent($"{player.DisplayName} возвращает 2 закона на колоду"));
	}

	void TakeAuction(PlayerState player, int characterId)
	{
		foreach (var slot in _state.AuctionSlots)
		{
			if (!slot.TryRemoveCard(characterId))
				continue;
			player.Hand.Add(_create(CardKind.Character, characterId));
			var name = _db.Characters.TryGetValue(characterId, out var c) ? c.Name : $"#{characterId}";
			_raise(new LogEvent($"{player.DisplayName} берёт с аукциона «{name}» (#69)"));

			// Replace from small deck at same position (prefer base).
			if (_state.SmallDeck.Count > 0)
			{
				var repl = _state.SmallDeck[0];
				_state.SmallDeck.RemoveAt(0);
				if (slot.CardAtBase is null)
					slot.CardAtBase = repl;
				else
					slot.CardAtTip = repl;
				_raise(new LogEvent($"На аукцион из малой колоды: #{repl}"));
			}

			NotifyCardTaken(player);
			return;
		}

		throw new InvalidOperationException("Этой карты нет на аукционе.");
	}

	void ParkGems(PlayerState player, PyramidCard host, int amount)
	{
		amount = Math.Clamp(amount, 0, 8);
		var moved = 0;
		while (moved < amount)
		{
			var progressed = false;
			foreach (GemColor color in Enum.GetValues<GemColor>())
			{
				if (moved >= amount)
					break;
				if (player.Screen.TrySpend(color))
				{
					host.ParkedGems++;
					moved++;
					progressed = true;
				}
			}

			if (!progressed)
				break;
		}

		_raise(new LogEvent($"{player.DisplayName} кладёт на закон #{host.Card.DefinitionId} камней: {moved}"));
	}

	void DrawOneToHand(PlayerState player) => _rewards.RequestCardDraws(player, 1);

	/// <summary>Law 78 trigger after any card taken to hand (development phase).</summary>
	public void NotifyCardTaken(PlayerState player)
	{
		if (_state.Phase != TurnPhase.Development)
			return;
		if (_state.PendingLaw is not null)
			return;
		if (!player.HasTuckOnDrawLaw)
			return;
		if (player.Hand.Count == 0)
			return;

		var lawHost = player.Pyramid.AllCards.First(c =>
			c.Card.Kind == CardKind.Law && c.Card.DefinitionId == LawIds.TuckOnDraw);
		var drawn = player.Hand[^1];
		_state.PendingLaw = new PendingLawResolution
		{
			PlayerId = player.PlayerId,
			LawInstanceId = lawHost.Card.InstanceId,
			LawDefinitionId = LawIds.TuckOnDraw,
			Kind = LawPromptKind.OfferTuckDrawnCard,
			DrawnInstanceId = drawn.InstanceId,
			OptionLabels = ["Оставить на руке", "Подложить под #78"]
		};
		_state.DevelopmentSubPhase = DevelopmentSubPhase.ResolvingLaw;
		_raise(new LawPromptEvent(player.PlayerId, LawIds.TuckOnDraw, LawPromptKind.OfferTuckDrawnCard,
			_state.PendingLaw.OptionLabels));
	}

	void TuckDrawn(PlayerState player, PyramidCard lawHost, int drawnInstanceId)
	{
		var idx = player.Hand.FindIndex(c => c.InstanceId == drawnInstanceId);
		if (idx < 0)
			throw new InvalidOperationException("Карта уже не на руке.");
		var card = player.Hand[idx];
		player.Hand.RemoveAt(idx);
		lawHost.TuckedCards.Add(card);
		_raise(new LogEvent($"{player.DisplayName} подкладывает карту #{card.DefinitionId} под #78"));
	}

	void ClearPending() => _state.PendingLaw = null;

	public static void TransferTokens(PyramidCard from, PyramidCard to)
	{
		to.VictoryPoints += from.VictoryPoints;
		from.VictoryPoints = 0;
		to.Science += from.Science;
		from.Science = 0;
		to.Magic += from.Magic;
		from.Magic = 0;
		to.Defense += from.Defense;
		from.Defense = 0;
		to.BonusMagic += from.BonusMagic;
		from.BonusMagic = 0;
		to.ParkedGems += from.ParkedGems;
		from.ParkedGems = 0;
		foreach (var b in from.BonusCircles)
			to.BonusCircles.Add(b);
		from.BonusCircles.Clear();
		to.TuckedCards.AddRange(from.TuckedCards);
		from.TuckedCards.Clear();
	}

	public static void DuplicateTokens(PyramidCard card)
	{
		// ParkedGems are physical stones — never duplicate.
		card.VictoryPoints *= 2;
		card.Science *= 2;
		card.Magic *= 2;
		card.Defense *= 2;
		card.BonusMagic *= 2;
		var circles = card.BonusCircles.ToList();
		card.BonusCircles.Clear();
		foreach (var (color, amount) in circles)
			card.BonusCircles.Add((color, amount * 2));
	}

	public void TransferTokensSafe(PyramidCard from, PyramidCard to)
	{
		TransferTokens(from, to);
		if (from.InfiniteGem is GemColor inf)
		{
			from.InfiniteGem = null;
			if (to.InfiniteGem is null)
				to.InfiniteGem = inf;
			else
				_state.Reserve.Add(inf);
		}
	}
}

/// <summary>Payload for ResolveLawCommand.</summary>
public sealed class ResolveLawCommandData
{
	public int? OptionIndex { get; init; }
	public GemColor? GemColor { get; init; }
	public IReadOnlyList<int>? HandIndices { get; init; }
	public int? CharacterDefinitionId { get; init; }
}
