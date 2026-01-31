using UnityEngine;

public class MaterialSwapper : MonoBehaviour
{
    public Renderer targetRenderer;
    public Material updateMaterial;
    public int materialIndex;
    public void SetUpdateMaterial()
    {
        var mats = targetRenderer.materials;
        if (mats != null && mats.Length > 0 )
        {
            mats[materialIndex] = updateMaterial;
            targetRenderer.materials = mats;
        }
        
    }
}