using Godot;
using STS2Agent.Models;

namespace STS2Agent.Designer;

public partial class CardTooltipDesigner : Control
{
    private const float TooltipWidth = 260f;
    private const float TooltipHeight = 160f;
    private const float FadeDuration = 0.2f;
    private const float MarginBottom = 10f;

    private bool _isVisible;
    private bool _disposed;
    private Label? _nameLabel;
    private HBoxContainer? _badgesBox;
    private HBoxContainer? _barWrapper;
    private Panel? _barBg;
    private Panel? _barFill;
    private Label? _scoreLabel;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;

        ZIndex = 1000;
        PopulateSampleData();
        Logger.Info("[Tooltip] CardTooltipDesigner ready");
    }

    public void ShowAt(RewardCardInfo card, float anchorX, float anchorY)
    {
        if (_disposed) return;

        float targetX = anchorX - TooltipWidth / 2f;
        float targetY = anchorY - TooltipHeight - MarginBottom;

        var viewport = GetViewport();
        if (viewport != null)
        {
            var screenSize = viewport.GetVisibleRect().Size;
            targetX = Mathf.Clamp(targetX, 0, screenSize.X - TooltipWidth);
            targetY = Mathf.Clamp(targetY, 0, screenSize.Y - TooltipHeight);
        }

        GlobalPosition = new Vector2(targetX, targetY);
        RefreshContent(card);

        if (!_isVisible)
        {
            _isVisible = true;
            Modulate = new Color(1, 1, 1, 0);
            Show();
            CreateTween().TweenProperty(this, "modulate:a", 1f, FadeDuration)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }
    }

    public new void Hide()
    {
        if (_disposed || !_isVisible) return;
        _isVisible = false;
        CreateTween().TweenProperty(this, "modulate:a", 0f, FadeDuration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out)
            .TweenCallback(Callable.From(() =>
            {
                if (!_disposed) base.Hide();
            }));
    }

    private void RefreshContent(RewardCardInfo card)
    {
        if (_nameLabel == null || _badgesBox == null) return;

        var displayName = string.IsNullOrEmpty(card.DisplayNameZh) ? card.Name : card.DisplayNameZh;
        _nameLabel.Text = displayName;
        _nameLabel.AddThemeColorOverride("font_color", GetRankColor(card.Rank));

        foreach (var child in _badgesBox.GetChildren())
            child.QueueFree();

        bool hasData = card.PickRate.HasValue || card.WinRateDelta.HasValue || card.SkadaScore.HasValue;

        if (!hasData)
        {
            var noData = new Label { Text = "暂无数据" };
            noData.HorizontalAlignment = HorizontalAlignment.Left;
            noData.AddThemeFontSizeOverride("font_size", 12);
            noData.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _badgesBox.AddChild(noData);
        }
        else
        {
            if (card.Rank.HasValue)
                _badgesBox.AddChild(CreateBadge($"#{card.Rank}", GetRankColor(card.Rank.Value)));
            if (card.PickRate.HasValue)
                _badgesBox.AddChild(CreateBadge($"{card.PickRate:F1}%", new Color(0.7f, 0.85f, 1f, 0.9f)));
            if (card.WinRateDelta.HasValue)
            {
                var delta = card.WinRateDelta.Value;
                var deltaColor = delta >= 0
                    ? new Color(0.4f, 1f, 0.5f, 0.9f)
                    : new Color(1f, 0.4f, 0.4f, 0.9f);
                _badgesBox.AddChild(CreateBadge($"{delta:+0.0;-0.0}%", deltaColor));
            }
            if (card.Confidence != null)
            {
                var (confText, confColor) = card.Confidence switch
                {
                    "high" => ("高置信", new Color(0.4f, 1f, 0.5f, 0.9f)),
                    "medium" => ("中置信", new Color(1f, 0.85f, 0.4f, 0.9f)),
                    _ => ("低置信", new Color(0.6f, 0.6f, 0.6f, 0.8f))
                };
                _badgesBox.AddChild(CreateBadge(confText, confColor));
            }
        }

        if (card.SkadaScore.HasValue)
        {
            _barWrapper?.Show();
            if (_barBg != null && _barFill != null && _scoreLabel != null)
            {
                float normalized = Mathf.Clamp(card.SkadaScore.Value / 100f, 0f, 1f);
                _barFill.SizeFlagsStretchRatio = normalized;
                _barBg.SizeFlagsStretchRatio = 1f - normalized;
                _barFill.AddThemeStyleboxOverride("panel", CreateBarFillStyle(normalized));
                _scoreLabel.Text = $"{(int)card.SkadaScore}";
            }
        }
        else
        {
            _barWrapper?.Hide();
            if (_scoreLabel != null) _scoreLabel.Text = "";
        }
    }

    private Label CreateBadge(string text, Color color)
    {
        var label = new Label { Text = text };
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private StyleBoxFlat CreateBarBgStyle() => new()
    {
        BgColor = new Color(0.18f, 0.18f, 0.22f, 0.7f),
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
    };

    private StyleBoxFlat CreateBarFillStyle(float normalized)
    {
        Color fillColor = normalized >= 0.7f
            ? new Color(0.3f, 0.85f, 0.4f, 0.9f)
            : normalized >= 0.4f
                ? new Color(0.9f, 0.75f, 0.3f, 0.9f)
                : new Color(0.85f, 0.35f, 0.35f, 0.9f);

        return new StyleBoxFlat { BgColor = fillColor };
    }

    private Color GetRankColor(int? rank) => rank switch
    {
        1 => new Color(1.0f, 0.85f, 0.2f, 1f),
        2 => new Color(0.75f, 0.75f, 0.82f, 1f),
        3 => new Color(0.8f, 0.55f, 0.3f, 1f),
        _ => new Color(0.85f, 0.85f, 0.9f, 1f)
    };

    // 预填示例数据，方便在编辑器中预览
    private void PopulateSampleData()
    {
        var card = new RewardCardInfo
        {
            Name = "Bash",
            DisplayNameZh = "重击",
            Rank = 1,
            PickRate = 42.5f,
            WinRateDelta = 5.8f,
            SkadaScore = 70,
            Confidence = "high"
        };

        _nameLabel = GetNode<Label>("MainBox/NameLabel");
        _badgesBox = GetNode<HBoxContainer>("MainBox/BadgesBox");
        _barWrapper = GetNode<HBoxContainer>("MainBox/BarWrapper");
        _barBg = GetNode<Panel>("MainBox/BarWrapper/BarBg");
        _barFill = GetNode<Panel>("MainBox/BarWrapper/BarBg/BarFill");
        _scoreLabel = GetNode<Label>("MainBox/BarWrapper/ScoreLabel");

        RefreshContent(card);
    }

    public override void _ExitTree()
    {
        _disposed = true;
        base._ExitTree();
    }
}
