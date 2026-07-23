using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Models;
using Namestnik.Core.Scoring;

namespace Namestnik.Core;

public sealed partial class GameEngine
{
	static readonly HashSet<string> ValidCorners = new(StringComparer.OrdinalIgnoreCase)
	{
		"tl", "tr", "bl", "br"
	};

	void BeginRecolorPhase()
	{
		Raise(new LogEvent("=== Перекраска секторов ==="));
		Raise(new LogEvent("Оставшимися камнями за ширмой можно перекрасить сектора кругов."));

		foreach (var player in State.Players)
		{
			foreach (var card in player.Hand)
				State.Discard.Add(card.DefinitionId);
			player.Hand.Clear();
			player.HasFinishedRecolor = false;
		}

		State.Phase = TurnPhase.Recolor;
		Raise(new PhaseChangedEvent(State.Phase, State.Turn));

		var scorer = new FinalScorer(_db);
		foreach (var virtualPlayer in State.Players.Where(p => p.Role == SessionRole.VirtualOpponent))
		{
			scorer.ApplyGreedyRecolor(virtualPlayer, State.Reserve);
			virtualPlayer.HasFinishedRecolor = true;
			Raise(new LogEvent($"{virtualPlayer.DisplayName} завершил перекраску (авто)"));
		}

		TryCompleteRecolorPhase();
	}

	void ApplyRecolorSector(RecolorSectorCommand cmd)
	{
		EnsurePhase(TurnPhase.Recolor);
		var player = State.GetPlayer(cmd.PlayerId);
		if (player.HasFinishedRecolor)
			throw new InvalidOperationException("Перекраска уже завершена.");

		var corner = NormalizeCorner(cmd.Corner);
		var card = player.Pyramid.FindByInstanceId(cmd.CardInstanceId)
			?? throw new InvalidOperationException("Карта не найдена в пирамиде.");

		var printed = PrintedSectors(card);
		var current = card.EffectiveSector(printed, corner);
		if (current == cmd.Color)
			return;

		// Refund previous override gem, if any.
		if (card.SectorOverrides.TryGetValue(corner, out var oldColor))
			RefundRecolorGem(player, oldColor);

		if (!player.Screen.TrySpend(cmd.Color))
			throw new InvalidOperationException($"Нет камня {ColorRu(cmd.Color)} за ширмой.");
		State.Reserve.Add(cmd.Color);

		card.SectorOverrides[corner] = cmd.Color;
		Raise(new LogEvent(
			$"{player.DisplayName}: сектор {corner.ToUpperInvariant()} карты #{card.Card.DefinitionId} → {ColorRu(cmd.Color)}"));
	}

	void ApplyClearSectorRecolor(ClearSectorRecolorCommand cmd)
	{
		EnsurePhase(TurnPhase.Recolor);
		var player = State.GetPlayer(cmd.PlayerId);
		if (player.HasFinishedRecolor)
			throw new InvalidOperationException("Перекраска уже завершена.");

		var corner = NormalizeCorner(cmd.Corner);
		var card = player.Pyramid.FindByInstanceId(cmd.CardInstanceId)
			?? throw new InvalidOperationException("Карта не найдена в пирамиде.");

		if (!card.SectorOverrides.TryGetValue(corner, out var oldColor))
			throw new InvalidOperationException("Сектор не перекрашен.");

		card.SectorOverrides.Remove(corner);
		RefundRecolorGem(player, oldColor);
		Raise(new LogEvent(
			$"{player.DisplayName}: снята перекраска {corner.ToUpperInvariant()} с карты #{card.Card.DefinitionId}"));
	}

	void ApplyConfirmRecolor(ConfirmRecolorCommand cmd)
	{
		EnsurePhase(TurnPhase.Recolor);
		var player = State.GetPlayer(cmd.PlayerId);
		if (player.HasFinishedRecolor)
			return;

		player.HasFinishedRecolor = true;
		Raise(new LogEvent($"{player.DisplayName} завершил перекраску"));
		TryCompleteRecolorPhase();
	}

	void TryCompleteRecolorPhase()
	{
		if (State.Phase != TurnPhase.Recolor)
			return;
		if (State.Players.Any(p => !p.HasFinishedRecolor))
			return;
		FinalizeMatch();
	}

	void RefundRecolorGem(PlayerState player, GemColor color)
	{
		if (State.Reserve.TrySpend(color))
			player.Screen.Add(color);
		else
			player.Screen.Add(color); // keep playable even if reserve was emptied elsewhere
	}

	SectorColors PrintedSectors(PyramidCard card) =>
		card.Card.Kind == CardKind.Character
			? _db.GetCharacter(card.Card.DefinitionId).Sectors
			: _db.GetLaw(card.Card.DefinitionId).Sectors;

	static string NormalizeCorner(string corner)
	{
		var c = corner.Trim().ToLowerInvariant();
		if (!ValidCorners.Contains(c))
			throw new InvalidOperationException($"Неизвестный угол сектора: {corner}");
		return c;
	}
}
