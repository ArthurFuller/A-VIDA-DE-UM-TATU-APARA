using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DeliveryRequirement
{
    public SaleItem item;
    [Min(1)] public int quantity = 1;
}

[Serializable]
public sealed class DeliveryMissionTemplate
{
    public string title;
    [TextArea(2, 5)] public string description;
    public List<DeliveryRequirement> requirements = new();
    [Min(0)] public int coinReward;
}

public sealed class DeliveryMissionCatalog : ScriptableObject
{
    public List<DeliveryMissionTemplate> entries = new();

    public static bool IsUsable(DeliveryMissionTemplate template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.title) ||
            template.coinReward < 0 || template.requirements == null ||
            template.requirements.Count == 0) return false;
        foreach (DeliveryRequirement requirement in template.requirements)
            if (requirement == null || requirement.quantity < 1 ||
                (int)requirement.item < 0 || (int)requirement.item > (int)SaleItem.Eggs)
                return false;
        return true;
    }

    public static string ItemName(SaleItem item) => item switch
    {
        SaleItem.Wood => "madeira", SaleItem.Fish => "peixe",
        SaleItem.Corn => "milho", SaleItem.Pumpkin => "macaxeira",
        SaleItem.Food => "comida pronta", SaleItem.CornSeeds => "sementes de milho",
        SaleItem.PumpkinSeeds => "sementes de macaxeira",
        SaleItem.Jerimum => "jerimum", SaleItem.JerimumSeeds => "sementes de jerimum",
        SaleItem.Milk => "leite", SaleItem.Eggs => "ovos",
        _ => "recurso"
    };
}
