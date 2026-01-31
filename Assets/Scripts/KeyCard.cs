using UnityEngine;

public class VRKeyCard : MonoBehaviour
{
    public string keyCardID = "KeyCard";

    private void OnEnable()
    {
        var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.AddListener(OnGrab);
    }

    private void OnDisable()
    {
        var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.RemoveListener(OnGrab);
    }

    private void OnGrab(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        // Get the local player's inventory and add the keycard
        PlayerInventory.LocalInventory?.CollectKeyCard(keyCardID);

        Debug.Log($"[KeyCard] {keyCardID} collected locally.");

        // Destroy ONLY the local copy
        Destroy(gameObject);
    }
}
