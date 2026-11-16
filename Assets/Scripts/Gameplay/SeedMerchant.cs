using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class SeedMerchant : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private SeedCatalog catalog;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private CanvasGroup shopGroup;
    [SerializeField] private Text shopStatusText;
    [SerializeField] private DragCursorPreview dragPreview;
    [SerializeField] private ActiveMissionsPanel activeMissionsPanel;
    [SerializeField] private DayTimeline timeline;
    [SerializeField] private ShopTabs shopTabs;
    [SerializeField] private ActivityCardAvailability hand;
    [SerializeField, HideInInspector, Min(0)] private int fenceWoodCost = 4;
    [SerializeField, HideInInspector, Min(0)] private int fenceCoinCost = 8;
    [SerializeField, HideInInspector] private int[] marketDays = { 1, 3, 5, 7, 9, 11 };
    [SerializeField, HideInInspector, Min(1)] private int foodCoinCost = 1;
    [SerializeField, Min(0)] private float panelFadeDuration = 0.25f;
    [Header("Apresentação da mercearia")]
    [SerializeField] private CanvasGroup backdropGroup;
    [SerializeField, Min(0.1f)] private float panelEntranceDuration = 0.46f;
    [SerializeField, Range(0.5f, 4f),
        Tooltip("Multiplica a velocidade da entrada e saida da janela, sem alterar as distancias. 1 = tempo normal; 2 = duas vezes mais rapido.")]
    private float panelAnimationSpeed = 1.55f;
    [SerializeField, Min(0f)] private float entranceExtraDistance = 40f;
    private RectTransform panelRect;
    private Vector2 panelHome;
    private Sequence panelTransition;
    private float PanelTime(float duration) =>
        Mathf.Max(0f, duration) / Mathf.Max(0.5f,
            panelAnimationSpeed > 0f ? panelAnimationSpeed : 1.55f);

    public event Action AvailabilityChanged;
    public event Action Closed;
    public bool IsAvailable { get; private set; }
    public bool IsOpen => shopGroup != null && shopGroup.interactable;
    public int FenceWoodCost => fenceWoodCost;
    public int FenceCoinCost => fenceCoinCost;
    public int FenceSlotsAvailable => hand != null ? hand.FreeSlots : 0;

    private void Awake()
    {
        GameBalance balance = GameBalance.Current;
        if (balance != null)
        {
            fenceWoodCost = balance.fenceWoodCost;
            fenceCoinCost = balance.fenceCoinCost;
            foodCoinCost = balance.foodCoinCost;
            marketDays = balance.marketDays;
        }
        if (shopGroup == null) return;
        panelRect = shopGroup.transform as RectTransform;
        if (panelRect != null) panelHome = panelRect.anchoredPosition;
        shopGroup.DOKill();
        shopGroup.alpha = 0f;
        shopGroup.blocksRaycasts = false;
        shopGroup.interactable = false;
        if (backdropGroup != null)
        {
            backdropGroup.alpha = 0f;
            backdropGroup.interactable = false;
            backdropGroup.blocksRaycasts = false;
        }
    }
    public void ToggleShop()
    {
        if (!IsAvailable || dayCycle == null || !dayCycle.CanAcceptActions)
        {
            if (feedback != null) feedback.ShowMessage("A barraca está fechada hoje.");
            return;
        }
        if (IsOpen) HideShop();
        else ShowShop();
    }

    public void SetDay(int day, bool hasDeliveryDue = false)
    {
        bool scheduled = Array.IndexOf(marketDays, day) >= 0;
        IsAvailable = hasDeliveryDue || scheduled || day == 12;
        HideShop();
        AvailabilityChanged?.Invoke();
    }

    public int GetUnitPrice(SaleItem item)
    {
        if (catalog == null) return 0;
        return item switch
        {
            SaleItem.CornSeeds => catalog.Get(CropType.Corn).coinCost,
            SaleItem.PumpkinSeeds => catalog.Get(CropType.Pumpkin).coinCost,
            SaleItem.JerimumSeeds => catalog.Get(CropType.Jerimum).coinCost,
            SaleItem.Food => foodCoinCost,
            _ => 0
        };
    }

    public bool TryBuyCart(int cornSeeds, int pumpkinSeeds, int jerimumSeeds, int food, int fences)
    {
        if (!IsAvailable || !IsOpen || dayCycle == null || !dayCycle.CanAcceptActions || resources == null ||
            catalog == null ||
            cornSeeds != 0 || pumpkinSeeds != 0 || jerimumSeeds != 0 || food < 0 || fences < 0 ||
            fences > FenceSlotsAvailable ||
            resources.Wood < (long)fences * fenceWoodCost ||
            (long)resources.Food + food > int.MaxValue) return false;
        long total = (long)food * GetUnitPrice(SaleItem.Food) + (long)fences * fenceCoinCost;
        if (total <= 0 || total > int.MaxValue || !resources.TrySpendCoins((int)total))
        {
            if (feedback != null) feedback.ShowMessage("Compra acima do limite de moedas.");
            return false;
        }
        if (food > 0)
        {
            resources.AddFood(food);
            if (resourceHud != null && shopGroup != null)
                resourceHud.FlyFromRect(ResourceKind.Food, food,
                    shopGroup.transform as RectTransform);
        }
        if (fences > 0)
        {
            resources.TrySpendWood(fences * fenceWoodCost);
            for (int i = 0; i < fences; i++) hand.AddCard(ActivityKind.BuildFence);
        }
        if (feedback != null) feedback.ShowMessage("Compra concluída: -" + total + " moedas.");
        return true;
    }

    private float EntranceDistance()
    {
        if (panelRect == null) return 400f;
        RectTransform parent = panelRect.parent as RectTransform;
        // O limite inferior funciona mesmo se o Canvas ainda nao tiver resolvido o layout.
        float parentHeight = parent != null ? parent.rect.height : 0f;
        float safeHeight = Mathf.Max(parentHeight, panelRect.rect.height * 1.6f);
        return (safeHeight + panelRect.rect.height) * 0.5f + entranceExtraDistance;
    }

    public void ShowShop()
    {
        if (!IsAvailable || shopGroup == null || IsOpen) return;
        // A cena armazena o painel como inativo. Ativar antes de preparar
        // o ShopTabs garante que o Awake capture as posicoes reais dos itens.
        if (!shopGroup.gameObject.activeSelf)
            shopGroup.gameObject.SetActive(true);
        if (dragPreview != null) dragPreview.CancelCurrentDrag();
        if (activeMissionsPanel != null) activeMissionsPanel.Close();
        if (timeline != null) timeline.ClosePanel();
        if (shopTabs != null) shopTabs.ShowDefault(true);
        AudioManager.Play(SoundCue.PanelOpen);

        if (backdropGroup != null)
        {
            backdropGroup.transform.SetAsLastSibling();
            backdropGroup.DOKill();
            backdropGroup.alpha = 0f;
            backdropGroup.blocksRaycasts = true;
            backdropGroup.interactable = false;
            backdropGroup.DOFade(1f, Mathf.Min(PanelTime(panelEntranceDuration), 0.3f))
                .SetEase(Ease.OutSine).SetUpdate(true);
        }
        shopGroup.transform.SetAsLastSibling();
        panelTransition?.Kill();
        panelTransition = null;
        shopGroup.DOKill();
        if (panelRect != null)
        {
            panelRect.DOKill();
            panelRect.anchoredPosition = panelHome + Vector2.down * EntranceDistance();
            // As abas e os produtos so animam DEPOIS que a janela estiver no centro.
            panelRect.DOAnchorPos(panelHome, PanelTime(panelEntranceDuration))
                .SetEase(Ease.OutCubic).SetUpdate(true)
                .OnComplete(() =>
                {
                    if (shopGroup != null && shopGroup.interactable)
                        shopTabs?.OnShopOpened();
                });
        }
        shopGroup.alpha = 1f;
        shopGroup.blocksRaycasts = true;
        shopGroup.interactable = true;
        if (panelRect == null) shopTabs?.OnShopOpened();
        if (shopStatusText != null) shopStatusText.text = "Role a lista para ver todos os itens.";
    }

    public void HideShop()
    {
        if (shopGroup == null) return;
        bool wasOpen = IsOpen;
        bool wasClosing = panelTransition != null;
        if (wasOpen) AudioManager.Play(SoundCue.PanelClose);
        if (shopTabs != null) shopTabs.OnShopClosed();
        shopGroup.blocksRaycasts = false;
        shopGroup.interactable = false;
        panelTransition?.Kill();
        panelTransition = null;
        shopGroup.DOKill();

        if (panelRect != null) panelRect.DOKill();
        if (wasOpen && panelRect != null)
        {
            panelTransition = DOTween.Sequence().SetTarget(this).SetUpdate(true)
                .Append(panelRect.DOAnchorPos(panelHome + Vector2.down * EntranceDistance(),
                    PanelTime(panelFadeDuration)).SetEase(Ease.InCubic))
                .Join(shopGroup.DOFade(0f, PanelTime(panelFadeDuration)))
                .OnComplete(() =>
                {
                    if (panelRect != null) panelRect.anchoredPosition = panelHome;
                    panelTransition = null;
                    Closed?.Invoke();
                });
        }
        else
        {
            shopGroup.alpha = 0f;
            if (panelRect != null) panelRect.anchoredPosition = panelHome;
            if (wasOpen || wasClosing) Closed?.Invoke();
        }

        if (backdropGroup != null)
        {
            backdropGroup.DOKill();
            if (wasOpen)
                backdropGroup.DOFade(0f, PanelTime(panelFadeDuration)).SetUpdate(true)
                    .OnComplete(() => backdropGroup.blocksRaycasts = false);
            else
            {
                backdropGroup.alpha = 0f;
                backdropGroup.blocksRaycasts = false;
            }
        }
    }

    private void OnDisable()
    {
        panelTransition?.Kill();
        panelTransition = null;
        DOTween.Kill(this);
        if (panelRect != null)
        {
            panelRect.DOKill();
            panelRect.anchoredPosition = panelHome;
        }
        if (shopGroup != null)
        {
            shopGroup.DOKill();
            shopGroup.alpha = 0f;
            shopGroup.interactable = false;
            shopGroup.blocksRaycasts = false;
        }
        if (backdropGroup != null)
        {
            backdropGroup.DOKill();
            backdropGroup.alpha = 0f;
            backdropGroup.blocksRaycasts = false;
        }
    }
}
