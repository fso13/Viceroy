using Namestnik.Core.Events;
using Namestnik.Core.Models;

namespace Namestnik.Core.Rewards;

/// <summary>Applies character rewards onto a player / pyramid card.</summary>
public sealed class RewardApplicator
{
	readonly GameState _state;
	readonly CardDatabase _db;
	readonly Action<GameEvent> _raise;
	readonly Func<CardKind, int, CardInstance> _createInstance;
	readonly Action<PlayerState>? _afterCardTaken;

	public RewardApplicator(
		GameState state,
		CardDatabase db,
		Action<GameEvent> raise,
		Func<CardKind, int, CardInstance> createInstance,
		Action<PlayerState>? afterCardTaken = null)
	{
		_state = state;
		_db = db;
		_raise = raise;
		_createInstance = createInstance;
		_afterCardTaken = afterCardTaken;
	}

	public void Apply(
		PlayerState player,
		PyramidCard host,
		Reward reward,
		ref bool needsLevel5Choice,
		ref int pendingCardDraws,
		bool suppressChoicePrompt = false)
	{
		switch (reward)
		{
			case VpReward vp:
				host.VictoryPoints += vp.Amount;
				_raise(new LogEvent($"{player.DisplayName}: +{vp.Amount} VP на карту"));
				break;
			case GemsReward gems:
				TakeAnyGems(player, gems.Amount);
				break;
			case CardReward cards:
				pendingCardDraws += cards.Amount;
				break;
			case ScienceReward sci:
				host.Science += sci.Amount;
				_raise(new LogEvent($"{player.DisplayName}: +{sci.Amount} наука"));
				break;
			case MagicReward mag:
				host.Magic += mag.Amount;
				_raise(new LogEvent($"{player.DisplayName}: +{mag.Amount} магия"));
				break;
			case DefenseReward def:
				host.Defense += def.Amount;
				_raise(new LogEvent($"{player.DisplayName}: +{def.Amount} защита"));
				break;
			case AttackReward atk:
				player.Screen.AttackTokens += atk.Amount;
				_raise(new LogEvent($"{player.DisplayName}: +{atk.Amount} атака (за ширму)"));
				break;
			case InfiniteReward inf:
				PlaceInfinite(player, host, inf.Color);
				break;
			case BonusMagicReward bm:
				host.BonusMagic += bm.Amount;
				_raise(new LogEvent($"{player.DisplayName}: бонус магии +{bm.Amount}"));
				break;
			case BonusCircleReward bc:
				host.BonusCircles.Add((bc.Color, bc.Amount));
				_raise(new LogEvent($"{player.DisplayName}: бонус круга {bc.Color} +{bc.Amount}"));
				break;
			case MultiReward multi:
				foreach (var part in multi.Parts)
					Apply(player, host, part, ref needsLevel5Choice, ref pendingCardDraws, suppressChoicePrompt);
				break;
			case ChoiceReward choice:
				if (choice.Options.Count == 0)
					break;
				if (suppressChoicePrompt || choice.Options.Count == 1)
				{
					Apply(player, host, choice.Options[0], ref needsLevel5Choice, ref pendingCardDraws,
						suppressChoicePrompt);
					break;
				}

				_state.PendingRewardChoice = new PendingRewardChoice
				{
					PlayerId = player.PlayerId,
					HostInstanceId = host.Card.InstanceId,
					Options = choice.Options,
					OptionLabels = choice.Options.Select(DescribeReward).ToList()
				};
				break;
			default:
				_raise(new LogEvent($"Неизвестная награда: {reward.GetType().Name}"));
				break;
		}
	}

	void PlaceInfinite(PlayerState player, PyramidCard host, GemColor color)
	{
		if (host.InfiniteGem is not null)
		{
			_raise(new LogEvent("На карте уже есть неисчерпаемый камень"));
			return;
		}

		if (_state.Reserve.TrySpend(color))
		{
			host.InfiniteGem = color;
			_raise(new LogEvent($"{player.DisplayName}: неисчерпаемый {color} (из резерва)"));
			return;
		}

		if (player.Screen.TrySpend(color))
		{
			host.InfiniteGem = color;
			_raise(new LogEvent($"{player.DisplayName}: неисчерпаемый {color} (из-за ширмы)"));
			return;
		}

		_raise(new LogEvent($"{player.DisplayName}: нет камня {color} для неисчерпаемого"));
	}

	static string DescribeReward(Reward reward) => reward switch
	{
		VpReward vp => $"+{vp.Amount} VP",
		GemsReward g => $"+{g.Amount} камней",
		CardReward c => $"+{c.Amount} карт",
		ScienceReward s => $"+{s.Amount} наука",
		MagicReward m => $"+{m.Amount} магия",
		DefenseReward d => $"+{d.Amount} защита",
		AttackReward a => $"+{a.Amount} атака",
		InfiniteReward i => $"∞ {i.Color}",
		BonusMagicReward b => $"бонус магии +{b.Amount}",
		BonusCircleReward bc => $"бонус круга {bc.Color} +{bc.Amount}",
		MultiReward multi => string.Join(" + ", multi.Parts.Select(DescribeReward)),
		ChoiceReward => "выбор…",
		_ => reward.GetType().Name
	};

	public void TakeAnyGems(PlayerState player, int amount)
	{
		var taken = 0;
		while (taken < amount)
		{
			var progressed = false;
			foreach (GemColor color in Enum.GetValues<GemColor>())
			{
				if (taken >= amount)
					break;
				if (_state.Reserve.TrySpend(color))
				{
					player.Screen.Add(color);
					taken++;
					progressed = true;
				}
			}

			if (!progressed)
				break;
		}

		_raise(new LogEvent($"{player.DisplayName}: взял {taken}/{amount} камней"));
	}

	public void DrawCardsAuto(PlayerState player, int amount)
	{
		for (var i = 0; i < amount; i++)
		{
			if (_state.LawDeck.Count > 0)
			{
				var id = _state.LawDeck[0];
				_state.LawDeck.RemoveAt(0);
				player.Hand.Add(_createInstance(CardKind.Law, id));
				_raise(new CardDrawnEvent(player.PlayerId, id, CardKind.Law));
				_raise(new LogEvent($"{player.DisplayName} тянет закон #{id}"));
				_afterCardTaken?.Invoke(player);
			}
			else if (_state.SmallDeck.Count > 0)
			{
				var id = _state.SmallDeck[0];
				_state.SmallDeck.RemoveAt(0);
				player.Hand.Add(_createInstance(CardKind.Character, id));
				_raise(new CardDrawnEvent(player.PlayerId, id, CardKind.Character));
				var name = _db.Characters.TryGetValue(id, out var c) ? c.Name : $"#{id}";
				_raise(new LogEvent($"{player.DisplayName} тянет «{name}» из малой колоды"));
				_afterCardTaken?.Invoke(player);
			}
			else
			{
				_raise(new LogEvent($"{player.DisplayName}: колоды пусты, карта не взята"));
				break;
			}
		}
	}
}
