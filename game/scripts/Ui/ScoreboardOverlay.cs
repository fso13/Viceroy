using Godot;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>End-of-match scoreboard: place, attribute breakdown, totals.</summary>
public partial class ScoreboardOverlay : Control
{
	static readonly Color Dim = new(0, 0, 0, 0.82f);
	static readonly Color PanelBg = new(0.12f, 0.11f, 0.09f, 0.98f);
	static readonly Color PanelBorder = new(0.75f, 0.62f, 0.32f);
	static readonly Color HeaderFg = new(0.92f, 0.84f, 0.55f);
	static readonly Color CellFg = new(0.88f, 0.86f, 0.8f);
	static readonly Color TotalFg = new(0.98f, 0.92f, 0.55f);
	static readonly Color LocalRowBg = new(0.32f, 0.26f, 0.12f, 0.95f);
	static readonly Color LocalRowBorder = new(0.95f, 0.78f, 0.28f);
	static readonly Color AltRowBg = new(0.16f, 0.15f, 0.13f, 0.7f);
	static readonly Color WinnerFg = new(0.55f, 0.95f, 0.55f);
	static readonly Color PenaltyFg = new(1f, 0.55f, 0.45f);
	static readonly Color LocalNameFg = new(1f, 0.92f, 0.55f);

	static readonly (string Title, float Width)[] Columns =
	[
		("Место", 54),
		("Игрок", 150),
		("Круги", 58),
		("∞", 48),
		("Законы", 64),
		("VP", 48),
		("Магия", 58),
		("Наборы", 64),
		("Штраф", 58),
		("Итого", 64)
	];

	ColorRect _dim = null!;
	VBoxContainer _table = null!;
	Label _title = null!;
	Label _notes = null!;
	Action? _onMenu;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;
		ZIndex = 120;

		var dim = new ColorRect
		{
			Color = Dim,
			MouseFilter = MouseFilterEnum.Stop
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);
		_dim = dim;

		var panel = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Stop,
			CustomMinimumSize = new Vector2(860, 0)
		};
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical = GrowDirection.Both;
		var style = new StyleBoxFlat
		{
			BgColor = PanelBg,
			BorderColor = PanelBorder
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(10);
		style.SetContentMarginAll(20);
		panel.AddThemeStyleboxOverride("panel", style);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 14);

		_title = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Text = "Итоги партии"
		};
		_title.AddThemeFontSizeOverride("font_size", 24);
		_title.AddThemeColorOverride("font_color", HeaderFg);

		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 240),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};

		_table = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_table.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(_table);

		_notes = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Visible = false
		};
		_notes.AddThemeFontSizeOverride("font_size", 12);
		_notes.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.65f));

		var menuButton = new Button
		{
			Text = "В меню",
			CustomMinimumSize = new Vector2(0, 42)
		};
		menuButton.Pressed += () => _onMenu?.Invoke();

		root.AddChild(_title);
		root.AddChild(scroll);
		root.AddChild(_notes);
		root.AddChild(menuButton);
		panel.AddChild(root);
		AddChild(panel);
	}

	public void ShowResult(MatchResult result, int localPlayerId, Action onMenu)
	{
		if (!IsNodeReady() || _title is null)
			_Ready();

		_onMenu = onMenu;

		var ranked = result.Scores
			.OrderByDescending(s => s.Total)
			.ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (ranked.Count == 0)
		{
			_title!.Text = "Итоги партии";
			Visible = true;
			MoveToFront();
			return;
		}

		_title!.Text = result.IsDraw
			? "Ничья — итоговая таблица"
			: $"Победитель: {ranked.FirstOrDefault(s => result.WinnerIds.Contains(s.PlayerId))?.DisplayName ?? ranked[0].DisplayName}";

		foreach (var child in _table.GetChildren())
			child.QueueFree();

		_table.AddChild(MakeHeaderRow());

		var place = 0;
		var lastTotal = int.MaxValue;
		var index = 0;
		foreach (var score in ranked)
		{
			index++;
			if (score.Total != lastTotal)
			{
				place = index;
				lastTotal = score.Total;
			}

			_table.AddChild(MakePlayerRow(
				place,
				score,
				isLocal: score.PlayerId == localPlayerId,
				isWinner: result.WinnerIds.Contains(score.PlayerId),
				index));
		}

		var local = ranked.FirstOrDefault(s => s.PlayerId == localPlayerId);
		if (local is { Notes.Count: > 0 })
		{
			_notes.Visible = true;
			_notes.Text = "Ваши детали: " + string.Join(" · ", local.Notes);
		}
		else
		{
			_notes.Visible = false;
			_notes.Text = "";
		}

		Visible = true;
		MoveToFront();
	}

	public void HideBoard()
	{
		Visible = false;
		_onMenu = null;
	}

	Control MakeHeaderRow()
	{
		var tips = new[]
		{
			"Место по сумме очков",
			"Имя игрока",
			"Очки за одноцветные круги",
			"Очки за неисчерпаемые камни",
			"Очки за законы",
			"Жетоны победы на картах",
			"Магия × бонус магии",
			"Наборы защита/магия/наука × 12",
			"Штраф за чужие атаки",
			"Итоговая сумма"
		};
		var row = MakeRowBox();
		for (var i = 0; i < Columns.Length; i++)
		{
			var cell = MakeCell(Columns[i].Title, Columns[i].Width, header: true, numeric: i >= 2);
			cell.TooltipText = tips[i];
			row.AddChild(cell);
		}

		return WrapRow(row, isLocal: false, alt: false, header: true);
	}

	Control MakePlayerRow(int place, ScoreBreakdown score, bool isLocal, bool isWinner, int index)
	{
		var row = MakeRowBox();
		var name = isLocal ? $"★ {score.DisplayName}" : score.DisplayName;
		row.AddChild(MakeCell($"{place}", Columns[0].Width, winner: isWinner));
		row.AddChild(MakeCell(name, Columns[1].Width, local: isLocal, winner: isWinner));
		row.AddChild(MakeCell($"{score.Circles}", Columns[2].Width, numeric: true));
		row.AddChild(MakeCell($"{score.Infinites}", Columns[3].Width, numeric: true));
		row.AddChild(MakeCell($"{score.Laws}", Columns[4].Width, numeric: true));
		row.AddChild(MakeCell($"{score.VpTokens}", Columns[5].Width, numeric: true));
		row.AddChild(MakeCell($"{score.Magic}", Columns[6].Width, numeric: true));
		row.AddChild(MakeCell($"{score.Sets}", Columns[7].Width, numeric: true));
		row.AddChild(MakeCell(
			score.AttackPenalty > 0 ? $"−{score.AttackPenalty}" : "0",
			Columns[8].Width,
			numeric: true,
			penalty: score.AttackPenalty > 0));
		row.AddChild(MakeCell(
			$"{score.Total}",
			Columns[9].Width,
			numeric: true,
			total: true,
			winner: isWinner));
		return WrapRow(row, isLocal, alt: index % 2 == 0, header: false);
	}

	static HBoxContainer MakeRowBox()
	{
		var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 8);
		return row;
	}

	static Control WrapRow(Control content, bool isLocal, bool alt, bool header)
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var style = new StyleBoxFlat();
		if (header)
		{
			style.BgColor = new Color(0.18f, 0.16f, 0.12f, 0.95f);
			style.BorderColor = new Color(0.45f, 0.4f, 0.28f);
			style.BorderWidthBottom = 1;
		}
		else if (isLocal)
		{
			style.BgColor = LocalRowBg;
			style.BorderColor = LocalRowBorder;
			style.SetBorderWidthAll(1);
			style.SetCornerRadiusAll(6);
		}
		else
		{
			style.BgColor = alt ? AltRowBg : new Color(0, 0, 0, 0);
		}

		style.SetContentMarginAll(header ? 8 : 7);
		panel.AddThemeStyleboxOverride("panel", style);
		panel.AddChild(content);
		return panel;
	}

	static Label MakeCell(
		string text,
		float width,
		bool header = false,
		bool numeric = false,
		bool local = false,
		bool winner = false,
		bool total = false,
		bool penalty = false)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(width, 0),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
		};
		label.AddThemeFontSizeOverride("font_size", header ? 12 : 14);

		if (header)
			label.AddThemeColorOverride("font_color", HeaderFg);
		else if (penalty)
			label.AddThemeColorOverride("font_color", PenaltyFg);
		else if (total && winner)
			label.AddThemeColorOverride("font_color", WinnerFg);
		else if (total)
			label.AddThemeColorOverride("font_color", TotalFg);
		else if (local)
			label.AddThemeColorOverride("font_color", LocalNameFg);
		else if (winner)
			label.AddThemeColorOverride("font_color", WinnerFg);
		else
			label.AddThemeColorOverride("font_color", CellFg);

		return label;
	}
}
