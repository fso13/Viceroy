using Godot;
using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Models;

namespace Namestnik.Net;

/// <summary>
/// Thin client: local engine is a mirror fed by host snapshots; commands go to host via RPC.
/// </summary>
public sealed class ClientSession : IGameSession
{
	readonly CardDatabase _db;
	readonly NetworkManager _net;
	readonly string _address;
	readonly int _port;

	public GameMode Mode => GameMode.Client;
	public GameEngine? Engine { get; private set; }
	public int LocalPlayerId { get; private set; } = -1;

	public event Action<GameEvent>? EventReceived;

	public ClientSession(
		CardDatabase db,
		NetworkManager net,
		string address,
		int port = NetworkManager.DefaultPort)
	{
		_db = db;
		_net = net;
		_address = address;
		_port = port;
	}

	public void Start()
	{
		_net.ClientConnected += OnConnected;
		_net.ConnectionFailed += OnFailed;
		_net.HelloReceived += OnHello;
		_net.SnapshotReceived += OnSnapshot;
		_net.LogReceived += OnRemoteLog;

		if (_net.Join(_address, _port) != Error.Ok)
			EventReceived?.Invoke(new ErrorEvent(-1, "Join failed"));
	}

	void OnConnected()
	{
		EventReceived?.Invoke(new LogEvent(
			$"Joined host {_address}:{_port} as peer {_net.Multiplayer.GetUniqueId()}. Waiting for seat…"));
	}

	void OnHello(int playerId, int seed)
	{
		LocalPlayerId = playerId;
		EventReceived?.Invoke(new LogEvent($"Seat assigned: player {playerId} (seed {seed})"));
	}

	void OnSnapshot(string snapshotJson)
	{
		try
		{
			var state = GameSnapshot.Deserialize(snapshotJson, GameMode.Client);

			if (Engine is null)
			{
				Engine = new GameEngine(_db, state);
				Engine.EventRaised += ForwardEngineEvent;
				EventReceived?.Invoke(new LogEvent("Снапшот получен от хоста"));
				EventReceived?.Invoke(new PhaseChangedEvent(state.Phase, state.Turn));
			}
			else
			{
				Engine.LoadState(state);
			}

			if (LocalPlayerId < 0 && state.Players.Count > 1)
				LocalPlayerId = 1;
		}
		catch (Exception ex)
		{
			EventReceived?.Invoke(new ErrorEvent(-1, $"Bad snapshot: {ex.Message}"));
			GD.PrintErr(ex);
		}
	}

	void OnRemoteLog(string message)
	{
		if (message.StartsWith("ERROR:", StringComparison.Ordinal))
			EventReceived?.Invoke(new ErrorEvent(LocalPlayerId, message["ERROR:".Length..].Trim()));
		else
			EventReceived?.Invoke(new LogEvent(message));
	}

	void OnFailed(string reason)
	{
		EventReceived?.Invoke(new ErrorEvent(-1, reason));
	}

	public void Submit(GameCommand command)
	{
		if (!_net.HasActivePeer)
		{
			EventReceived?.Invoke(new ErrorEvent(LocalPlayerId, "Not connected"));
			return;
		}

		if (LocalPlayerId < 0)
		{
			EventReceived?.Invoke(new ErrorEvent(-1, "Seat not assigned yet"));
			return;
		}

		try
		{
			command = WithPlayerId(command, LocalPlayerId);
			_net.SendCommandToHost(CommandSerializer.Serialize(command));
		}
		catch (Exception ex)
		{
			EventReceived?.Invoke(new ErrorEvent(LocalPlayerId, $"Cannot send: {ex.Message}"));
		}
	}

	static GameCommand WithPlayerId(GameCommand command, int playerId) => command switch
	{
		SubmitAuctionBidCommand c => c with { PlayerId = playerId },
		PassAuctionCommand => new PassAuctionCommand(playerId),
		BidGemCommand c => c with { PlayerId = playerId },
		BidAttackCommand c => c with { PlayerId = playerId },
		ChooseAuctionCardCommand c => c with { PlayerId = playerId },
		PlayCardCommand c => c with { PlayerId = playerId },
		DiscardForGemsCommand c => c with { PlayerId = playerId },
		PassDevelopmentCommand => new PassDevelopmentCommand(playerId),
		ChooseLevel5RewardCommand c => c with { PlayerId = playerId },
		ResolveLawCommand c => c with { PlayerId = playerId },
		ClaimPassGemsCommand c => c with { PlayerId = playerId },
		ChooseRewardCommand c => c with { PlayerId = playerId },
		ChooseDeckDrawCommand c => c with { PlayerId = playerId },
		UndoCommand => new UndoCommand(playerId),
		ResolveTokenSwapCommand c => c with { PlayerId = playerId },
		_ => command
	};

	void ForwardEngineEvent(GameEvent e) => EventReceived?.Invoke(e);

	public void Shutdown()
	{
		if (Engine is not null)
			Engine.EventRaised -= ForwardEngineEvent;
		Engine = null;
		LocalPlayerId = -1;
		_net.ClientConnected -= OnConnected;
		_net.ConnectionFailed -= OnFailed;
		_net.HelloReceived -= OnHello;
		_net.SnapshotReceived -= OnSnapshot;
		_net.LogReceived -= OnRemoteLog;
		_net.Shutdown();
	}
}
