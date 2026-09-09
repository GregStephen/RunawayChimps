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

        var mats = targetRenderer.materials;
        if (materialIndex < 0 || materialIndex >= mats.Length)
        {
            Debug.LogWarning($"{name}: Material slot {materialIndex} is outside the renderer's {mats.Length} slots.", this);
            return;
        }

        mats[materialIndex] = updateMaterial;
        targetRenderer.materials = mats;
    }
}
