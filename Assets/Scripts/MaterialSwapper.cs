using UnityEngine;

public class MaterialSwapper : MonoBehaviour
{
    public Renderer targetRenderer;
    public Material updateMaterial;
    public int materialIndex;
    public void SetUpdateMaterial()
    {
        // Proximity exits can run while a level and its renderers are unloading.
        if (targetRenderer == null)
            return;

        // Change this renderer's bindings without cloning every material in its slots.
        // No shared Material asset properties are modified.
        var mats = targetRenderer.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= mats.Length)
        {
            Debug.LogWarning($"{name}: Material slot {materialIndex} is outside the renderer's {mats.Length} slots.", this);
            return;
        }

        if (mats[materialIndex] == updateMaterial) return;
        mats[materialIndex] = updateMaterial;
        targetRenderer.sharedMaterials = mats;
    }
}
