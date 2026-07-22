using Godot;
using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Models;

namespace Namestnik.Net;

/// <summary>
/// Host-authoritative session. Engine runs only here; clients send commands via RPC
/// and receive state snapshots.
/// </summary>
public sealed class HostSession : IGameSession
{
	readonly CardDatabase _db;
	readonly NetworkManager _net;
	readonly int _expectedHumans;
	readonly Dictionary<int, int> _peerToPlayer = new();
	bool _matchStarted;

	public GameMode Mode => GameMode.Host;
	public GameEngine? Engine { get; private set; }
	public int LocalPlayerId => 0;

	public event Action<GameEvent>? EventReceived;

	public HostSession(CardDatabase db, NetworkManager net, int expectedHumans = 2)
	{
		_db = db;
		_net = net;
		_expectedHumans = Math.Clamp(expectedHumans, 2, 4);
	}

	public void Start()
	{
		if (_net.StartHost() != Error.Ok)
		{
			EventReceived?.Invoke(new ErrorEvent(-1, "Failed to start host"));
			return;
		}

		_peerToPlayer[1] = 0; // Godot server peer id is always 1
		_net.PeerJoined += OnPeerJoined;
		_net.PeerLeft += OnPeerLeft;
		_net.CommandReceived += OnRemoteCommand;

		// Lobby: single local human until the first remote peer joins.
		Engine = GameEngine.CreateNew(_db, GameMode.Host, humanCount: 1, virtualCount: 0);
		Engine.EventRaised += OnEngineEvent;
		EventReceived?.Invoke(new LogEvent(
			$"Host ready on port {NetworkManager.DefaultPort}. Waiting for players (target {_expectedHumans})."));
	}

	void OnPeerJoined(int peerId)
	{
		if (_peerToPlayer.ContainsKey(peerId))
			return;

		var nextPlayerId = _peerToPlayer.Count;
		_peerToPlayer[peerId] = nextPlayerId;
		EventReceived?.Invoke(new LogEvent($"Peer {peerId} → player {nextPlayerId}"));

		// First remote peer: start a 2-human match (host=0, guest=1).
		if (!_matchStarted && _peerToPlayer.Count >= 2)
		{
			BeginMatchWithPlayers(Math.Min(_expectedHumans, _peerToPlayer.Count));
			return;
		}

		if (_matchStarted && Engine is not null)
		{
			_net.SendHello(peerId, nextPlayerId, Engine.State.Seed);
			_net.SendSnapshotTo(peerId, GameSnapshot.Serialize(Engine.State));
		}
	}

	void OnPeerLeft(int peerId)
	{
		if (_peerToPlayer.Remove(peerId))
			EventReceived?.Invoke(new LogEvent($"Peer {peerId} left"));
	}

	/// <summary>Call when lobby is full / host presses Start Match.</summary>
	public void BeginMatchWithPlayers(int humanCount)
	{
		if (Engine is not null)
			Engine.EventRaised -= OnEngineEvent;

		Engine = GameEngine.CreateNew(_db, GameMode.Host, humanCount, virtualCount: 0);
		Engine.EventRaised += OnEngineEvent;
		_matchStarted = true;

		EventReceived?.Invoke(new LogEvent($"Host match started with {humanCount} humans (seed {Engine.State.Seed})"));

		foreach (var (peerId, playerId) in _peerToPlayer)
		{
			if (peerId == 1)
				continue;
			_net.SendHello(peerId, playerId, Engine.State.Seed);
		}

		BroadcastSnapshot();
	}

	void OnEngineEvent(GameEvent e)
	{
		EventReceived?.Invoke(e);
		if (e is LogEvent log)
			_net.BroadcastLog(log.Message);
		else if (e is ErrorEvent err)
			_net.BroadcastLog($"ERROR: {err.Message}");
	}

	public void Submit(GameCommand command)
	{
		if (Engine is null)
			throw new InvalidOperationException("Session not started");

		// Host always plays as player 0.
		command = WithPlayerId(command, LocalPlayerId);

		if (!Engine.TryApply(command, out var error))
		{
			EventReceived?.Invoke(new ErrorEvent(command.PlayerId, error));
			return;
		}

		BroadcastSnapshot();
	}

	void OnRemoteCommand(int peerId, string commandJson)
	{
		if (Engine is null || !_matchStarted)
			return;

		if (!_peerToPlayer.TryGetValue(peerId, out var playerId))
		{
			_net.SendLogTo(peerId, "ERROR: No seat assigned");
			return;
		}

		try
		{
			var command = WithPlayerId(CommandSerializer.Deserialize(commandJson), playerId);
			if (!Engine.TryApply(command, out var error))
			{
				_net.SendLogTo(peerId, $"ERROR: {error}");
				EventReceived?.Invoke(new ErrorEvent(playerId, error));
				return;
			}

			BroadcastSnapshot();
		}
		catch (Exception ex)
		{
			_net.SendLogTo(peerId, $"ERROR: Bad command ({ex.Message})");
			GD.PrintErr($"Remote command from {peerId}: {ex}");
		}
	}

	void BroadcastSnapshot()
	{
		if (Engine is null)
			return;
		_net.BroadcastSnapshot(GameSnapshot.Serialize(Engine.State));
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

	public void Shutdown()
	{
		if (Engine is not null)
			Engine.EventRaised -= OnEngineEvent;
		Engine = null;
		_matchStarted = false;
		_peerToPlayer.Clear();
		_net.PeerJoined -= OnPeerJoined;
		_net.PeerLeft -= OnPeerLeft;
		_net.CommandReceived -= OnRemoteCommand;
		_net.Shutdown();
	}
}
