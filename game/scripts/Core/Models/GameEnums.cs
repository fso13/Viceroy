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
