using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum MandalaCombo
{
    None = 0,
    CornCrop = 1,
    CornAnimal = 2,
    ChickenCrop = 4,
    WaterCrop = 8,
    CowWater = 16
}

public sealed class MandalaSynergy : MonoBehaviour
{
    [Serializable]
    public struct SlotEffect
    {
        public Plot plot;
        public ParticleSystem lightParticles;
    }

    [SerializeField] private Plot[] plots;
    [Tooltip("Slot do poço central.")]
    [SerializeField] private Transform centralWater;
    [Header("Bônus da mandala")]
    [SerializeField, HideInInspector, Min(0)] private int cropGrowthNearCorn = 1;
    [SerializeField, HideInInspector, Min(0)] private int cropYieldNearCorn = 2;
    [SerializeField, HideInInspector, Min(0)] private int animalProductionNearCorn = 1;
    [SerializeField, HideInInspector, Min(0)] private int cropSalePriceNearWaterCow = 2;
    [Header("Uma única emissão por slot; efeitos são editáveis na Hierarchy")]
    [SerializeField] private SlotEffect[] slotEffects;

    private Dictionary<Plot, MandalaCombo> active = new();
    private Dictionary<Plot, MandalaCombo> next = new();
    private SeedCatalog growthCatalog;
    private int evaluatedDay = 1;
    public static MandalaSynergy Instance { get; private set; }
    public event Action Changed;
    internal event Action PlotStateChanged;

    private void Awake()
    {
        Instance = this;
        GameBalance balance = GameBalance.Current;
        if (balance == null) return;
        cropGrowthNearCorn = balance.cropGrowthNearCorn;
        cropYieldNearCorn = balance.cropYieldNearCorn;
        animalProductionNearCorn = balance.animalProductionNearCorn;
        cropSalePriceNearWaterCow = balance.cropSalePriceNearWaterCow;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }


    private void OnEnable()
    {
        if (plots == null) return;
        foreach (Plot plot in plots)
            if (plot != null) plot.StateChanged += OnPlotStateChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (plots != null)
            foreach (Plot plot in plots)
                if (plot != null) plot.StateChanged -= OnPlotStateChanged;
        active.Clear();
        if (slotEffects == null) return;
        foreach (SlotEffect link in slotEffects)
            if (link.lightParticles != null)
                link.lightParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnPlotStateChanged()
    {
        Refresh();
        PlotStateChanged?.Invoke();
    }

    public MandalaCombo For(Plot plot) => plot != null && active.TryGetValue(plot, out var combos)
        ? combos : MandalaCombo.None;
    public int ExtraCropYield(Plot plot) => plot != null &&
        (plot.Crop == CropType.Pumpkin || plot.Crop == CropType.Jerimum) &&
        (For(plot) & MandalaCombo.CornCrop) != 0
        ? cropYieldNearCorn : 0;
    public int ExtraAnimalProduction(Plot plot) => (For(plot) & MandalaCombo.CornAnimal) != 0
        ? animalProductionNearCorn : 0;
    public bool ProtectsFromPests(Plot plot) =>
        (For(plot) & MandalaCombo.ChickenCrop) != 0;
    public bool ProtectsFromDrought(Plot plot) =>
        (For(plot) & MandalaCombo.WaterCrop) != 0;
    public int CropSalePriceBonus(CropType crop)
    {
        if (crop == CropType.None || plots == null) return 0;
        foreach (Plot cow in plots)
        {
            if (cow == null || cow.Animal != AnimalKind.Cow ||
                (For(cow) & MandalaCombo.CowWater) == 0) continue;
            foreach (Plot neighbor in plots)
                if (neighbor != null && neighbor.Crop == crop && cow.IsAdjacentTo(neighbor))
                    return cropSalePriceNearWaterCow;
        }
        return 0;
    }

    public void ApplyGrowthBonuses(int currentDay, SeedCatalog catalog)
    {
        if (plots == null) return;
        evaluatedDay = currentDay;
        growthCatalog = catalog;
        Refresh();
        foreach (Plot plot in plots)
        {
            if (plot != null && (plot.Crop == CropType.Pumpkin ||
                plot.Crop == CropType.Jerimum) && (For(plot) & MandalaCombo.CornCrop) != 0)
                plot.AdvanceGrowthTowardMaturity(cropGrowthNearCorn, currentDay, catalog);
        }
    }

    public void Refresh()
    {
        if (plots == null) return;
        next.Clear();
        foreach (Plot plot in plots)
            if (plot != null) next[plot] = MandalaCombo.None;
        Plot waterPlot = centralWater != null ? centralWater.GetComponent<Plot>() : null;
        for (int i = 0; i < plots.Length; i++)
        {
            Plot a = plots[i];
            if (a == null) continue;
            for (int j = i + 1; j < plots.Length; j++)
            {
                Plot b = plots[j];
                if (b == null) continue;
                if (a == waterPlot || b == waterPlot)
                {
                    Plot neighbor = a == waterPlot ? b : a;
                    if (!waterPlot.IsAdjacentTo(neighbor, true)) continue;
                }
                else if (!a.IsAdjacentTo(b)) continue;
                AddPair(a, b, waterPlot);
                AddPair(b, a, waterPlot);
            }
        }
        bool changed = active.Count != next.Count;
        foreach (var item in next)
            if (!active.TryGetValue(item.Key, out var old) || old != item.Value)
                changed = true;
        if (!changed) return;
        (active, next) = (next, active);
        if (slotEffects != null)
            foreach (SlotEffect link in slotEffects)
            {
                if (link.plot == null || link.lightParticles == null) continue;
                bool shouldPlay = For(link.plot) != MandalaCombo.None;
                if (shouldPlay && !link.lightParticles.isEmitting) link.lightParticles.Play();
                else if (!shouldPlay && link.lightParticles.isEmitting)
                    link.lightParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        Changed?.Invoke();
    }

    private void AddPair(Plot source, Plot other, Plot water)
    {
        if (source.Crop == CropType.Corn && growthCatalog != null &&
            source.IsReady(evaluatedDay, growthCatalog) &&
            (other.Crop == CropType.Pumpkin || other.Crop == CropType.Jerimum))
        {
            next[source] |= MandalaCombo.CornCrop;
            next[other] |= MandalaCombo.CornCrop;
        }
        if (source.Crop == CropType.Corn && other.Animal != AnimalKind.None)
        {
            next[source] |= MandalaCombo.CornAnimal;
            next[other] |= MandalaCombo.CornAnimal;
        }
        if (source.Animal == AnimalKind.Chicken && other.Crop != CropType.None)
        {
            MarkConnectedCombo(source, MandalaCombo.ChickenCrop);
            MarkConnectedCombo(other, MandalaCombo.ChickenCrop);
        }
        if (source == water && source.IsWater && other.Crop != CropType.None)
        {
            next[source] |= MandalaCombo.WaterCrop;
            MarkConnectedCombo(other, MandalaCombo.WaterCrop);
        }
        if (source == water && source.IsWater && other.Animal == AnimalKind.Cow)
        {
            next[source] |= MandalaCombo.CowWater;
            MarkConnectedCombo(other, MandalaCombo.CowWater);
        }
    }

    private void MarkConnectedCombo(Plot plot, MandalaCombo combo)
    {
        next[plot] |= combo;
        if (plot.Crop == CropType.Corn || plot.JoinedPlots == null) return;
        foreach (Plot member in plot.JoinedPlots)
            if (member != null && next.ContainsKey(member)) next[member] |= combo;
    }
}
