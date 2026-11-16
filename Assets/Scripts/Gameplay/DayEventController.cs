using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public enum SertaoEventKind { None, Drought, Pests, Rain, Comadre, Calm, HeadOfCuia, Reforestation }

public sealed class DayEventController : MonoBehaviour
{
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
    [SerializeField, HideInInspector, Min(1)] private int headOfCuiaFishTheft = 2;
    [Header("Reflorestamento")]
    [SerializeField, HideInInspector, Min(1)]
    private int daysWithoutWoodForReforestation = 3;
    [SerializeField, HideInInspector, Min(1)]
    private int reforestationWood = 4;

    private readonly List<ActiveGeneral> activeGeneralEvents = new();
    private SertaoEventKind lastAnnounced;
    private int forestExploitation;
    private int stolenFishToday;
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
        headOfCuiaFishTheft = balance.headOfCuiaFishTheft;
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
    public bool IsRaining => IsActive(SertaoEventKind.Rain);
    public bool IsDrought => IsActive(SertaoEventKind.Drought);
    public event System.Action WeatherChanged;
    public CropType PestTarget => pestTarget;
    public int HarvestReductionFor(Plot plot) =>
        plot != null && plot.Crop != CropType.None && plot.Crop == pestTarget &&
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

    public void StartDay(int day, Color? previousBackdrop = null)
    {
        currentDay = day;
        stolenFishToday = 0;
        pestRegressionPending = false;
        for (int i = activeGeneralEvents.Count - 1; i >= 0; i--)
            if (--activeGeneralEvents[i].DaysRemaining <= 0) activeGeneralEvents.RemoveAt(i);
        for (int i = activeComadre.Count - 1; i >= 0; i--)
        {
            activeComadre[i].DaysRemaining--;
            if (activeComadre[i].DaysRemaining <= 0) activeComadre.RemoveAt(i);
        }

        if (!IsActive(SertaoEventKind.Pests)) pestTarget = CropType.None;
        SertaoEventKind announced = SertaoEventKind.None;
        if (lastAnnounced != SertaoEventKind.Comadre && activeComadre.Count == 0 &&
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
        if (announced == SertaoEventKind.None &&
            day - Mathf.Max(lastWoodCollectionDay, lastReforestationDay) >=
                daysWithoutWoodForReforestation)
        {
            announced = SertaoEventKind.Reforestation;
            lastReforestationDay = day;
            pendingWoodReward = reforestationWood;
        }
        if (announced == SertaoEventKind.None && Random.value < generalEventChance)
        {
            var options = new List<SertaoEventKind>();
            foreach (SertaoEventKind kind in new[] { SertaoEventKind.Drought, SertaoEventKind.Pests,
                         SertaoEventKind.Rain, SertaoEventKind.HeadOfCuia })
            {
                if (kind == lastAnnounced || IsActive(kind) ||
                    (kind == SertaoEventKind.Drought && IsActive(SertaoEventKind.Rain)) ||
                    (kind == SertaoEventKind.Rain && IsActive(SertaoEventKind.Drought)) ||
                    (kind == SertaoEventKind.HeadOfCuia && (resources == null || resources.Fish <= 0)))
                    continue;
                options.Add(kind);
            }
            if (options.Count > 0) announced = options[Random.Range(0, options.Count)];
            if (announced == SertaoEventKind.HeadOfCuia)
            {
                stolenFishToday = Mathf.Min(resources.Fish, headOfCuiaFishTheft);
                resources.TryDeliverFish(stolenFishToday);
            }
            else if (announced != SertaoEventKind.None)
            {
                if (announced == SertaoEventKind.Pests)
                {
                    pestTarget = SelectAffectedCrop();
                    pestRegressionPending = true;
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
                if (plot != null && (MandalaSynergy.Instance == null ||
                    !MandalaSynergy.Instance.ProtectsFromDrought(plot)))
                    plot.AdvanceGrowth(-droughtGrowthDelay);
        }
        else if (IsActive(SertaoEventKind.Rain))
        {
            foreach (var plot in plots) if (plot != null) plot.AdvanceGrowth(rainGrowthBonus);
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
        WeatherChanged?.Invoke();
        if (announced != SertaoEventKind.None && card != null)
        {
            card.Show(Title(announced), Description(announced), Art(announced), previousBackdrop);
            switch (announced)
            {
                case SertaoEventKind.Drought: AudioManager.Play(SoundCue.EventDrought); break;
                case SertaoEventKind.Rain: AudioManager.Play(SoundCue.EventRain); break;
                case SertaoEventKind.Pests: AudioManager.Play(SoundCue.EventPests); break;
                case SertaoEventKind.Comadre: AudioManager.Play(SoundCue.EventComadre); break;
                case SertaoEventKind.HeadOfCuia: AudioManager.Play(SoundCue.EventHeadOfCuia); break;
            }
        }
    }

    public void ApplyPendingPestRegression(SeedCatalog catalog)
    {
        if (!pestRegressionPending) return;
        pestRegressionPending = false;
        ApplyPestRegression(catalog);
    }

    private void ApplyPestRegression(SeedCatalog catalog)
    {
        if (pestTarget == CropType.None || plots == null || catalog == null) return;
        foreach (Plot plot in plots)
        {
            if (plot == null || plot.Crop != pestTarget) continue;
            if (MandalaSynergy.Instance != null &&
                MandalaSynergy.Instance.ProtectsFromPests(plot)) continue;
            plot.RegressGrowthFromPests(pestGrowthRegression, currentDay, catalog);
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
        return entry != null ? entry.text : DefaultDescription(kind);
    }

    private string DefaultDescription(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "A seca reduz a pesca e atrasa o crescimento. Duração: " + DurationFor(kind) + " dia(s).",
        SertaoEventKind.Pests => "As pragas afetam " +
            (pestTarget == CropType.Pumpkin ? "a macaxeira" :
             pestTarget == CropType.Corn ? "o milho" :
             pestTarget == CropType.Jerimum ? "o jerimum" : "as lavouras") +
            ". Duração: " + DurationFor(kind) + " dia(s).",
        SertaoEventKind.Rain => "A chuva acelera o crescimento e reduz o impacto da coleta de madeira. Duração: " + DurationFor(kind) + " dia(s).",
        SertaoEventKind.Calm => "Nenhum novo evento especial começou hoje.",
        SertaoEventKind.HeadOfCuia => "O Cabeça de Cuia roubou " + stolenFishToday +
            (stolenFishToday == 1 ? " peixe." : " peixes."),
        SertaoEventKind.Reforestation => "A exploração sustentável permitiu reflorestar parte da mata. Você recebe " +
            reforestationWood + (reforestationWood == 1 ? " madeira." : " madeiras."),
        _ => "A exploração da floresta desorientou um trabalhador por " + comadreAbsenceDays + " dia(s)."
    };

}
