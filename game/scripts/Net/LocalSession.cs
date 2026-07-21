using Namestnik.Ai;
using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Models;

namespace Namestnik.Net;

/// <summary>Solo (or hot-seat) session: engine runs locally.</summary>
public sealed class LocalSession : IGameSession
{
	readonly CardDatabase _db;
	readonly int _humans;
	readonly int _virtuals;
	readonly VirtualOpponent _virtualAi = new();

	public GameMode Mode => GameMode.Solo;
	public GameEngine? Engine { get; private set; }
	public int LocalPlayerId => 0;

	public event Action<GameEvent>? EventReceived;

	public LocalSession(CardDatabase db, int humans = 1, int virtuals = 1)
	{
		_db = db;
		_humans = humans;
		_virtuals = virtuals;
	}

	public void Start()
	{
		Engine = GameEngine.CreateNew(_db, GameMode.Solo, _humans, _virtuals);
		Engine.EventRaised += e => EventReceived?.Invoke(e);
		// Virtuals seal early; resolve waits until the human bids too.
		SubmitVirtualBidsIfNeeded();
	}

	public void Submit(GameCommand command)
	{
		if (Engine is null)
			throw new InvalidOperationException("Session not started");

		if (command is UndoCommand)
		{
			if (!Engine.TryUndo(out var undoError))
				EventReceived?.Invoke(new ErrorEvent(command.PlayerId, undoError));
			else
				SubmitVirtualBidsIfNeeded();
			return;
		}

		Engine.PushUndoCheckpoint();
		if (!Engine.TryApply(command, out var error))
		{
			// Failed command should not consume undo slot.
			Engine.TryUndo(out _);
			EventReceived?.Invoke(new ErrorEvent(command.PlayerId, error));
			return;
		}

		SubmitVirtualBidsIfNeeded();
	}

	public bool CanUndo => Engine?.CanUndo == true;

	void SubmitVirtualBidsIfNeeded()
	{
		if (Engine is null)
			return;

		var state = Engine.State;
		if (state.Phase != TurnPhase.Auction || state.AuctionSubPhase != AuctionSubPhase.CollectingBids)
			return;

		foreach (var virtualPlayer in state.ActiveAuctionPlayers()
					 .Where(p => p.Role == SessionRole.VirtualOpponent)
					 .Where(p => !state.SealedBids.ContainsKey(p.PlayerId))
					 .ToList())
		{
			var cmd = _virtualAi.ChooseAuctionAction(Engine, virtualPlayer.PlayerId);
			Engine.TryApply(cmd, out _);
		}
	}

	public void Shutdown()
	{
		Engine = null;
	}
}
