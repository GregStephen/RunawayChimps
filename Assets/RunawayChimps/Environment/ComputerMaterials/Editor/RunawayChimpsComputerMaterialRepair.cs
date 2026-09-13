using UnityEditor;
using UnityEngine;

public static class RunawayChimpsComputerMaterialRepair
{
    private const string Root = "Assets/RunawayChimps/Environment/ComputerMaterials";

    [MenuItem("Tools/Runaway Chimps/Repair Computer Materials (Built-in)")]
    public static void Repair()
    {
        var standard = Shader.Find("Standard");
        if (standard == null)
        {
            Debug.LogError("[ComputerMaterials] Built-in Standard shader was not found.");
            return;
        }

        RepairOne("MAT_Computer_PaintedSecuritySteel", "PaintedSecuritySteel", standard, false);
        RepairOne("MAT_Computer_BlackABS", "BlackABS", standard, false);
        RepairOne("MAT_Computer_DarkOxidizedMetal", "DarkOxidizedMetal", standard, false);
        RepairOne("MAT_Computer_ScreenGlass", "ScreenGlass", standard, true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ComputerMaterials] Repaired all four computer materials for the Built-in Render Pipeline.");
    }

    private static void RepairOne(string materialName, string textureStem, Shader standard, bool transparent)
    {
        string matPath = Root + "/Materials/" + materialName + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Debug.LogError("[ComputerMaterials] Missing material: " + matPath);
            return;
        }

        string texRoot = Root + "/Textures/" + textureStem + "/" + textureStem;
        mat.shader = standard;
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(texRoot + "_BaseColor.png"));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture>(texRoot + "_Normal.png"));
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture>(texRoot + "_MetallicSmoothness.png"));
        mat.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture>(texRoot + "_Occlusion.png"));
        mat.SetFloat("_BumpScale", textureStem == "ScreenGlass" ? 0.22f : 0.8f);
        mat.SetFloat("_GlossMapScale", 1f);
        mat.SetFloat("_OcclusionStrength", 1f);
        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_METALLICGLOSSMAP");

        if (transparent)
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            Color c = mat.color;
            c.a = 0.34f;
            mat.color = c;
        }
        else
        {
            mat.SetFloat("_Mode", 0f);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
            Color c = mat.color;
            c.a = 1f;
            mat.color = c;
        }

        EditorUtility.SetDirty(mat);
    }

    [MenuItem("Tools/Runaway Chimps/Validate Computer Materials")]
    public static void ValidateMaterials()
    {
        string[] names = {
            "MAT_Computer_PaintedSecuritySteel",
            "MAT_Computer_BlackABS",
            "MAT_Computer_DarkOxidizedMetal",
            "MAT_Computer_ScreenGlass"
        };

        bool ok = true;
        foreach (string name in names)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogError("[ComputerMaterials] Missing: " + path);
                ok = false;
                continue;
            }
            if (mat.shader == null || mat.shader.name != "Standard")
            {
                Debug.LogError("[ComputerMaterials] " + name + " is not using Built-in Standard.");
                ok = false;
            }
            if (mat.GetTexture("_MainTex") == null || mat.GetTexture("_BumpMap") == null ||
                mat.GetTexture("_MetallicGlossMap") == null || mat.GetTexture("_OcclusionMap") == null)
            {
                Debug.LogError("[ComputerMaterials] " + name + " is missing one or more PBR texture bindings.");
                ok = false;
            }
        }

        if (ok)
            Debug.Log("[ComputerMaterials] All four computer materials use Built-in Standard and have their PBR maps assigned.");
    }
}
