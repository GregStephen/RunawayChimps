using TMPro;
using UnityEngine;

public class CoconutDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text coconutText;

    private void OnEnable()
    {
        EconomyState.OnChanged += HandleEconomyChanged;
        HandleEconomyChanged(EconomyState.Coconuts, EconomyState.OwnedItemIds);
    }

    private void OnDisable()
    {
        EconomyState.OnChanged -= HandleEconomyChanged;
    }

    private void HandleEconomyChanged(int coconuts, System.Collections.Generic.IReadOnlyCollection<string> owned)
    {
        if (coconutText != null)
            coconutText.text = coconuts.ToString();
    }
}
