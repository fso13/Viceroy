using Godot;
using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Events;
using Namestnik.Core.Models;
using Namestnik.Ui;

namespace Namestnik.Net;

/// <summary>Global entry: owns CardDatabase and the active session.</summary>
public partial class GameSessionAutoload : Node
{
	public CardDatabase? Cards { get; private set; }
	public IGameSession? Session { get; private set; }
	public GameMode? ActiveMode => Session?.Mode;

	[Signal] public delegate void SessionEventEventHandler(string message);
	[Signal] public delegate void SessionStartedEventHandler();

	public override void _Ready()
	{
		DisplaySettings.LoadAndApply();

		try
		{
			using var file = Godot.FileAccess.Open("res://data/cards.json", Godot.FileAccess.ModeFlags.Read)
				?? throw new InvalidOperationException("Cannot open res://data/cards.json");
			Cards = CardDatabase.LoadFromJson(file.GetAsText());
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to load cards: {ex.Message}");
		}
	}

	public void StartSolo(int virtualOpponents = 1)
	{
		ShutdownSession();
		if (Cards is null)
			throw new InvalidOperationException("Card database missing");

		var session = new LocalSession(Cards, humans: 1, virtuals: virtualOpponents);
		Wire(session);
		session.Start();
		EmitSignal(SignalName.SessionStarted);
	}

	public void StartHost(int expectedHumans = 2)
	{
		ShutdownSession();
		if (Cards is null)
			throw new InvalidOperationException("Card database missing");

		var net = GetNode<NetworkManager>("/root/NetworkManager");
		var session = new HostSession(Cards, net, expectedHumans);
		Wire(session);
		session.Start();
		EmitSignal(SignalName.SessionStarted);
	}

	public void StartClient(string address, int port = NetworkManager.DefaultPort)
	{
		ShutdownSession();
		if (Cards is null)
			throw new InvalidOperationException("Card database missing");

		var net = GetNode<NetworkManager>("/root/NetworkManager");
		var session = new ClientSession(Cards, net, address, port);
		Wire(session);
		session.Start();
		EmitSignal(SignalName.SessionStarted);
	}

	void Wire(IGameSession session)
	{
		Session = session;
		session.EventReceived += OnEvent;
	}

	void OnEvent(GameEvent e)
	{
		var text = e switch
		{
			LogEvent log => log.Message,
			ErrorEvent err => $"ERROR: {err.Message}",
			PhaseChangedEvent phase => $"Phase → {phase.Phase} (turn {phase.Turn})",
			MatchStartedEvent start => $"Match started (seed {start.Seed}, mode {start.Mode})",
			MatchEndedEvent ended => ended.Result.IsDraw
				? $"Ничья ({string.Join(", ", ended.Result.Scores.Select(s => $"{s.DisplayName}:{s.Total}"))})"
				: $"Победа: {ended.Result.Scores.First(s => ended.Result.WinnerIds.Contains(s.PlayerId)).DisplayName} " +
				  $"({ended.Result.Scores.First(s => ended.Result.WinnerIds.Contains(s.PlayerId)).Total})",
			_ => e.GetType().Name
		};
		GD.Print($"[Session] {text}");
		EmitSignal(SignalName.SessionEvent, text);
	}

	public void Submit(GameCommand command) => Session?.Submit(command);

	public void ShutdownSession()
	{
		Session?.Shutdown();
		Session = null;
	}

	public override void _ExitTree() => ShutdownSession();
}
