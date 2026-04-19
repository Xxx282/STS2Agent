using Godot;
using STS2Agent.Models;

namespace STS2Agent.Services;

public partial class CardHoverService : Node
{
    private CardRewardService? _cardRewardService;
    private string? _hoveredCardId;
    private int _updateCounter;
    private const int CHECK_INTERVAL = 3;  // 每3帧检测一次

    public event Action<RewardCardInfo>? OnCardHovered;
    public event Action? OnCardUnhovered;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public void Initialize(CardRewardService cardRewardService)
    {
        _cardRewardService = cardRewardService;
        Logger.Info("[Hover] CardHoverService 初始化完成");
    }

    public override void _Process(double delta)
    {
        if (_cardRewardService == null) return;

        _updateCounter++;
        if (_updateCounter % CHECK_INTERVAL != 0) return;

        var reward = _cardRewardService.GetCurrentReward();
        if (!reward.IsVisible || reward.Cards.Count == 0)
        {
            if (_hoveredCardId != null)
            {
                _hoveredCardId = null;
                OnCardUnhovered?.Invoke();
            }
            return;
        }

        var viewport = GetViewport();
        if (viewport == null) return;

        var mousePos = viewport.GetMousePosition();
        string? newHovered = null;
        RewardCardInfo? hoveredCard = null;

        foreach (var card in reward.Cards)
        {
            if (card.ScreenX.HasValue && card.ScreenY.HasValue)
            {
                // 扩大检测区域，卡牌宽约240px，高约360px
                float halfW = 130f;
                float halfH = 190f;
                if (mousePos.X >= card.ScreenX.Value - halfW &&
                    mousePos.X <= card.ScreenX.Value + halfW &&
                    mousePos.Y >= card.ScreenY.Value - halfH &&
                    mousePos.Y <= card.ScreenY.Value + halfH)
                {
                    newHovered = card.CardId;
                    hoveredCard = card;
                    break;
                }
            }
        }

        if (newHovered != _hoveredCardId)
        {
            _hoveredCardId = newHovered;
            if (hoveredCard != null)
            {
                Logger.Info($"[Hover] 悬停: {hoveredCard.CardId} at ({hoveredCard.ScreenX:F0}, {hoveredCard.ScreenY:F0})");
                OnCardHovered?.Invoke(hoveredCard);
            }
            else
            {
                Logger.Info("[Hover] 离开");
                OnCardUnhovered?.Invoke();
            }
        }
    }
}
