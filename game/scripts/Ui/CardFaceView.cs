using Godot;
using Namestnik.Core;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>Procedural card face: sectors, name, and level rewards as symbols.</summary>
public partial class CardFaceView : Control
{
	enum FaceKind
	{
		Back,
		Character,
		Law,
		Placeholder
	}

	FaceKind _kind = FaceKind.Back;
	CharacterCard? _character;
	LawCard? _law;
	int _placeholderId;

	static readonly Color Parchment = new(0.16f, 0.14f, 0.11f);
	static readonly Color ParchmentLaw = new(0.12f, 0.14f, 0.16f);
	static readonly Color Border = new(0.55f, 0.45f, 0.28f);
	static readonly Color Banner = new(0.05f, 0.04f, 0.03f, 0.82f);
	static readonly Color Ink = new(0.92f, 0.88f, 0.78f);
	static readonly Color InkDim = new(0.7f, 0.65f, 0.55f);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = true;
	}

	public void ShowBack()
	{
		_kind = FaceKind.Back;
		_character = null;
		_law = null;
		QueueRedraw();
	}

	public void ShowCharacter(CharacterCard card)
	{
		_kind = FaceKind.Character;
		_character = card;
		_law = null;
		QueueRedraw();
	}

	public void ShowLaw(LawCard card)
	{
		_kind = FaceKind.Law;
		_law = card;
		_character = null;
		QueueRedraw();
	}

	public void ShowPlaceholder(int definitionId)
	{
		_kind = FaceKind.Placeholder;
		_placeholderId = definitionId;
		_character = null;
		_law = null;
		QueueRedraw();
	}

	public void ShowFromDb(CardDatabase? db, CardKind kind, int definitionId)
	{
		if (db is null)
		{
			ShowPlaceholder(definitionId);
			return;
		}

		if (kind == CardKind.Character && db.Characters.TryGetValue(definitionId, out var ch))
			ShowCharacter(ch);
		else if (kind == CardKind.Law && db.Laws.TryGetValue(definitionId, out var law))
			ShowLaw(law);
		else
			ShowPlaceholder(definitionId);
	}

	public void CopyContentFrom(CardFaceView other)
	{
		_kind = other._kind;
		_character = other._character;
		_law = other._law;
		_placeholderId = other._placeholderId;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		var size = Size;
		if (size.X < 4 || size.Y < 4)
			return;

		switch (_kind)
		{
			case FaceKind.Back:
				DrawBack(size);
				break;
			case FaceKind.Character when _character is not null:
				DrawCharacter(_character, size);
				break;
			case FaceKind.Law when _law is not null:
				DrawLaw(_law, size);
				break;
			default:
				DrawPlaceholder(size);
				break;
		}
	}

	void DrawBack(Vector2 size)
	{
		DrawRect(new Rect2(Vector2.Zero, size), new Color(0.2f, 0.16f, 0.12f), true);
		var inset = size * 0.12f;
		DrawRect(new Rect2(inset, size - inset * 2), new Color(0.28f, 0.22f, 0.16f), false, 2f);
		var font = ThemeDB.FallbackFont;
		var fs = FontSize(size, 0.14f);
		DrawString(font, new Vector2(size.X * 0.5f, size.Y * 0.52f), "Н",
			HorizontalAlignment.Center, -1, fs, InkDim);
		DrawFrame(size);
	}

	void DrawPlaceholder(Vector2 size)
	{
		DrawRect(new Rect2(Vector2.Zero, size), Parchment, true);
		var font = ThemeDB.FallbackFont;
		var fs = FontSize(size, 0.12f);
		DrawString(font, new Vector2(size.X * 0.5f, size.Y * 0.5f), $"#{_placeholderId}",
			HorizontalAlignment.Center, -1, fs, Ink);
		DrawFrame(size);
	}

	void DrawCharacter(CharacterCard card, Vector2 size)
	{
		DrawRect(new Rect2(Vector2.Zero, size), Parchment, true);
		var sectorR = size.X * 0.2f;
		var font = ThemeDB.FallbackFont;

		var topPad = sectorR * 0.45f;
		var bottomPad = sectorR * 0.85f;
		var rowArea = new Rect2(
			size.X * 0.06f,
			topPad,
			size.X * 0.88f,
			Mathf.Max(8f, size.Y - topPad - bottomPad));

		var levels = card.Levels.OrderByDescending(l => l.Level).ToList();
		var rowH = rowArea.Size.Y / Math.Max(1, levels.Count);
		var fs = FontSize(size, 0.075f);

		for (var i = 0; i < levels.Count; i++)
		{
			var level = levels[i];
			var row = new Rect2(
				rowArea.Position.X,
				rowArea.Position.Y + i * rowH,
				rowArea.Size.X,
				rowH);
			DrawLevelRow(level, row, font, fs);
		}

		DrawSectors(card.Sectors, size, sectorR);
		DrawNamePlate(card.Name, size, sectorR, font);
		DrawId(card.Id, size, sectorR, font);
		DrawFrame(size);
	}

	void DrawLaw(LawCard card, Vector2 size)
	{
		DrawRect(new Rect2(Vector2.Zero, size), ParchmentLaw, true);
		var sectorR = size.X * 0.22f;
		var font = ThemeDB.FallbackFont;
		var titleFs = FontSize(size, 0.07f);
		var bodyFs = FontSize(size, 0.055f);
		var title = $"ЗАКОН #{card.Id}";
		DrawString(font, new Vector2(size.X * 0.5f, size.Y * 0.28f), title,
			HorizontalAlignment.Center, (int)(size.X * 0.85f), titleFs, Ink);

		var body = CardTooltips.FormatLawText(card.Text);
		body = Truncate(body, size.X < 100 ? 48 : 110);
		var bodyRect = new Rect2(
			size.X * 0.08f,
			size.Y * 0.34f,
			size.X * 0.84f,
			size.Y * 0.48f - sectorR * 0.5f);
		DrawString(font, bodyRect.Position + new Vector2(0, bodyFs), body,
			HorizontalAlignment.Left, (int)bodyRect.Size.X, bodyFs, InkDim);

		DrawSectors(card.Sectors, size, sectorR);
		DrawFrame(size);
	}

	void DrawLevelRow(LevelReward level, Rect2 row, Font font, int fontSize)
	{
		var gemR = Mathf.Min(row.Size.Y * 0.4f, row.Size.X * 0.11f);
		var gemCenter = new Vector2(row.Position.X + gemR + 2f, row.Position.Y + row.Size.Y * 0.5f);
		DrawCircle(gemCenter, gemR, CardSymbolPainter.GemTint(level.Cost));
		DrawArc(gemCenter, gemR, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.45f), 1.2f);

		var colonX = gemCenter.X + gemR + 3f;
		DrawString(font, new Vector2(colonX, gemCenter.Y + fontSize * 0.35f), ":",
			HorizontalAlignment.Left, -1, fontSize, InkDim);

		var iconSize = Mathf.Min(row.Size.Y * 0.78f, row.Size.X * 0.2f);
		var iconFs = Math.Max(fontSize, (int)(iconSize * 0.42f));
		var rewardOrigin = new Vector2(colonX + fontSize * 0.55f, row.Position.Y + (row.Size.Y - iconSize) * 0.5f);
		DrawReward(level.Reward, rewardOrigin, iconSize, font, iconFs, row.End.X - 2f);
	}

	float DrawReward(Reward reward, Vector2 origin, float iconSize, Font font, int fontSize, float maxX)
	{
		switch (reward)
		{
			case MultiReward multi:
			{
				var x = origin.X;
				foreach (var part in multi.Parts)
				{
					var w = MeasureReward(part, iconSize, fontSize);
					if (x + w > maxX)
						break;
					DrawReward(part, new Vector2(x, origin.Y), iconSize, font, fontSize, maxX);
					x += w + iconSize * 0.12f;
				}

				return Math.Max(0f, x - origin.X);
			}
			case ChoiceReward choice:
			{
				var x = origin.X;
				for (var i = 0; i < choice.Options.Count; i++)
				{
					if (i > 0)
					{
						var sep = fontSize * 0.55f;
						if (x + sep > maxX)
							break;
						DrawString(font, new Vector2(x, origin.Y + iconSize * 0.75f), "/",
							HorizontalAlignment.Left, -1, fontSize, InkDim);
						x += sep;
					}

					var w = MeasureReward(choice.Options[i], iconSize, fontSize);
					if (x + w > maxX)
						break;
					DrawReward(choice.Options[i], new Vector2(x, origin.Y), iconSize, font, fontSize, maxX);
					x += w + iconSize * 0.08f;
				}

				return Math.Max(0f, x - origin.X);
			}
			case VpReward vp:
				CardSymbolPainter.DrawVp(this, origin, iconSize, vp.Amount, font, fontSize);
				return iconSize * 1.15f;
			case GemsReward g:
				CardSymbolPainter.DrawGems(this, origin, iconSize, g.Amount, font, fontSize);
				return iconSize * 1.2f;
			case CardReward c:
				CardSymbolPainter.DrawCardIcon(this, origin, iconSize, c.Amount, font, fontSize);
				return iconSize * 1.1f;
			case ScienceReward s:
				CardSymbolPainter.DrawScience(this, origin, iconSize, s.Amount, font, fontSize);
				return iconSize * 1.1f;
			case MagicReward m:
				CardSymbolPainter.DrawMagic(this, origin, iconSize, m.Amount, font, fontSize);
				return iconSize * 1.15f;
			case DefenseReward d:
				CardSymbolPainter.DrawShield(this, origin, iconSize, d.Amount, font, fontSize);
				return iconSize * 1.05f;
			case AttackReward a:
				CardSymbolPainter.DrawAttack(this, origin, iconSize, a.Amount, font, fontSize);
				return iconSize * 1.05f;
			case InfiniteReward inf:
				CardSymbolPainter.DrawInfinite(this, origin, iconSize, inf.Color, font, fontSize);
				return iconSize * 1.25f;
			case BonusMagicReward b:
				CardSymbolPainter.DrawBonusMagic(this, origin, iconSize, b.Amount, font, fontSize);
				return iconSize * 1.35f;
			case BonusCircleReward bc:
				CardSymbolPainter.DrawBonusCircle(this, origin, iconSize, bc.Color, bc.Amount, font, fontSize);
				return iconSize * 1.35f;
			default:
				return iconSize;
		}
	}

	static float MeasureReward(Reward reward, float iconSize, int fontSize) => reward switch
	{
		VpReward => iconSize * 1.15f,
		GemsReward => iconSize * 1.2f,
		CardReward => iconSize * 1.1f,
		ScienceReward => iconSize * 1.1f,
		MagicReward => iconSize * 1.15f,
		DefenseReward => iconSize * 1.05f,
		AttackReward => iconSize * 1.05f,
		InfiniteReward => iconSize * 1.25f,
		BonusMagicReward => iconSize * 1.35f,
		BonusCircleReward => iconSize * 1.35f,
		MultiReward m => m.Parts.Sum(p => MeasureReward(p, iconSize, fontSize) + iconSize * 0.12f),
		ChoiceReward c => c.Options.Sum(p => MeasureReward(p, iconSize, fontSize) + fontSize * 0.6f),
		_ => iconSize
	};

	void DrawSectors(SectorColors sectors, Vector2 size, float radius)
	{
		// Top: corner quarters (meet the bottom semicircle of a card above in the pyramid).
		DrawPie(sectors.Tl, new Vector2(0, 0), radius, 0f, Mathf.Pi * 0.5f);
		DrawPie(sectors.Tr, new Vector2(size.X, 0), radius, Mathf.Pi * 0.5f, Mathf.Pi);

		// Bottom: one semicircle on the bottom edge center (left/right halves = bl/br).
		var bottomCenter = new Vector2(size.X * 0.5f, size.Y);
		if (sectors.Bl == sectors.Br)
			DrawPie(sectors.Bl, bottomCenter, radius, Mathf.Pi, Mathf.Tau);
		else
		{
			DrawPie(sectors.Bl, bottomCenter, radius, Mathf.Pi, Mathf.Pi * 1.5f);
			DrawPie(sectors.Br, bottomCenter, radius, Mathf.Pi * 1.5f, Mathf.Tau);
		}
	}

	void DrawPie(GemColor color, Vector2 center, float radius, float a0, float a1)
	{
		const int segments = 14;
		var pts = new Vector2[segments + 2];
		pts[0] = center;
		for (var i = 0; i <= segments; i++)
		{
			var t = i / (float)segments;
			var a = Mathf.Lerp(a0, a1, t);
			pts[i + 1] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
		}

		DrawColoredPolygon(pts, CardSymbolPainter.GemTint(color));
		DrawArc(center, radius, a0, a1, segments, new Color(0, 0, 0, 0.35f), 1.5f);
	}

	void DrawNamePlate(string name, Vector2 size, float sectorR, Font font)
	{
		var fs = FontSize(size, 0.065f);
		var display = name.Length > 10 && size.X < 110 ? name[..9] + "…" : name;
		var plateW = size.X * 0.42f;
		var plateH = Mathf.Max(fs + 6f, size.Y * 0.1f);
		var x = size.X - plateW - 3f;
		var y = size.Y - plateH - 2f;
		// Keep clear of the bottom semicircle.
		if (x < size.X * 0.5f + sectorR * 0.15f)
			x = size.X * 0.5f + sectorR * 0.15f;
		DrawRect(new Rect2(x, y, size.X - x - 2f, plateH), Banner, true);
		DrawString(font, new Vector2(x + (size.X - x - 2f) * 0.5f, y + plateH * 0.72f), display,
			HorizontalAlignment.Center, (int)(size.X - x - 4f), fs, Ink);
	}

	void DrawId(int id, Vector2 size, float sectorR, Font font)
	{
		var fs = Math.Max(7, FontSize(size, 0.045f));
		var x = Mathf.Min(size.X * 0.08f, size.X * 0.5f - sectorR - 4f);
		if (x < 4f) x = 4f;
		DrawString(font, new Vector2(x, size.Y - 4f), id.ToString(),
			HorizontalAlignment.Left, -1, fs, InkDim);
	}

	void DrawFrame(Vector2 size)
	{
		DrawRect(new Rect2(1, 1, size.X - 2, size.Y - 2), Border, false, 2f);
	}

	static int FontSize(Vector2 size, float fraction) =>
		Math.Max(7, (int)(Mathf.Min(size.X, size.Y) * fraction));

	static string Truncate(string text, int max)
	{
		text = text.Replace('\n', ' ').Trim();
		return text.Length <= max ? text : text[..(max - 1)] + "…";
	}
}
