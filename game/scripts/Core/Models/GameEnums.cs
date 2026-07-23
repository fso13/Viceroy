namespace Namestnik.Core.Models;

public enum GameMode
{
	Solo,
	Host,
	Client
}

public enum TurnPhase
{
	Setup,
	Auction,
	Development,
	/// <summary>End-game: paint pyramid sectors with screen gems before scoring.</summary>
	Recolor,
	GameOver
}

public enum AuctionRound
{
	None = 0,
	First = 1,
	Second = 2,
	Third = 3
}

public enum SessionRole
{
	LocalHuman,
	VirtualOpponent,
	RemoteHuman
}
