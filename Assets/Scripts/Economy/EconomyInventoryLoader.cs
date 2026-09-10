using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.EconomyModels;
using UnityEngine;

// Publish only a complete inventory; a failed later page must not erase ownership.
public static class EconomyInventoryLoader
{
    private static int generation;

    public static void Refresh(string entityId, string entityType, string currencyId)
    {
        if (string.IsNullOrWhiteSpace(entityId) || string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(currencyId))
        {
            Debug.LogWarning("Economy refresh needs an entity and currency ID.");
            return;
        }
        int request = ++generation;
        var items = new List<InventoryItem>();
        var tokens = new HashSet<string>();
        Action<string> fetchPage = null;
        fetchPage = token =>
        {
            if (request != generation) return;
            try
            {
                PlayFabEconomyAPI.GetInventoryItems(new GetInventoryItemsRequest
                {
                    Entity = new EntityKey { Id = entityId, Type = entityType },
                    ContinuationToken = token,
                    Count = 50
                }, response =>
                {
                    if (request != generation) return;
                    if (response.Items != null) items.AddRange(response.Items);
                    if (!string.IsNullOrEmpty(response.ContinuationToken))
                    {
                        if (!tokens.Add(response.ContinuationToken))
                        {
                            Debug.LogWarning("Economy returned a repeated inventory page token. Keeping the previous inventory.");
                            return;
                        }
                        fetchPage(response.ContinuationToken);
                        return;
                    }
                    ApplySnapshot(items, currencyId);
                }, error => Debug.LogWarning("Inventory refresh failed: " + error.ErrorMessage));
            }
            catch (Exception exception) { Debug.LogException(exception); }
        };
        fetchPage(null);
    }

    public static void ApplySnapshot(IEnumerable<InventoryItem> items, string currencyId)
    {
        long currency = 0;
        var owned = new HashSet<string>();
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) continue;
            int amount = Math.Max(0, item.Amount ?? 0);
            if (item.Id == currencyId) currency += amount;
            else if (amount > 0) owned.Add(item.Id);
        }
        EconomyState.Set((int)Math.Min(int.MaxValue, currency), owned);
    }
}
