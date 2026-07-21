using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Models;

namespace Namestnik.Net;

public interface IGameSession
{
	GameMode Mode { get; }
	GameEngine? Engine { get; }
	int LocalPlayerId { get; }

	/// <summary>Solo undo only; Host/Client default to false.</summary>
	bool CanUndo => false;

	event Action<GameEvent>? EventReceived;

	void Start();
	void Submit(GameCommand command);
	void Shutdown();
}
