using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory LocalInventory;

    // Runtime tracking of collected cards
    public List<string> collectedCards = new List<string>();

    // Assign your card prefabs here in the Inspector
    public List<VRKeyCard> keyCardPrefabs;

    private void Awake()
    {
        LocalInventory = this;
    }

    public void CollectKeyCard(string id)
    {
        if (!collectedCards.Contains(id))
            collectedCards.Add(id);

        Debug.Log("[Inventory] Collected: " + id);
    }

    public void DropAllKeyCards(Vector3 dropPosition)
    {
        Debug.Log("[Inventory] Dropping all keycards...");

        foreach (string id in collectedCards)
        {
            VRKeyCard prefab = keyCardPrefabs.Find(p => p.keyCardID == id);

            if (prefab != null)
            {
                // Spawn a new local keycard at player location
                Instantiate(prefab, dropPosition, Quaternion.identity);
                Debug.Log("[Inventory] Dropped keycard: " + id);
            }
            else
            {
                Debug.LogWarning("[Inventory] No prefab found for keycard: " + id);
            }
        }

        collectedCards.Clear();
    }
}
