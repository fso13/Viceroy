namespace Namestnik.Core.Models;

public static class GemAccounting
{
	/// <summary>
	/// Physical gems per color: reserve + virtual box + screens + infinites.
	/// Parked gems are colorless and tracked separately in <see cref="TotalParked"/>.
	/// </summary>
	public static int CountColor(GameState state, GemColor color)
	{
		var n = state.Reserve[color] + state.VirtualBox[color];
		foreach (var p in state.Players)
		{
			n += p.Screen[color];
			foreach (var card in p.Pyramid.AllCards)
			{
				if (card.InfiniteGem == color)
					n++;
			}
		}

		return n;
	}

	public static int TotalParked(GameState state) =>
		state.Players.Sum(p => p.Pyramid.AllCards.Sum(c => c.ParkedGems));

	public static int TotalPhysical(GameState state)
	{
		var total = 0;
		foreach (GemColor color in Enum.GetValues<GemColor>())
			total += CountColor(state, color);
		return total + TotalParked(state);
	}

	public static bool Conserved(GameState state) =>
		TotalPhysical(state) == GameState.PhysicalGemsPerColor * 4;
}
