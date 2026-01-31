using UnityEngine;

public class KeyCard : MonoBehaviour
{
    private bool isInserted = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isInserted) return;

        KeyBox box = other.GetComponent<KeyBox>();
        if (box != null)
        {
            isInserted = true;

            // Tell the keybox a card was inserted
            box.AddKey();

            // Remove the card (destroy locally)
            Destroy(gameObject);
        }
    }
}
