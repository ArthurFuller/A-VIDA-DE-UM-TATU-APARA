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
    [Min(1)] public int availableFromDay = 1;
    [Min(0), Tooltip("Dias adicionais para a entrega a partir da oferta.")]
    public int deliveryDays = 2;
    [Tooltip("Disputa a vaga de encomenda grande do dia depois de ser liberada.")]
    public bool largeOrder;
}

public sealed class DeliveryMissionCatalog : ScriptableObject
{
    public List<DeliveryMissionTemplate> entries = new();

    public static bool IsUsable(DeliveryMissionTemplate template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.title) ||
            template.coinReward < 0 || template.requirements == null ||
            template.requirements.Count == 0) return false;
        long totalUnits = 0;
        foreach (DeliveryRequirement requirement in template.requirements)
        {
            if (requirement == null || requirement.quantity < 1 ||
                (int)requirement.item < 0 || (int)requirement.item > (int)SaleItem.Eggs)
                return false;
            totalUnits += requirement.quantity;
        }
        return totalUnits >= 2;
    }

}
