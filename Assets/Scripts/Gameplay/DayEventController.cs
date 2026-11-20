using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public enum SertaoEventKind { None, Drought, Pests, Rain, Comadre, Calm, HeadOfCuia, Reforestation }

public sealed class DayEventController : MonoBehaviour
{
    public const string HeadOfCuiaLossToken = "{peixesRoubados}";
    public const string PestCropToken = "{cultura}";
    public const string PestDurationToken = "{duracao}";
    private const int MaxConsecutiveCalmDays = 2;

    public readonly struct TimelineEvent
    {
        public readonly string Title;
        public readonly string Description;
        public readonly Sprite Art;
        public readonly int StartedOn;
        public readonly SertaoEventKind Kind;

        public TimelineEvent(string title, string description, Sprite art, int startedOn,
            SertaoEventKind kind)
        {
            Title = title;
            Description = description;
            Art = art;
            StartedOn = startedOn;
            Kind = kind;
        }
    }

    private sealed class ActiveComadre
    {
        public int StartedOn;
        public int DaysRemaining;
    }

    private sealed class ActiveGeneral
    {
        public SertaoEventKind Kind;
        public int StartedOn;
        public int DaysRemaining;
    }

    [SerializeField] private EventCardView card;
    internal EventCardView TutorialCard => card;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private Plot[] plots;
    [SerializeField] private WorkerAssignment[] workers;
    [Header("Artes dos eventos")]
    [SerializeField] private Sprite droughtArt;
    [SerializeField] private Sprite rainArt;
    [SerializeField] private Sprite comadreArt;
    [SerializeField] private Sprite calmArt;
    [SerializeField] private Sprite headOfCuiaArt;
    [SerializeField] private Sprite pestsArt;
    [SerializeField] private Sprite reforestationArt;
    [Header("Parâmetros provisórios de eventos")]
    [SerializeField, HideInInspector, Range(0f, 1f)] private float generalEventChance = 0.35f;
    [SerializeField, HideInInspector, Min(1)] private int eventDurationDays = 2;
    [SerializeField, HideInInspector, Min(0)] private int droughtFishingReduction = 1;
    [SerializeField, HideInInspector, Min(0)] private int pestHarvestReduction = 1;
    [Header("Pragas")]
    [FormerlySerializedAs("cassavaRegressionWithoutProtection")]
    [SerializeField, HideInInspector, Min(0)] private int pestGrowthRegression = 2;
    [SerializeField, HideInInspector, Min(0)] private int droughtGrowthDelay = 1;
    [SerializeField, HideInInspector, Min(0)] private int rainGrowthBonus = 1;
    [SerializeField, HideInInspector, Min(1)] private int forestExploitationThreshold = 6;
    [SerializeField, HideInInspector, Range(0f, 1f)] private float comadreChance = 0.5f;
    [SerializeField, HideInInspector, Min(1)] private int comadreAbsenceDays = 1;
    [SerializeField, HideInInspector, Min(0)] private int rainForestRecovery = 2;
    private int headOfCuiaMinimumFish = 8;
    private int headOfCuiaChancePercent = 25;
    private int headOfCuiaTheftPercent = 20;
    [Header("Reflorestamento")]
    [SerializeField, HideInInspector, Min(1)]
    private int daysWithoutWoodForReforestation = 3;
    [SerializeField, HideInInspector, Min(1)]
    private int reforestationWood = 4;

    private readonly List<ActiveGeneral> activeGeneralEvents = new();
    private SertaoEventKind lastAnnounced;
    private SertaoEventKind preparedEvent;
    private int consecutiveCalmDays;
    private int forestExploitation;
    private int stolenFishToday;
    private string StolenFishLabel => stolenFishToday + (stolenFishToday == 1 ? " peixe" : " peixes");
    private int currentDay = 1;
    private int lastWoodCollectionDay = 1;
    private int lastReforestationDay = -100;
    private int pendingWoodReward;

    private void Awake()
    {
        GameBalance balance = GameBalance.Current;
        if (balance == null) return;
        generalEventChance = balance.generalEventChance;
        droughtFishingReduction = balance.droughtFishingReduction;
        pestHarvestReduction = balance.pestHarvestReduction;
        pestGrowthRegression = balance.pestGrowthRegression;
        droughtGrowthDelay = balance.droughtGrowthDelay;
        rainGrowthBonus = balance.rainGrowthBonus;
        forestExploitationThreshold = balance.forestExploitationThreshold;
        comadreChance = balance.comadreChance;
        comadreAbsenceDays = balance.comadreAbsenceDays;
        rainForestRecovery = balance.rainForestRecovery;
        headOfCuiaMinimumFish = Mathf.Max(1, balance.headOfCuiaMinimumFish);
        headOfCuiaChancePercent = Mathf.Clamp(balance.headOfCuiaChancePercent, 0, 100);
        headOfCuiaTheftPercent = Mathf.Clamp(balance.headOfCuiaTheftPercent, 1, 100);
        daysWithoutWoodForReforestation = balance.daysWithoutWoodForReforestation;
        reforestationWood = balance.reforestationWood;
    }

    private int DurationFor(SertaoEventKind kind)
    {
        GameBalance balance = GameBalance.Current;
        if (balance == null) return eventDurationDays;
        return kind == SertaoEventKind.Rain ? balance.rainDurationDays :
            kind == SertaoEventKind.Pests ? balance.pestDurationDays : balance.droughtDurationDays;
    }
    private bool pestRegressionPending;
    private CropType pestTarget = CropType.None;
    private readonly HashSet<Plot> pestTargetsWithEmptyBar = new();
    public bool IsRaining => IsActive(SertaoEventKind.Rain);
    public bool IsDrought => IsActive(SertaoEventKind.Drought);
    public event System.Action WeatherChanged;
    public CropType PestTarget => pestTarget;
    public int HarvestReductionFor(Plot plot) =>
        plot != null && plot.Crop != CropType.None && plot.Crop == pestTarget && !plot.IsWilting &&
        IsActive(SertaoEventKind.Pests) &&
        (MandalaSynergy.Instance == null || !MandalaSynergy.Instance.ProtectsFromPests(plot))
            ? pestHarvestReduction : 0;
    private readonly List<ActiveComadre> activeComadre = new();
    private readonly Dictionary<int, List<TimelineEvent>> eventsByDay = new();

    public bool IsPanelOpen => card != null && card.IsOpen;
    public IReadOnlyList<TimelineEvent> EventsForDay(int day) =>
        eventsByDay.TryGetValue(day, out List<TimelineEvent> events) ?
            events : System.Array.Empty<TimelineEvent>();

    public void AfterEventCardClosed(System.Action action)
    {
        if (card == null || !card.IsOpen)
        {
            action?.Invoke();
            return;
        }
        void OnClosed()
        {
            card.Closed -= OnClosed;
            action?.Invoke();
        }
        card.Closed += OnClosed;
    }
    public int FishingReduction => IsActive(SertaoEventKind.Drought) ? droughtFishingReduction : 0;
    public int HarvestReduction => IsActive(SertaoEventKind.Pests) ? pestHarvestReduction : 0;

    private bool IsActive(SertaoEventKind kind)
    {
        foreach (ActiveGeneral active in activeGeneralEvents)
            if (active.Kind == kind) return true;
        return false;
    }

    public void RegisterWorker(WorkerAssignment worker)
    {
        if (worker == null || System.Array.IndexOf(workers, worker) >= 0) return;
        workers = new List<WorkerAssignment>(workers) { worker }.ToArray();
    }

    public void RecordWood(int amount)
    {
        if (amount <= 0) return;
        forestExploitation += amount;
        lastWoodCollectionDay = currentDay;
    }

    public void ReleasePendingReward()
    {
        if (pendingWoodReward <= 0 || resources == null) return;
        int wood = pendingWoodReward;
        pendingWoodReward = 0;
        resources.AddWood(wood);
        if (resourceHud != null && card != null && card.CardRect != null)
            resourceHud.FlyFromRect(ResourceKind.Wood, wood, card.CardRect);
    }

    public void StartDay(int day, SeedCatalog catalog, Color? previousBackdrop = null) =>
        StartDay(day, catalog, previousBackdrop, false);

    internal void PrepareFirstDay(int day, SeedCatalog catalog) => StartDay(day, catalog, null, true);

    internal bool HasPreparedEvent => preparedEvent != SertaoEventKind.None && card != null;

    private void StartDay(int day, SeedCatalog catalog, Color? previousBackdrop, bool deferPresentation)
    {
        preparedEvent = SertaoEventKind.None;
        currentDay = day;
        stolenFishToday = 0;
        pestRegressionPending = false;
        pestTargetsWithEmptyBar.Clear();
        for (int i = activeGeneralEvents.Count - 1; i >= 0; i--)
            if (--activeGeneralEvents[i].DaysRemaining <= 0) activeGeneralEvents.RemoveAt(i);
        for (int i = activeComadre.Count - 1; i >= 0; i--)
        {
            activeComadre[i].DaysRemaining--;
            if (activeComadre[i].DaysRemaining <= 0) activeComadre.RemoveAt(i);
        }

        if (!IsActive(SertaoEventKind.Pests)) pestTarget = CropType.None;
        SertaoEventKind announced = SertaoEventKind.None;
        if (day > 0 && lastAnnounced != SertaoEventKind.Comadre && activeComadre.Count == 0 &&
            forestExploitation >= forestExploitationThreshold && Random.value < comadreChance)
        {
            var candidates = new System.Collections.Generic.List<WorkerAssignment>();
            foreach (var worker in workers)
                if (worker != null && !worker.IsUnavailableToday) candidates.Add(worker);
            if (candidates.Count > 1)
            {
                candidates[Random.Range(0, candidates.Count)].ScheduleAbsence(comadreAbsenceDays);
                activeComadre.Add(new ActiveComadre
                {
                    StartedOn = day, DaysRemaining = comadreAbsenceDays
                });
                forestExploitation = 0;
                announced = SertaoEventKind.Comadre;
            }
        }
        if (day > 0 && announced == SertaoEventKind.None &&
            day - Mathf.Max(lastWoodCollectionDay, lastReforestationDay) >=
                daysWithoutWoodForReforestation)
        {
            announced = SertaoEventKind.Reforestation;
            lastReforestationDay = day;
            pendingWoodReward = reforestationWood;
        }
        if (day > 0 && announced == SertaoEventKind.None &&
            (consecutiveCalmDays >= MaxConsecutiveCalmDays || Random.value < generalEventChance))
        {
            announced = RollGeneralEvent();
            if (announced == SertaoEventKind.HeadOfCuia)
            {
                // Usa inteiros para que metade exata arredonde para baixo.
                int loss = (int)(((long)resources.Fish * headOfCuiaTheftPercent + 49) / 100);
                if (resources.TryDeliverFish(loss)) stolenFishToday = loss;
            }
            else if (announced != SertaoEventKind.None)
            {
                if (announced == SertaoEventKind.Pests)
                {
                    pestTarget = SelectAffectedCrop();
                    pestRegressionPending = true;
                    // Guarda o progresso da véspera antes da Seca ou dos bônus da mandala.
                    if (pestTarget != CropType.None)
                        foreach (Plot plot in plots)
                            if (plot != null && plot.Crop == pestTarget && plot.GrowthAge(day - 1) <= 0)
                                pestTargetsWithEmptyBar.Add(plot);
                }
                activeGeneralEvents.Add(new ActiveGeneral
                {
                    Kind = announced, StartedOn = day, DaysRemaining = DurationFor(announced)
                });
            }
        }

        if (IsActive(SertaoEventKind.Drought))
        {
            foreach (var plot in plots)
                if (plot != null && !plot.IsWilting && (MandalaSynergy.Instance == null ||
                    !MandalaSynergy.Instance.ProtectsFromDrought(plot)))
                    plot.AdvanceGrowth(-droughtGrowthDelay);
        }
        else if (IsActive(SertaoEventKind.Rain))
        {
            if (catalog != null && rainGrowthBonus > 0)
                foreach (Plot plot in plots)
                {
                    if (plot == null) continue;
                    CropSettings crop = catalog.Get(plot.Crop);
                    if (crop == null) continue;
                    int advance = Mathf.Min(rainGrowthBonus, crop.growthDays - plot.GrowthAge(day));
                    if (advance > 0) plot.AdvanceGrowth(advance);
                }
            forestExploitation = Mathf.Max(0, forestExploitation - rainForestRecovery);
        }
        if (announced == SertaoEventKind.None) announced = SertaoEventKind.Calm;
        var activeToday = new List<TimelineEvent>();
        foreach (ActiveGeneral active in activeGeneralEvents)
            activeToday.Add(new TimelineEvent(Title(active.Kind),
                Description(active.Kind), Art(active.Kind), active.StartedOn, active.Kind));
        foreach (ActiveComadre comadre in activeComadre)
            activeToday.Add(new TimelineEvent(Title(SertaoEventKind.Comadre),
                Description(SertaoEventKind.Comadre),
                Art(SertaoEventKind.Comadre), comadre.StartedOn, SertaoEventKind.Comadre));
        if (announced == SertaoEventKind.Calm)
            activeToday.Add(new TimelineEvent(Title(announced),
                Description(announced), Art(announced), day, announced));
        else if (announced == SertaoEventKind.HeadOfCuia ||
                 announced == SertaoEventKind.Reforestation)
            activeToday.Add(new TimelineEvent(Title(announced),
                Description(announced), Art(announced), day, announced));
        activeToday.Sort((a, b) => a.StartedOn.CompareTo(b.StartedOn));
        eventsByDay[day] = activeToday;
        lastAnnounced = announced;
        consecutiveCalmDays = day > 0 && announced == SertaoEventKind.Calm
            ? Mathf.Min(consecutiveCalmDays + 1, MaxConsecutiveCalmDays)
            : 0;
        WeatherChanged?.Invoke();
        if (announced == SertaoEventKind.None || card == null) return;
        if (deferPresentation)
        {
            card.Prepare(Title(announced), AnnouncedDescription(announced), Art(announced), previousBackdrop);
            preparedEvent = announced;
            return;
        }
        card.Show(Title(announced), AnnouncedDescription(announced), Art(announced), previousBackdrop);
        PlayEventSound(announced);
    }

    private SertaoEventKind RollGeneralEvent()
    {
        if (lastAnnounced != SertaoEventKind.HeadOfCuia && resources != null &&
            resources.Fish >= headOfCuiaMinimumFish &&
            Random.Range(0, 100) < headOfCuiaChancePercent)
            return SertaoEventKind.HeadOfCuia;

        var options = new List<SertaoEventKind>();
        foreach (SertaoEventKind kind in new[] { SertaoEventKind.Drought, SertaoEventKind.Pests,
                     SertaoEventKind.Rain })
        {
            if (kind == SertaoEventKind.Pests && (plots == null ||
                !System.Array.Exists(plots, plot => plot != null && plot.Crop != CropType.None)))
                continue;
            if (kind == lastAnnounced || IsActive(kind) ||
                (kind == SertaoEventKind.Drought && IsActive(SertaoEventKind.Rain)) ||
                (kind == SertaoEventKind.Rain && IsActive(SertaoEventKind.Drought)))
                continue;
            options.Add(kind);
        }
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : SertaoEventKind.None;
    }

    internal void RevealPreparedBackdrop()
    {
        if (HasPreparedEvent) card.RevealPreparedBackdrop();
    }

    internal void ShowPreparedEvent()
    {
        if (!HasPreparedEvent) return;
        SertaoEventKind announced = preparedEvent;
        preparedEvent = SertaoEventKind.None;
        card.PlayPreparedEntrance(firstDay: true);
        PlayEventSound(announced);
    }

    private static void PlayEventSound(SertaoEventKind announced)
    {
        switch (announced)
        {
            case SertaoEventKind.Drought: AudioManager.Play(SoundCue.EventDrought); break;
            case SertaoEventKind.Rain: AudioManager.Play(SoundCue.EventRain); break;
            case SertaoEventKind.Pests: AudioManager.Play(SoundCue.EventPests); break;
            case SertaoEventKind.Comadre: AudioManager.Play(SoundCue.EventComadre); break;
            case SertaoEventKind.HeadOfCuia: AudioManager.Play(SoundCue.EventHeadOfCuia); break;
        }
    }

    public void ApplyPendingPestRegression(SeedCatalog catalog)
    {
        if (!pestRegressionPending) return;
        pestRegressionPending = false;
        ApplyPestRegression(catalog);
        pestTargetsWithEmptyBar.Clear();
    }

    private void ApplyPestRegression(SeedCatalog catalog)
    {
        if (pestTarget == CropType.None || plots == null || catalog == null) return;
        foreach (Plot plot in plots)
        {
            if (plot == null || plot.Crop != pestTarget || plot.IsWilting) continue;
            if (MandalaSynergy.Instance != null &&
                MandalaSynergy.Instance.ProtectsFromPests(plot)) continue;
            if (pestTargetsWithEmptyBar.Contains(plot)) plot.Harvest();
            else plot.RegressGrowthFromPests(pestGrowthRegression, currentDay, catalog);
        }
    }

    private CropType SelectAffectedCrop()
    {
        var options = new List<CropType>();
        if (plots != null)
            foreach (Plot plot in plots)
                if (plot != null && plot.Crop != CropType.None && !options.Contains(plot.Crop))
                    options.Add(plot.Crop);
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : CropType.None;
    }

    private Sprite Art(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => droughtArt,
        SertaoEventKind.Rain => rainArt,
        SertaoEventKind.Comadre => comadreArt,
        SertaoEventKind.Calm => calmArt,
        SertaoEventKind.HeadOfCuia => headOfCuiaArt,
        SertaoEventKind.Pests => pestsArt,
        SertaoEventKind.Reforestation => reforestationArt,
        _ => null
    };

    private string Title(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "SECA",
        SertaoEventKind.Pests => "PRAGAS",
        SertaoEventKind.Rain => "CHUVA",
        SertaoEventKind.Calm => "DIA TRANQUILO",
        SertaoEventKind.HeadOfCuia => "CABEÇA DE CUIA",
        SertaoEventKind.Reforestation => "REFLORESTAMENTO",
        _ => "COMADRE FULOZINHA"
    };

    private string Description(SertaoEventKind kind)
    {
        CardDescriptionCatalog.Entry entry = CardDescriptionCatalog.Instance != null ?
            CardDescriptionCatalog.Instance.Find(CardDescriptionCatalog.EventKey(kind)) : null;
        if (kind == SertaoEventKind.Pests) return PestDescription(entry?.text);
        if (kind == SertaoEventKind.HeadOfCuia)
        {
            string text = entry?.text;
            if (string.IsNullOrEmpty(text)) return DefaultDescription(kind);
            if (text.Contains(HeadOfCuiaLossToken))
                return text.Replace(HeadOfCuiaLossToken, StolenFishLabel);

            // Suporta descrições editadas com número fixo (ex.: "Você perde 2 peixes")
            // sem alterar o catálogo, a diagramação ou o restante da escrita do jogador.
            string dynamicText = System.Text.RegularExpressions.Regex.Replace(
                text, @"\b(perdeu|perde)\s+\d+\s+peixes?\b",
                match => match.Groups[1].Value + " " + StolenFishLabel,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return dynamicText != text ? dynamicText : DefaultDescription(kind);
        }
        return entry != null ? entry.text : DefaultDescription(kind);
    }

    private string PestCropName => pestTarget switch
    {
        CropType.Pumpkin => "Macaxeira",
        CropType.Corn => "Milho",
        CropType.Jerimum => "Jerimum",
        _ => ""
    };

    private string AnnouncedDescription(SertaoEventKind kind)
    {
        string text = Description(kind);
        if (kind == SertaoEventKind.Pests || !IsActive(SertaoEventKind.Pests) ||
            pestTarget == CropType.None) return text;
        return text + "\n\nPraga ainda ativa. Cultura afetada: " + PestCropName + ".";
    }

    private string PestDescription(string text)
    {
        int duration = DurationFor(SertaoEventKind.Pests);
        string durationText = duration + (duration == 1 ? " dia" : " dias");
        string durationLabel = "\n\nDuração: " + durationText + ".";
        if (pestTarget == CropType.None)
            return "A praga chegou quando não havia plantações." + durationLabel;
        if (string.IsNullOrWhiteSpace(text))
            return "Cultura afetada: " + PestCropName +
                ".\n\nA praga atrasa o crescimento das plantas sem proteção, inclusive as que estavam prontas." +
                durationLabel;
        bool hasCropToken = text.Contains(PestCropToken);
        text = text.Replace(PestCropToken, PestCropName).Replace(PestDurationToken, durationText);
        return hasCropToken ? text : "Cultura afetada: " + PestCropName + ".\n\n" + text;
    }

    private string DefaultDescription(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "A seca reduz a pesca e atrasa o crescimento. Duração: " + DurationFor(kind) + " dia(s).",
        SertaoEventKind.Rain => "A chuva acelera o crescimento e reduz o impacto da coleta de madeira. Duração: " + DurationFor(kind) + " dia(s).",
        SertaoEventKind.Calm => "Nenhum novo evento especial começou hoje.",
        SertaoEventKind.HeadOfCuia => "O Cabeça de Cuia roubou " + StolenFishLabel + ".",
        SertaoEventKind.Reforestation => "A exploração sustentável permitiu reflorestar parte da mata. Você recebe " +
            reforestationWood + (reforestationWood == 1 ? " madeira." : " madeiras."),
        _ => "A exploração da floresta desorientou um trabalhador por " + comadreAbsenceDays + " dia(s)."
    };

}
