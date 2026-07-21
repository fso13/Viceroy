using Godot;

namespace Namestnik.Net;

/// <summary>
/// ENet host/client bootstrap + host-authoritative RPCs (commands in, snapshots/logs out).
/// </summary>
public partial class NetworkManager : Node
{
	public const int DefaultPort = 7777;
	public const int MaxClients = 3; // + host = 4

	public bool IsHost { get; private set; }
	public bool HasActivePeer =>
		Multiplayer.HasMultiplayerPeer() &&
		Multiplayer.MultiplayerPeer.GetConnectionStatus() ==
		MultiplayerPeer.ConnectionStatus.Connected;

	/// <summary>Host only: remote peer submitted a command JSON.</summary>
	public event Action<int, string>? CommandReceived;

	/// <summary>Client only: host pushed a full state snapshot JSON.</summary>
	public event Action<string>? SnapshotReceived;

	/// <summary>Client only: host pushed a log/error line.</summary>
	public event Action<string>? LogReceived;

	/// <summary>Client only: seat assignment (game playerId) + match seed.</summary>
	public event Action<int, int>? HelloReceived;

	[Signal] public delegate void HostStartedEventHandler();
	[Signal] public delegate void ClientConnectedEventHandler();
	[Signal] public delegate void ConnectionFailedEventHandler(string reason);
	[Signal] public delegate void PeerJoinedEventHandler(int peerId);
	[Signal] public delegate void PeerLeftEventHandler(int peerId);

	public Error StartHost(int port = DefaultPort)
	{
		Shutdown();
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateServer(port, MaxClients);
		if (err != Error.Ok)
		{
			EmitSignal(SignalName.ConnectionFailed, $"Cannot start host: {err}");
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		IsHost = true;
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		GD.Print($"Host listening on {port}");
		EmitSignal(SignalName.HostStarted);
		return Error.Ok;
	}

	public Error Join(string address, int port = DefaultPort)
	{
		Shutdown();
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateClient(address, port);
		if (err != Error.Ok)
		{
			EmitSignal(SignalName.ConnectionFailed, $"Cannot connect: {err}");
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		IsHost = false;
		Multiplayer.ConnectedToServer += () =>
		{
			GD.Print($"Connected to {address}:{port}");
			EmitSignal(SignalName.ClientConnected);
		};
		Multiplayer.ConnectionFailed += () =>
			EmitSignal(SignalName.ConnectionFailed, "Connection failed");
		Multiplayer.ServerDisconnected += () =>
			EmitSignal(SignalName.ConnectionFailed, "Server disconnected");
		return Error.Ok;
	}

	public void Shutdown()
	{
		if (Multiplayer.HasMultiplayerPeer())
		{
			Multiplayer.PeerConnected -= OnPeerConnected;
			Multiplayer.PeerDisconnected -= OnPeerDisconnected;
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null!;
		}

		IsHost = false;
	}

	// --- Outbound helpers ---

	public void SendCommandToHost(string commandJson) =>
		RpcId(1, MethodName.RpcReceiveCommand, commandJson);

	public void BroadcastSnapshot(string snapshotJson) =>
		Rpc(MethodName.RpcReceiveSnapshot, snapshotJson);

	public void SendSnapshotTo(int peerId, string snapshotJson) =>
		RpcId(peerId, MethodName.RpcReceiveSnapshot, snapshotJson);

	public void BroadcastLog(string message) =>
		Rpc(MethodName.RpcReceiveLog, message);

	public void SendLogTo(int peerId, string message) =>
		RpcId(peerId, MethodName.RpcReceiveLog, message);

	public void SendHello(int peerId, int playerId, int seed) =>
		RpcId(peerId, MethodName.RpcReceiveHello, playerId, seed);

	// --- RPC endpoints (must exist identically on all peers) ---

	/// <summary>Client → host: command JSON.</summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	void RpcReceiveCommand(string commandJson)
	{
		if (!IsHost)
			return;
		var sender = Multiplayer.GetRemoteSenderId();
		CommandReceived?.Invoke(sender, commandJson);
	}

	/// <summary>Host → clients: state snapshot JSON.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	void RpcReceiveSnapshot(string snapshotJson) =>
		SnapshotReceived?.Invoke(snapshotJson);

	/// <summary>Host → clients: log / error text.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	void RpcReceiveLog(string message) =>
		LogReceived?.Invoke(message);

	/// <summary>Host → one client: seat + seed.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	void RpcReceiveHello(int playerId, int seed) =>
		HelloReceived?.Invoke(playerId, seed);

	void OnPeerConnected(long id)
	{
		GD.Print($"Peer joined: {id}");
		EmitSignal(SignalName.PeerJoined, (int)id);
	}

	void OnPeerDisconnected(long id)
	{
		GD.Print($"Peer left: {id}");
		EmitSignal(SignalName.PeerLeft, (int)id);
	}
}
