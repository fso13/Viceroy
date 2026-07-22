using Godot;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>Legend of card symbols drawn with the same glyphs/colors as abstract cards.</summary>
public partial class SymbolLegendBar : HFlowContainer
{
	public override void _Ready()
	{
		AddThemeConstantOverride("h_separation", 8);
		AddThemeConstantOverride("v_separation", 2);
		Build();
	}

	void Build()
	{
		foreach (var child in GetChildren())
			child.QueueFree();

		AddChild(MakeTitle("Легенда:"));
		AddEntry(LegendGlyphKind.CostGem, "ур.", "стоимость уровня");
		AddEntry(LegendGlyphKind.Vp, "слава", "очки славы");
		AddEntry(LegendGlyphKind.Science, "наука", "наука (шестерёнка)");
		AddEntry(LegendGlyphKind.Magic, "магия", "магия (свиток)");
		AddEntry(LegendGlyphKind.Attack, "атака", "атака (меч)");
		AddEntry(LegendGlyphKind.Defense, "защита", "защита (щит)");
		AddEntry(LegendGlyphKind.Gems, "камни", "камни из резерва");
		AddEntry(LegendGlyphKind.Card, "карта", "взять карту");
		AddEntry(LegendGlyphKind.Infinite, "∞", "неисчерпаемый камень");
		AddEntry(LegendGlyphKind.BonusMagic, "+маг.", "бонус магии");
		AddEntry(LegendGlyphKind.BonusCircle, "+круг", "бонус одноцветного круга");
		AddEntry(LegendGlyphKind.Choice, "выбор", "выбор одной из наград");
	}

	void AddEntry(LegendGlyphKind kind, string caption, string tip)
	{
		var row = new HBoxContainer { TooltipText = tip };
		row.AddThemeConstantOverride("separation", 2);
		row.AddChild(new LegendGlyph { Kind = kind });
		var label = new Label
		{
			Text = caption,
			VerticalAlignment = VerticalAlignment.Center,
			TooltipText = tip
		};
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.68f));
		row.AddChild(label);
		AddChild(row);
	}

	static Label MakeTitle(string text)
	{
		var label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 0.75f));
		return label;
	}
}

enum LegendGlyphKind
{
	CostGem,
	Vp,
	Gems,
	Card,
	Science,
	Magic,
	Defense,
	Attack,
	Infinite,
	BonusMagic,
	BonusCircle,
	Choice
}

partial class LegendGlyph : Control
{
	public LegendGlyphKind Kind { get; set; }

	const float SizePx = 18f;

	public override void _Ready()
	{
		var width = Kind is LegendGlyphKind.Gems or LegendGlyphKind.Infinite
			or LegendGlyphKind.BonusMagic or LegendGlyphKind.BonusCircle
			? SizePx + 12f
			: SizePx;
		CustomMinimumSize = new Vector2(width, SizePx);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		var font = ThemeDB.FallbackFont;
		const int fs = 9;
		var origin = new Vector2(1, (Size.Y - SizePx) * 0.5f);
		switch (Kind)
		{
			case LegendGlyphKind.CostGem:
				CardSymbolPainter.DrawCostGem(this, origin + new Vector2(SizePx * 0.45f, SizePx * 0.5f),
					SizePx * 0.32f, GemColor.Green);
				break;
			case LegendGlyphKind.Vp:
				CardSymbolPainter.DrawVp(this, origin, SizePx, 12, font, fs);
				break;
			case LegendGlyphKind.Gems:
				CardSymbolPainter.DrawGems(this, origin, SizePx, 3, font, fs);
				break;
			case LegendGlyphKind.Card:
				CardSymbolPainter.DrawCardIcon(this, origin, SizePx, 1, font, fs);
				break;
			case LegendGlyphKind.Science:
				CardSymbolPainter.DrawScience(this, origin, SizePx, 1, font, fs);
				break;
			case LegendGlyphKind.Magic:
				CardSymbolPainter.DrawMagic(this, origin, SizePx, 1, font, fs);
				break;
			case LegendGlyphKind.Defense:
				CardSymbolPainter.DrawShield(this, origin, SizePx, 1, font, fs);
				break;
			case LegendGlyphKind.Attack:
				CardSymbolPainter.DrawAttack(this, origin, SizePx, 1, font, fs);
				break;
			case LegendGlyphKind.Infinite:
				CardSymbolPainter.DrawInfinite(this, origin, SizePx, GemColor.Blue, font, fs);
				break;
			case LegendGlyphKind.BonusMagic:
				CardSymbolPainter.DrawBonusMagic(this, origin, SizePx, 2, font, fs);
				break;
			case LegendGlyphKind.BonusCircle:
				CardSymbolPainter.DrawBonusCircle(this, origin, SizePx, GemColor.Yellow, 3, font, fs);
				break;
			case LegendGlyphKind.Choice:
				CardSymbolPainter.DrawChoiceSlash(this, origin, SizePx, font, fs);
				break;
		}
	}
}
