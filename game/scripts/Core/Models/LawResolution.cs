namespace Namestnik.Core.Models;

public enum LawPromptKind
{
	/// <summary>Pick option index 0..n-1 for choose-one laws.</summary>
	ChooseOption,

	/// <summary>Pick gem color for infinite reward.</summary>
	ChooseInfiniteColor,

	/// <summary>Return exactly 2 law cards from hand to top of law deck (indices).</summary>
	ReturnLawsToDeck,

	/// <summary>Take one face-up auction character to hand.</summary>
	TakeAuctionCard,

	/// <summary>Park 0..8 gems from screen onto this law.</summary>
	ParkGems,

	/// <summary>Optional token swap (67) — decline or confirm later; MVP: decline/skip.</summary>
	OptionalSwapDecline,

	/// <summary>Law 78: offer to tuck a just-drawn card.</summary>
	OfferTuckDrawnCard
}

public sealed class PendingLawResolution
{
	public required int PlayerId { get; init; }
	public required int LawInstanceId { get; init; }
	public required int LawDefinitionId { get; init; }
	public required LawPromptKind Kind { get; set; }

	/// <summary>For ChooseOption — human-readable labels.</summary>
	public List<string> OptionLabels { get; init; } = new();

	/// <summary>Drawn card instance waiting for law 78 tuck decision.</summary>
	public int? DrawnInstanceId { get; set; }

	/// <summary>After ChooseOption selected INF path.</summary>
	public bool AwaitingInfiniteColor { get; set; }
}

public static class LawIds
{
	public const int Replace = 65;
	public const int TuckUnderCharacter = 66;
	public const int OptionalSwap = 67;
	public const int SkipAuction = 69;
	public const int DrawLawsReturn2 = 70;
	public const int ChooseAtkVpGems = 71;
	public const int TuckFromHand = 72;
	public const int ChooseDefInfGems = 73;
	public const int TuckFreeCard = 74;
	public const int ParkGems = 76;
	public const int ChooseAtkCardGems = 77;
	public const int TuckOnDraw = 78;
	public const int ChooseDefVpGems = 80;
	public const int ChooseMagSciGems = 81;
	public const int ChooseSciCardGems = 82;
	public const int ChooseMagBonusInfGems = 83;
	public const int ChooseBonusCardGems = 84;

	public static bool IsChoiceLaw(int id) => id is
		ChooseAtkVpGems or ChooseDefInfGems or ChooseAtkCardGems or ChooseDefVpGems
		or ChooseMagSciGems or ChooseSciCardGems or ChooseMagBonusInfGems or ChooseBonusCardGems;

	public static bool NeedsTargetOnPlay(int id) => id is
		Replace or TuckUnderCharacter or TuckFreeCard;

	public static bool IsPassiveEndGameOnly(int id) => id is 68 or 75 or 79 or 85 or 86 or 87 or 88;
}
