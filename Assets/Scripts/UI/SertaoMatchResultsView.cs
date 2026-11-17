using System;
using System.Collections;
using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SertaoMatchResultsView : MonoBehaviour
{
    [Serializable]
    private struct ProductionLine
    {
        public SaleItem item;
        public Text quantity;
    }

    [Serializable]
    private struct DeductionColumns
    {
        public Text quantity;
        public Text unitCost;
    }

    [Header("Páginas")]
    [SerializeField] private CanvasGroup resultsPage;
    [SerializeField] private CanvasGroup outcomePage;
    [SerializeField] private CanvasGroup curtain;
    [SerializeField] private Button continueButton;

    [Header("Produção e saldo")]
    [SerializeField] private ProductionLine[] production;
    [SerializeField, HideInInspector] private Text coinsEarned;
    [SerializeField] private Text balanceHeading;
    [SerializeField] private Text startingBalance;
    [SerializeField] private Text finalCoins;
    [SerializeField] private Text delivered;
    [SerializeField] private Text developedPlots;
    [SerializeField] private Text builtHouses;

    [Header("Descontos")]
    [SerializeField] private RectTransform deductionHeader;
    [SerializeField] private DeductionColumns[] deductionColumns;
    [SerializeField] private Text meals;
    [SerializeField] private Text orders;
    [SerializeField] private Text plots;
    [SerializeField] private Text houses;

    [Header("Desfecho")]
    [SerializeField] private Text outcomeMessage;
    [SerializeField] private Text outcomeTitle;
    [SerializeField] private string victoryMessage = "Sua comunidade prosperou.";
    [SerializeField] private string zeroMessage = "Sua comunidade ficou sem reservas.";
    [SerializeField] private string deficitMessage = "Sua comunidade ficou endividada.";
    [SerializeField] private string victoryTitle = "Você venceu";
    [SerializeField] private string defeatTitle = "Fim de jogo";

    [Header("Transições de tela")]
    [SerializeField, Min(0.1f)] private float resultsDarken = 1.2f;
    [SerializeField, Min(0.1f)] private float pageReveal = 0.85f;
    [SerializeField, Min(0.1f)] private float exitDarken = 0.65f;
    [SerializeField, Min(0.1f)] private float outcomeHold = 4.5f;

    [Header("Entrada do resumo")]
    [SerializeField, Min(0.1f)] private float countDuration = 0.40f;
    [SerializeField, Min(0.05f)] private float rowInterval = 0.26f;
    [SerializeField, Min(0.05f)] private float sectionInterval = 0.21f;
    [SerializeField, Min(0.1f)] private float outcomeTypingDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float outcomeTitleTypingDuration = 0.48f;

    private const string MenuScene = "MainMenu";
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Color32 ShortfallColor = new Color32(125, 43, 33, 255);
    private static SertaoMatchResultsView pendingMenuReturn;
    private GS2FrontEnd returningMenu;
    private HoverPunchButton continueFeedback;
    private Sequence transition;
    private bool shown;
    private bool continuing;
    private bool won;

    internal bool IsConfigured => resultsPage != null && outcomePage != null &&
        curtain != null && continueButton != null;

    private void Awake()
    {
        if (!IsConfigured) return;
        resultsPage.gameObject.SetActive(false);
        outcomePage.gameObject.SetActive(false);
        curtain.alpha = 0f;
        curtain.blocksRaycasts = false;
        if (continueButton.TryGetComponent(out continueFeedback))
            continueFeedback.AnimatePointerClick = false;
        continueButton.onClick.AddListener(Continue);
    }

    internal void Show(SertaoMatchResult result, Action applySettlement)
    {
        if (shown || !IsConfigured) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        shown = true;
        won = result.Won;
        // Também oculta a linha antiga em instâncias criadas antes da atualização do prefab.
        if (coinsEarned != null && coinsEarned.transform.parent != null)
            coinsEarned.transform.parent.gameObject.SetActive(false);
        Set(outcomeMessage, won ? victoryMessage : result.FinalCoins == 0 ? zeroMessage : deficitMessage);
        Set(outcomeTitle, won ? victoryTitle : defeatTitle);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        continueButton.interactable = false;
        curtain.blocksRaycasts = true;
        transition = DOTween.Sequence().SetUpdate(true)
            .Append(curtain.DOFade(1f, resultsDarken).SetEase(Ease.InOutSine))
            .AppendCallback(() =>
            {
                applySettlement?.Invoke();
                resultsPage.gameObject.SetActive(true);
            })
            .Append(curtain.DOFade(0f, pageReveal).SetEase(Ease.InOutSine))
            .Append(BuildSummaryEntrance(result))
            .OnComplete(EnableContinue);
    }

    private Sequence BuildSummaryEntrance(SertaoMatchResult result)
    {
        Sequence entrance = DOTween.Sequence();
        if (production != null)
            foreach (ProductionLine line in production)
                CountAt(entrance, 0f, line.quantity, result.Produced(line.item));

        float communityAt = countDuration + 0.12f + sectionInterval;
        FadeRowAt(entrance, communityAt, delivered);
        CountAt(entrance, communityAt, delivered, result.Delivered);
        FadeRowAt(entrance, communityAt + rowInterval, developedPlots);
        CountProgressAt(entrance, communityAt + rowInterval, developedPlots,
            result.DevelopedPlots, result.TotalPlots);
        FadeRowAt(entrance, communityAt + rowInterval * 2f, builtHouses);
        CountProgressAt(entrance, communityAt + rowInterval * 2f, builtHouses,
            result.BuiltHouses, result.TotalHouses);

        float balanceAt = communityAt + rowInterval * 2f + countDuration + sectionInterval;
        FadeAt(entrance, balanceAt, balanceHeading);
        if (deductionHeader == null && startingBalance != null && startingBalance.transform.parent != null)
        {
            Transform legacyLabel = startingBalance.transform.parent.Find("Descrição");
            if (legacyLabel != null)
                Set(legacyLabel.GetComponent<Text>(), "Acumulado na partida");
        }
        FadeRowAt(entrance, balanceAt, startingBalance);
        CountAt(entrance, balanceAt, startingBalance, result.StartingBalance);

        float headerAt = balanceAt + countDuration + sectionInterval;
        if (deductionHeader != null)
            foreach (Graphic graphic in deductionHeader.GetComponentsInChildren<Graphic>(true))
                FadeAt(entrance, headerAt, graphic);
        float deductionsAt = headerAt + rowInterval;
        DeductionAt(entrance, deductionsAt, 0, meals, result.MissedMeals);
        DeductionAt(entrance, deductionsAt + rowInterval, 1, orders, result.UndeliveredOrders);
        DeductionAt(entrance, deductionsAt + rowInterval * 2f, 2, plots, result.UndevelopedPlots);
        DeductionAt(entrance, deductionsAt + rowInterval * 3f, 3, houses, result.UnbuiltHouses);

        float finalAt = deductionsAt + rowInterval * 3f + countDuration + sectionInterval;
        FadeRowAt(entrance, finalAt, finalCoins);
        if (finalCoins != null && result.FinalCoins < 0)
        {
            Color debt = ShortfallColor;
            debt.a = finalCoins.color.a;
            finalCoins.color = debt;
        }
        CountAt(entrance, finalAt, finalCoins, result.FinalCoins, result.StartingBalance);
        return entrance;
    }

    private void CountProgressAt(Sequence entrance, float at, Text target, int current, int total)
    {
        if (target == null) return;
        bool incomplete = current < total;
        string prefix = incomplete ? $"<color=#{ColorUtility.ToHtmlStringRGB(ShortfallColor)}>" : string.Empty;
        string suffix = (incomplete ? "</color>" : string.Empty) + " / " + Number(total);
        if (incomplete) target.supportRichText = true;
        CountAt(entrance, at, target, current, format: count => prefix + Number(count) + suffix);
    }

    private void DeductionAt(Sequence entrance, float at, int index, Text total,
        SertaoMatchResult.Deduction deduction)
    {
        DeductionColumns columns = deductionColumns != null && index < deductionColumns.Length
            ? deductionColumns[index] : default;
        if (columns.quantity == null || columns.unitCost == null)
        {
            Set(total, $"{Number(deduction.Count)} | {Number(-deduction.UnitCost)} cada | {Number(-deduction.Total)}");
            FadeRowAt(entrance, at, total);
            FadeAt(entrance, at, total);
            return;
        }

        Set(columns.unitCost, Number(-deduction.UnitCost));
        FadeRowAt(entrance, at, total, columns.quantity);
        CountAt(entrance, at, columns.quantity, deduction.Count);
        CountAt(entrance, at, total, -deduction.Total, flashDeduction: true);
    }

    private void CountAt(Sequence entrance, float at, Text target, long amount,
        long start = 0, Func<long, string> format = null, bool flashDeduction = false)
    {
        if (target == null) return;
        format ??= Number;
        target.text = format(start);
        Color resting = target.color;
        Vector3 scale = target.rectTransform.localScale;
        long last = start;
        FadeAt(entrance, at, target);
        entrance.Insert(at, DOVirtual.Float(0f, 1f, countDuration, progress =>
        {
            long value = (long)Math.Round(start + ((decimal)amount - start) * (decimal)progress);
            if (last == value) return;
            target.text = format(value);
            last = value;
        }).SetEase(Ease.OutCubic));
        Color flash = flashDeduction && amount < 0 ? new Color(0.72f, 0.23f, 0.15f, resting.a)
            : new Color(1f, 1f, 1f, resting.a);
        float pulseAt = at + countDuration;
        entrance.Insert(pulseAt, target.DOColor(flash, 0.035f));
        entrance.Insert(pulseAt + 0.035f, target.DOColor(resting, 0.085f));
        entrance.Insert(pulseAt, target.rectTransform.DOScale(scale * 1.08f, 0.06f)
            .SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad));
    }

    private static void FadeRowAt(Sequence entrance, float at, Text value, Graphic secondValue = null)
    {
        if (value == null || value.transform.parent == null) return;
        foreach (Graphic graphic in value.transform.parent.GetComponentsInChildren<Graphic>(true))
            if (graphic != value && graphic != secondValue) FadeAt(entrance, at, graphic);
    }

    private static void FadeAt(Sequence entrance, float at, Graphic graphic)
    {
        if (graphic == null) return;
        // Fade all glyphs together, including numbers with an inline color tag.
        CanvasRenderer renderer = graphic.canvasRenderer;
        float opacity = renderer.GetAlpha();
        renderer.SetAlpha(0f);
        entrance.Insert(at, DOTween.To(renderer.GetAlpha, renderer.SetAlpha, opacity, 0.16f)
            .SetEase(Ease.OutSine));
    }

    private static string Number(long value) => value.ToString("N0", Portuguese);

    private static void Set(Text target, string value)
    {
        if (target != null) target.text = value;
    }

    private void EnableContinue()
    {
        curtain.blocksRaycasts = false;
        continueButton.interactable = true;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
    }

    private void Continue()
    {
        if (!shown || continuing || !continueButton.interactable) return;
        if (!Application.CanStreamedLevelBeLoaded(MenuScene))
        {
            Debug.LogError("A cena MainMenu precisa estar na lista de cenas do jogo.", this);
            return;
        }

        continuing = true;
        continueFeedback?.PlayAcceptedClick();
        continueButton.interactable = false;
        curtain.blocksRaycasts = true;
        transition = DOTween.Sequence().SetUpdate(true);
        if (continueFeedback != null) transition.AppendInterval(0.16f);
        transition.AppendCallback(() => AudioManager.Play(SoundCue.UiPaper))
            .Append(curtain.DOFade(1f, exitDarken).SetEase(Ease.InOutSine))
            .AppendCallback(() =>
            {
                resultsPage.gameObject.SetActive(false);
                outcomePage.gameObject.SetActive(true);
            })
            .Append(curtain.DOFade(0f, pageReveal).SetEase(Ease.InOutSine));
        AppendOutcomeLine(outcomeMessage, outcomeTypingDuration);
        transition.AppendInterval(0.10f);
        AppendOutcomeLine(outcomeTitle, outcomeTitleTypingDuration);
        transition.AppendInterval(outcomeHold)
            .Append(curtain.DOFade(1f, exitDarken).SetEase(Ease.InOutSine))
            .OnComplete(() => StartCoroutine(ReturnToMenu()));
    }

    private void AppendOutcomeLine(Text line, float duration)
    {
        if (line == null) return;
        if (line is LetterJumpText letters)
        {
            transition.Append(letters.PrepareTimedReveal(letters.text, duration));
            return;
        }

        FadeAt(transition, transition.Duration(), line);
    }

    private IEnumerator ReturnToMenu()
    {
        outcomePage.gameObject.SetActive(false);
        if (transform.parent != null) transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
        pendingMenuReturn = this;
        yield return SceneManager.LoadSceneAsync(MenuScene, LoadSceneMode.Single);
        transition = DOTween.Sequence().SetUpdate(true)
            .Append(curtain.DOFade(0f, pageReveal).SetEase(Ease.InOutSine));
        if (returningMenu != null) returningMenu.AddEntranceTweens(transition);
        transition.OnComplete(() =>
        {
            curtain.blocksRaycasts = false;
            if (returningMenu != null) returningMenu.CompleteMatchReturn();
            Destroy(gameObject);
        });
    }

    internal static bool BindReturningMenu(GS2FrontEnd menu, out bool showRanking)
    {
        SertaoMatchResultsView results = pendingMenuReturn;
        pendingMenuReturn = null;
        showRanking = results != null && results.won;
        if (results == null) return false;
        results.returningMenu = menu;
        return true;
    }

    private void OnDestroy()
    {
        if (pendingMenuReturn == this) pendingMenuReturn = null;
        transition?.Kill();
        if (outcomeMessage is LetterJumpText letters) letters.DOKill();
        if (outcomeTitle is LetterJumpText titleLetters) titleLetters.DOKill();
        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
    }
}
