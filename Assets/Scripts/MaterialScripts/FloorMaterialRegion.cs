using UnityEngine;

[ExecuteAlways]
public class RandomTileRegion : MonoBehaviour
{
    public Material[] variations;

    [Tooltip("If checked, randomizes immediately when you toggle this value.")]
    public bool randomizeNow = false;

    void Update()
    {
        // Editor-only manual trigger
        if (!Application.isPlaying && randomizeNow)
        {
            randomizeNow = false; // Reset toggle
            ApplyRandomTiles();
        }
    }

    [ContextMenu("Randomize Tiles Now")]
    public void ApplyRandomTiles()
    {
        if (variations == null || variations.Length == 0)
            return;

        foreach (Transform child in transform)
        {
            var r = child.GetComponent<Renderer>();
            if (r == null) continue;

            // Pick a random material
            r.sharedMaterial = variations[Random.Range(0, variations.Length)];

            // Random 0, 90, 180, 270 rotation
            int step = Random.Range(0, 4);
            child.localRotation = Quaternion.Euler(0, step * 90f, 0);
        }
    }
}
