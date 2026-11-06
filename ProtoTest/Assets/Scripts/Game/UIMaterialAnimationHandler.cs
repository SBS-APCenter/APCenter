using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stand-in for the game's UIMaterialAnimationHandler. The serialized data shows it is a Mask that forwards
/// Animator-driven values to named material properties of its Graphic; this recreates that behaviour.
/// </summary>
public class UIMaterialAnimationHandler : Mask
{
    public string floatPropName_1; public float floatPropValue_1;
    public string floatPropName_2; public float floatPropValue_2;
    public string floatPropName_3; public float floatPropValue_3;
    public string floatPropName_4; public float floatPropValue_4;
    public string colorPropName_1; public Color colorPropValue_1;
    public string colorPropName_2; public Color colorPropValue_2;
    public string vectorPropName_1; public Vector4 vectorPropValue_1;
    public string vectorPropName_2; public Vector4 vectorPropValue_2;
    public string texTOPropName_1; public Vector4 texTOPropValue_1 = new Vector4(1, 1, 0, 0);
    public string texTOPropName_2; public Vector4 texTOPropValue_2 = new Vector4(1, 1, 0, 0);
    public bool maskEnabled;
    public new bool showMaskGraphic; // serialized separately from Mask.m_ShowMaskGraphic in the game data

    Material instance;
    Material instanceSource;
    int lastHash;

    public override bool MaskEnabled() => maskEnabled && base.MaskEnabled();

    public override Material GetModifiedMaterial(Material baseMaterial)
    {
        if (baseMaterial == null)
            return base.GetModifiedMaterial(baseMaterial);
        if (instance == null || instanceSource != baseMaterial)
        {
            if (instance != null) DestroyImmediate(instance);
            instance = new Material(baseMaterial) { name = baseMaterial.name + " (anim)", hideFlags = HideFlags.HideAndDontSave };
            instanceSource = baseMaterial;
        }
        instance.CopyPropertiesFromMaterial(baseMaterial); // picks up edits made to the base material after the copy
        ApplyTo(instance);
        return base.GetModifiedMaterial(instance);
    }

    void LateUpdate()
    {
        int hash = ValueHash();
        if (hash == lastHash || graphic == null)
            return;
        lastHash = hash;
        graphic.SetMaterialDirty();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (instance != null) DestroyImmediate(instance);
    }

    void ApplyTo(Material m)
    {
        SetFloat(m, floatPropName_1, floatPropValue_1);
        SetFloat(m, floatPropName_2, floatPropValue_2);
        SetFloat(m, floatPropName_3, floatPropValue_3);
        SetFloat(m, floatPropName_4, floatPropValue_4);
        if (!string.IsNullOrEmpty(colorPropName_1)) m.SetColor(colorPropName_1, colorPropValue_1);
        if (!string.IsNullOrEmpty(colorPropName_2)) m.SetColor(colorPropName_2, colorPropValue_2);
        if (!string.IsNullOrEmpty(vectorPropName_1)) m.SetVector(vectorPropName_1, vectorPropValue_1);
        if (!string.IsNullOrEmpty(vectorPropName_2)) m.SetVector(vectorPropName_2, vectorPropValue_2);
        SetTilingOffset(m, texTOPropName_1, texTOPropValue_1);
        SetTilingOffset(m, texTOPropName_2, texTOPropValue_2);
    }

    static void SetFloat(Material m, string name, float value)
    {
        if (!string.IsNullOrEmpty(name)) m.SetFloat(name, value);
    }

    static void SetTilingOffset(Material m, string name, Vector4 v)
    {
        if (string.IsNullOrEmpty(name) || !m.HasProperty(name)) return;
        m.SetTextureScale(name, new Vector2(v.x, v.y));
        m.SetTextureOffset(name, new Vector2(v.z, v.w));
    }

    /// <summary>Lets editor tools push animated values after sampling a clip outside play mode.</summary>
    public void ForceRefresh()
    {
        lastHash = 0;
        if (graphic != null) graphic.SetMaterialDirty();
    }

    int ValueHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + floatPropValue_1.GetHashCode();
            h = h * 31 + floatPropValue_2.GetHashCode();
            h = h * 31 + floatPropValue_3.GetHashCode();
            h = h * 31 + floatPropValue_4.GetHashCode();
            h = h * 31 + colorPropValue_1.GetHashCode();
            h = h * 31 + colorPropValue_2.GetHashCode();
            h = h * 31 + vectorPropValue_1.GetHashCode();
            h = h * 31 + vectorPropValue_2.GetHashCode();
            h = h * 31 + texTOPropValue_1.GetHashCode();
            h = h * 31 + texTOPropValue_2.GetHashCode();
            return h;
        }
    }
}
