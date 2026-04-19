using System;
using Godot;
using STS2Agent.Models;
using STS2Agent.Services;

namespace STS2Agent.UI;

public partial class CardTooltipNode : Control
{
    private const float FadeDuration = 0.2f;

    private bool _isVisible;
    private bool _disposed;

    // UI nodes
    private Panel? _bg;
    private VBoxContainer? _mainBox;
    private Label? _nameLabel;
    private HBoxContainer? _badgesBox;
    private HBoxContainer? _barWrapper;
    private Panel? _barBg;
    private Panel? _barFill;
    private Label? _barText;
    private VBoxContainer? _statRow;
    private Label? _noDataLabel;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 1000;

        BuildUIPureCode();
        GlobalPosition = new Vector2(-9999, -9999);
        Hide();

        Logger.Info("[Tooltip] CardTooltipNode initialized");
    }

    private void BuildUIPureCode()
    {
        // ── Background Panel ──────────────────────────────────────────────
        _bg = new Panel { Name = "Background" };
        _bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _bg.AddThemeStyleboxOverride("panel", CreateBgStyle());
        AddChild(_bg);

        // ── Main VBox ──────────────────────────────────────────────────────
        _mainBox = new VBoxContainer { Name = "MainBox" };
        _mainBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _mainBox.OffsetLeft = 12;
        _mainBox.OffsetTop = 10;
        _mainBox.OffsetRight = -12;
        _mainBox.OffsetBottom = -10;
        _mainBox.AddThemeConstantOverride("separation", 8);
        AddChild(_mainBox);

        // ── Name Label ──────────────────────────────────────────────────────
        _nameLabel = new Label { Name = "NameLabel" };
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _nameLabel.VerticalAlignment = VerticalAlignment.Center;
        _nameLabel.AddThemeFontSizeOverride("font_size", 15);
        _mainBox.AddChild(_nameLabel);

        // ── Badges HBox (#Rank badge) ───────────────────────────────────────
        _badgesBox = new HBoxContainer { Name = "BadgesBox" };
        _badgesBox.AddThemeConstantOverride("separation", 6);
        _mainBox.AddChild(_badgesBox);

        // ── BarWrapper (PickRate) ───────────────────────────────────────────
        //   ├─ BarTitle   ("拾取")
        //   ├─ BarFill    (proportional fill, colored)
        //   ├─ BarBg      (remainder, grey)
        //   └─ BarText    ("45.2%")
        _barWrapper = new HBoxContainer { Name = "BarWrapper" };
        _barWrapper.AddThemeConstantOverride("separation", 6);
        _mainBox.AddChild(_barWrapper);

        var barTitle = new Label { Name = "BarTitle", Text = "拾取" };
        barTitle.HorizontalAlignment = HorizontalAlignment.Left;
        barTitle.VerticalAlignment = VerticalAlignment.Center;
        barTitle.AddThemeFontSizeOverride("font_size", 10);
        barTitle.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f, 0.8f));
        _barWrapper.AddChild(barTitle);

        _barFill = new Panel { Name = "BarFill", CustomMinimumSize = new Vector2(0, 8) };
        _barFill.AddThemeStyleboxOverride("panel", CreateBarFillStyle(0.5f));
        _barWrapper.AddChild(_barFill);

        _barBg = new Panel { Name = "BarBg", CustomMinimumSize = new Vector2(0, 8) };
        _barBg.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _barBg.AddThemeStyleboxOverride("panel", CreateBarBgStyle());
        _barWrapper.AddChild(_barBg);

        _barText = new Label { Name = "BarText" };
        _barText.HorizontalAlignment = HorizontalAlignment.Right;
        _barText.VerticalAlignment = VerticalAlignment.Center;
        _barText.AddThemeFontSizeOverride("font_size", 10);
        _barText.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 0.9f));
        _barText.CustomMinimumSize = new Vector2(36, 0);
        _barWrapper.AddChild(_barText);

        // ── StatRow (WinRateDelta + HoldStrength) ───────────────────────────
        _statRow = new VBoxContainer { Name = "StatRow" };
        _statRow.AddThemeConstantOverride("separation", 4);
        _mainBox.AddChild(_statRow);

        // ── NoData fallback ────────────────────────────────────────────────
        _noDataLabel = new Label { Name = "NoDataLabel", Text = "暂无数据", Visible = false };
        _noDataLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _noDataLabel.VerticalAlignment = VerticalAlignment.Center;
        _noDataLabel.AddThemeFontSizeOverride("font_size", 11);
        _noDataLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f, 0.7f));
        _mainBox.AddChild(_noDataLabel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Style Factories
    // ═══════════════════════════════════════════════════════════════════════

    private StyleBoxFlat CreateBgStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.10f, 0.95f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.45f, 0.45f, 0.65f, 0.75f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10,
            ShadowColor = new Color(0, 0, 0, 0.4f),
            ShadowSize = 4,
            ShadowOffset = new Vector2(0, 2)
        };
    }

    private StyleBoxFlat CreateBarBgStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.18f, 0.22f, 0.8f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
    }

    private StyleBoxFlat CreateBarFillStyle(float fillRatio)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.35f, 0.75f, 0.95f, 0.85f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
    }

    private StyleBoxFlat CreateBadgeBgStyle(Color baseColor)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(baseColor.R, baseColor.G, baseColor.B, 0.2f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(baseColor.R, baseColor.G, baseColor.B, 0.45f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 6,
            ContentMarginTop = 2,
            ContentMarginRight = 6,
            ContentMarginBottom = 2
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Show / Hide
    // ═══════════════════════════════════════════════════════════════════════

    public void ShowAt(RewardCardInfo card, float anchorX, float anchorY)
    {
        if (_disposed || _mainBox == null) return;

        RefreshContent(card);

        // Measure actual size
        Size = _mainBox.GetCombinedMinimumSize() + new Vector2(24, 20);

        // Position: above the card, offset upward by ~half a card height
        float targetX = anchorX - Size.X / 2f;
        float targetY = anchorY - Size.Y - 180f; // 180 ≈ half card height

        var viewport = GetViewport();
        if (viewport != null)
        {
            var screenSize = viewport.GetVisibleRect().Size;
            targetX = Mathf.Clamp(targetX, 0, screenSize.X - Size.X);
            targetY = Mathf.Clamp(targetY, 0, screenSize.Y - Size.Y);
        }

        GlobalPosition = new Vector2(targetX, targetY);

        if (!_isVisible)
        {
            _isVisible = true;
            Modulate = new Color(1, 1, 1, 0);
            Show();
            var tween = CreateTween();
            tween.TweenProperty(this, "modulate:a", 1f, FadeDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }
    }

    public new void Hide()
    {
        if (_disposed || !_isVisible) return;
        _isVisible = false;

        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, FadeDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!_disposed) base.Hide();
        }));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Content
    // ═══════════════════════════════════════════════════════════════════════

    private void RefreshContent(RewardCardInfo card)
    {
        if (_nameLabel == null) return;

        // ── Name ─────────────────────────────────────────────────────────
        var displayName = string.IsNullOrEmpty(card.DisplayNameZh) ? card.Name : card.DisplayNameZh;
        _nameLabel.Text = displayName;
        _nameLabel.AddThemeColorOverride("font_color", GetRankColor(card.Rank));

        // ── Rank Badge ───────────────────────────────────────────────────
        foreach (var node in _badgesBox!.GetChildren())
            node.QueueFree();

        if (card.Rank.HasValue)
        {
            _badgesBox.AddChild(CreateBadge($"#{card.Rank}", GetRankColor(card.Rank.Value)));
        }

        // ── Check data availability ─────────────────────────────────────
        bool hasPick = card.PickRate.HasValue;
        bool hasWinDelta = card.WinRateDelta.HasValue;
        bool hasHold = card.HoldStrength.HasValue;
        bool hasAnyData = hasPick || hasWinDelta || hasHold;

        _noDataLabel!.Visible = !hasAnyData;
        _barWrapper!.Visible = hasPick;
        _statRow!.Visible = hasWinDelta || hasHold;

        // ── BarWrapper (PickRate) ────────────────────────────────────────
        if (hasPick)
        {
            float normalized = Mathf.Clamp(card.PickRate!.Value / 100f, 0f, 1f);
            // Split available space proportionally: BarFill gets `normalized`, BarBg gets `1 - normalized`
            _barFill!.SizeFlagsStretchRatio = normalized;
            _barBg!.SizeFlagsStretchRatio = 1f - normalized;
            if (_barText != null) _barText.Text = $"{card.PickRate:F1}%";
        }

        // ── StatRow (WinRateDelta + HoldStrength) ───────────────────────
        foreach (var node in _statRow.GetChildren())
            node.QueueFree();

        if (hasWinDelta)
        {
            _statRow.AddChild(CreateStatLine("选卡建议", FormatWinDelta(card.WinRateDelta!.Value),
                card.WinRateDelta.Value >= 0
                    ? new Color(0.35f, 0.9f, 0.45f)
                    : new Color(1f, 0.4f, 0.4f)));
        }

        if (hasHold)
        {
            _statRow.AddChild(CreateStatLine("持有实力", FormatWinDelta(card.HoldStrength!.Value),
                card.HoldStrength.Value >= 0
                    ? new Color(0.35f, 0.9f, 0.45f)
                    : new Color(1f, 0.4f, 0.4f)));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Widget Builders
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Single-stat row: "Label   Value" in one HBox.
    /// </summary>
    private Control CreateStatLine(string label, string value, Color valueColor)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var labelNode = new Label { Text = label };
        labelNode.HorizontalAlignment = HorizontalAlignment.Left;
        labelNode.VerticalAlignment = VerticalAlignment.Center;
        labelNode.AddThemeFontSizeOverride("font_size", 11);
        labelNode.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f, 0.85f));
        row.AddChild(labelNode);

        var valueNode = new Label { Text = value };
        valueNode.HorizontalAlignment = HorizontalAlignment.Right;
        valueNode.VerticalAlignment = VerticalAlignment.Center;
        valueNode.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        valueNode.AddThemeFontSizeOverride("font_size", 12);
        valueNode.AddThemeColorOverride("font_color", valueColor);
        row.AddChild(valueNode);

        return row;
    }

    /// <summary>
    /// Creates a small pill badge with colored border/background.
    /// </summary>
    private Control CreateBadge(string text, Color color)
    {
        var container = new Panel { CustomMinimumSize = new Vector2(0, 20) };
        container.AddThemeStyleboxOverride("panel", CreateBadgeBgStyle(color));

        var label = new Label { Text = text };
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", color);
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        label.OffsetLeft = 6;
        label.OffsetTop = 2;
        label.OffsetRight = -6;
        label.OffsetBottom = -2;
        container.AddChild(label);

        return container;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static string FormatWinDelta(float delta)
    {
        return $"{delta:+0.0;-0.0}%";
    }

    private Color GetRankColor(int? rank)
    {
        if (!rank.HasValue) return new Color(0.85f, 0.85f, 0.9f, 1f);
        return rank.Value switch
        {
            1 => new Color(1.0f, 0.85f, 0.2f, 1f),
            2 => new Color(0.75f, 0.75f, 0.82f, 1f),
            3 => new Color(0.8f, 0.55f, 0.3f, 1f),
            _ => new Color(0.85f, 0.85f, 0.9f, 1f)
        };
    }

    public override void _ExitTree()
    {
        _disposed = true;
        base._ExitTree();
    }
}
