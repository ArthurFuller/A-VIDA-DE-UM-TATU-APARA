using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class FoodPreparationPanel : MonoBehaviour
{
    [Serializable]
    private struct IngredientRow
    {
        public RectTransform icon;
        public Text stock;
        public RectTransform decrease;
        public RectTransform increase;
        public RectTransform quantity;
        public RectTransform quantityBackground;

        public IEnumerable<RectTransform> Elements()
        {
            yield return icon;
            yield return stock != null ? stock.rectTransform : null;
            yield return decrease;
            yield return increase;
            yield return quantity;
            yield return quantityBackground;
        }
    }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectMask2D panelMask;
    [SerializeField] private CanvasGroup backdrop;
    [SerializeField] private Text titleText;
    [SerializeField] private IngredientRow[] rows;
    [SerializeField] private Text fishCountText;
    [SerializeField] private Text cornCountText;
    [SerializeField] private Text pumpkinCountText;
    [SerializeField] private Text jerimumCountText;
    [SerializeField] private Text milkCountText;
    [SerializeField] private Text eggsCountText;
    [SerializeField] private Text resultText;
    [SerializeField] private RectTransform prepareButton;
    [SerializeField] private RectTransform closeButton;
    [SerializeField, Min(0f)] private float panelDuration = 0.5f;
    [SerializeField, Min(0f)] private float rowDuration = 0.35f;
    [SerializeField, Min(0f)] private float rowWaveDelay = 0.06f;
    [SerializeField, Min(0f)] private float footerDuration = 0.32f;
    [SerializeField, Min(0f)] private float offscreenPadding = 80f;
    [SerializeField, Range(0f, 1f)] private float blockedIngredientOpacity = 0.40f;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private InventoryTrading trading;
    [SerializeField] private Button jerimumPlusButton;
    [SerializeField] private Button jerimumMinusButton;
    [SerializeField] private Button milkPlusButton;
    [SerializeField] private Button milkMinusButton;
    [SerializeField] private Button eggsPlusButton;
    [SerializeField] private Button eggsMinusButton;
    private int fish;
    private int corn;
    private int pumpkin;
    private int jerimum;
    private int milk;
    private int eggs;
    private bool plus;
    private bool transitioning;
    private Vector2 panelHome;
    private Vector3 closeScale;
    private Sequence transition;
    private readonly Dictionary<RectTransform, Vector2> homes = new();
    private List<(CanvasGroup Group, float RestingAlpha)>[] ingredientGroups;
    private const int MaxIngredientTypes = 2;

    internal bool BlocksCameraZoom => isActiveAndEnabled && group != null &&
        group.isActiveAndEnabled && group.blocksRaycasts;

    private void Awake()
    {
        FullscreenBackdrop.Cover(backdrop);
        if (panel != null) panelHome = panel.anchoredPosition;
        if (closeButton != null) closeScale = closeButton.localScale;
        if (rows != null)
        {
            ingredientGroups = new List<(CanvasGroup Group, float RestingAlpha)>[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var rowGroups = new List<(CanvasGroup Group, float RestingAlpha)>(6);
                ingredientGroups[i] = rowGroups;
                foreach (RectTransform element in rows[i].Elements())
                {
                    Remember(element);
                    if (element == null) continue;
                    if (!element.TryGetComponent(out CanvasGroup opacity))
                        opacity = element.gameObject.AddComponent<CanvasGroup>();
                    rowGroups.Add((opacity, opacity.alpha));
                }
            }
        }
        Remember(prepareButton);
        Remember(resultText != null ? resultText.rectTransform : null);
        if (jerimumPlusButton != null) jerimumPlusButton.onClick.AddListener(AddJerimum);
        if (jerimumMinusButton != null) jerimumMinusButton.onClick.AddListener(RemoveJerimum);
        if (milkPlusButton != null) milkPlusButton.onClick.AddListener(AddMilk);
        if (milkMinusButton != null) milkMinusButton.onClick.AddListener(RemoveMilk);
        if (eggsPlusButton != null) eggsPlusButton.onClick.AddListener(AddEggs);
        if (eggsMinusButton != null) eggsMinusButton.onClick.AddListener(RemoveEggs);
        HideImmediate();
    }
    private void OnDestroy()
    {
        transition?.Kill();
        if (jerimumPlusButton != null) jerimumPlusButton.onClick.RemoveListener(AddJerimum);
        if (jerimumMinusButton != null) jerimumMinusButton.onClick.RemoveListener(RemoveJerimum);
        if (milkPlusButton != null) milkPlusButton.onClick.RemoveListener(AddMilk);
        if (milkMinusButton != null) milkMinusButton.onClick.RemoveListener(RemoveMilk);
        if (eggsPlusButton != null) eggsPlusButton.onClick.RemoveListener(AddEggs);
        if (eggsMinusButton != null) eggsMinusButton.onClick.RemoveListener(RemoveEggs);
    }
    private void OnEnable() { if (resources != null) resources.Changed += Refresh; }
    private void OnDisable() { if (resources != null) resources.Changed -= Refresh; }

    public void Show(bool usePlus = false)
    {
        if (panel == null || group == null) return;
        AudioManager.Play(SoundCue.UiPaper);
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        transition?.Kill();
        RestoreHomes();
        plus = usePlus;
        fish = corn = pumpkin = jerimum = milk = eggs = 0;
        if (titleText != null) titleText.text = plus ? "PREPARAR COMIDA+" : "PREPARAR COMIDA";
        Refresh();
        transitioning = true;
        if (backdrop != null)
        {
            backdrop.gameObject.SetActive(true);
            backdrop.transform.SetAsLastSibling();
            backdrop.alpha = 0f;
            backdrop.blocksRaycasts = true;
        }
        panel.SetAsLastSibling();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        if (panelMask != null) panelMask.enabled = true;
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(false);
            closeButton.localScale = Vector3.zero;
        }
        float below = BelowViewport();
        panel.anchoredPosition = panelHome + Vector2.down * below;
        transition = DOTween.Sequence().SetTarget(this);
        transition.Insert(0f, panel.DOAnchorPos(panelHome, panelDuration)
            .SetEase(Ease.InOutSine));
        if (backdrop != null)
            transition.Insert(0f, backdrop.DOFade(1f, panelDuration)
                .SetEase(Ease.OutSine));

        float rowEnd = panelDuration;
        if (rows != null)
            for (int i = 0; i < rows.Length; i++)
            {
                float start = panelDuration + i * rowWaveDelay;
                IngredientRow row = rows[i];
                float side = panel.rect.width + offscreenPadding;
                Enter(row.icon, Vector2.left * side, start, rowDuration, Ease.OutCubic);
                Enter(row.stock != null ? row.stock.rectTransform : null,
                    Vector2.left * side, start, rowDuration, Ease.OutCubic);
                Enter(row.decrease, Vector2.right * side, start, rowDuration, Ease.OutCubic);
                Enter(row.increase, Vector2.right * side, start, rowDuration, Ease.OutCubic);
                Enter(row.quantity, Vector2.right * side, start, rowDuration, Ease.OutCubic);
                Enter(row.quantityBackground, Vector2.right * side, start, rowDuration,
                    Ease.OutCubic);
                rowEnd = start + rowDuration;
            }

        float footerStart = Mathf.Max(panelDuration, rowEnd - rowDuration * 0.45f);
        Vector2 footerOffset = Vector2.down * (panel.rect.height + offscreenPadding);
        Enter(prepareButton, footerOffset, footerStart, footerDuration, Ease.OutBack);
        Enter(resultText != null ? resultText.rectTransform : null,
            footerOffset, footerStart, footerDuration, Ease.OutBack);
        float closeStart = Mathf.Max(rowEnd, footerStart + footerDuration);
        if (closeButton != null)
        {
            transition.InsertCallback(closeStart, () => closeButton.gameObject.SetActive(true));
            transition.Insert(closeStart, closeButton.DOScale(closeScale * 1.1f, 0.26f)
                .SetEase(Ease.OutCubic));
            transition.Insert(closeStart + 0.26f,
                closeButton.DOScale(closeScale, 0.18f).SetEase(Ease.InOutSine));
        }
        transition.InsertCallback(closeStart + (closeButton != null ? 0.44f : 0f), () =>
        {
            if (panelMask != null) panelMask.enabled = false;
            transitioning = false;
        });
    }

    private void Remember(RectTransform rect)
    {
        if (rect != null) homes[rect] = rect.anchoredPosition;
    }

    private void RestoreHomes()
    {
        foreach (KeyValuePair<RectTransform, Vector2> entry in homes)
            if (entry.Key != null) entry.Key.anchoredPosition = entry.Value;
    }

    private void Enter(RectTransform rect, Vector2 offset, float start, float duration, Ease ease)
    {
        if (rect == null || !homes.TryGetValue(rect, out Vector2 home)) return;
        rect.DOKill();
        rect.anchoredPosition = home + offset;
        transition.Insert(start, rect.DOAnchorPos(home, duration).SetEase(ease));
    }

    private float BelowViewport()
    {
        RectTransform parent = panel.parent as RectTransform;
        return parent != null ? (parent.rect.height + panel.rect.height) * 0.5f +
            offscreenPadding : panel.rect.height + offscreenPadding;
    }

    private int TotalSelected => fish + corn + pumpkin + jerimum + milk + eggs;
    private int TypesSelected => (fish > 0 ? 1 : 0) + (corn > 0 ? 1 : 0) +
        (pumpkin > 0 ? 1 : 0) + (jerimum > 0 ? 1 : 0) +
        (milk > 0 ? 1 : 0) + (eggs > 0 ? 1 : 0);

    private bool CanSelectType(int selected) => selected > 0 || TypesSelected < MaxIngredientTypes;

    private void Add(ref int selected, int stock)
    {
        if (transitioning) return;
        if (selected < stock && TotalSelected < 3 && CanSelectType(selected)) selected++;
        Refresh();
    }

    private void Remove(ref int selected)
    {
        if (transitioning) return;
        selected = Mathf.Max(0, selected - 1);
        Refresh();
    }
    public void AddFish() => Add(ref fish, resources != null ? resources.Fish : 0);
    public void RemoveFish() => Remove(ref fish);
    public void AddCorn() => Add(ref corn, resources != null ? resources.Corn : 0);
    public void RemoveCorn() => Remove(ref corn);
    public void AddPumpkin() => Add(ref pumpkin, resources != null ? resources.Pumpkin : 0);
    public void RemovePumpkin() => Remove(ref pumpkin);
    public void AddJerimum() => Add(ref jerimum, resources != null ? resources.Jerimum : 0);
    public void RemoveJerimum() => Remove(ref jerimum);
    public void AddMilk() => Add(ref milk, resources != null ? resources.Milk : 0);
    public void RemoveMilk() => Remove(ref milk);
    public void AddEggs() => Add(ref eggs, resources != null ? resources.Eggs : 0);
    public void RemoveEggs() => Remove(ref eggs);

    public void Confirm()
    {
        if (transitioning || actions == null) return;
        transitioning = true;
        if (group != null) group.interactable = false;
        RectTransform origin = resultText != null ? resultText.rectTransform : prepareButton;
        if (actions.ConfirmFoodPreparation(fish, corn, pumpkin, jerimum, milk, eggs, origin, () => Hide())) return;
        transitioning = false;
        if (group != null) group.interactable = true;
    }

    public void Cancel()
    {
        if (transitioning) return;
        Hide(() => actions?.CancelFoodPreparation());
    }

    private void Hide(System.Action onClosed = null)
    {
        void CompleteClose()
        {
            HideImmediate();
            onClosed?.Invoke();
        }
        if (panel == null) { CompleteClose(); return; }
        AudioManager.Play(SoundCue.UiPaper);
        transitioning = true;
        transition?.Kill();
        transition = DOTween.Sequence().SetTarget(this)
            .Append(panel.DOAnchorPos(panelHome + Vector2.down * BelowViewport(),
                panelDuration).SetEase(Ease.InOutSine))
            .OnComplete(CompleteClose);
        if (backdrop != null)
            transition.Insert(0f, backdrop.DOFade(0f, panelDuration)
                .SetEase(Ease.InOutSine));
    }

    private void HideImmediate()
    {
        if (panel != null) panel.anchoredPosition = panelHome;
        RestoreHomes();
        if (closeButton != null) closeButton.localScale = closeScale;
        if (panelMask != null) panelMask.enabled = false;
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = false;
        }
        if (backdrop != null)
        {
            backdrop.blocksRaycasts = false;
            backdrop.alpha = 0f;
            backdrop.gameObject.SetActive(false);
        }
        transitioning = false;
    }

    private void Refresh()
    {
        if (resources == null) return;
        int output = 0;
        bool found = trading != null && trading.TryGetRecipe(fish, corn, pumpkin, jerimum,
            milk, eggs, plus, out _, out output);
        SetStock(0, "Peixe", resources.Fish);
        SetStock(1, "Milho", resources.Corn);
        SetStock(2, "Macaxeira", resources.Pumpkin);
        SetStock(3, "Jerimum", resources.Jerimum);
        SetStock(4, "Leite", resources.Milk);
        SetStock(5, "Ovos", resources.Eggs);
        RefreshIngredientOpacity(0, fish);
        RefreshIngredientOpacity(1, corn);
        RefreshIngredientOpacity(2, pumpkin);
        RefreshIngredientOpacity(3, jerimum);
        RefreshIngredientOpacity(4, milk);
        RefreshIngredientOpacity(5, eggs);
        if (fishCountText != null) fishCountText.text = fish.ToString();
        if (cornCountText != null) cornCountText.text = corn.ToString();
        if (pumpkinCountText != null) pumpkinCountText.text = pumpkin.ToString();
        if (jerimumCountText != null) jerimumCountText.text = jerimum.ToString();
        if (milkCountText != null) milkCountText.text = milk.ToString();
        if (eggsCountText != null) eggsCountText.text = eggs.ToString();
        if (resultText != null) resultText.text = "COMIDAS: " + (found ? output : 0);
    }

    private void RefreshIngredientOpacity(int index, int selected)
    {
        if (ingredientGroups == null || index >= ingredientGroups.Length) return;
        float alpha = CanSelectType(selected) ? 1f : blockedIngredientOpacity;
        foreach (var (group, restingAlpha) in ingredientGroups[index])
            if (group != null) group.alpha = restingAlpha * alpha;
    }

    private void SetStock(int index, string name, int amount)
    {
        if (rows != null && index < rows.Length && rows[index].stock != null)
            rows[index].stock.text = name + " x" + amount;
    }
}
