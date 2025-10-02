using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class SkinToneManager : MonoBehaviour
{
    [Header("Gerçekçi Cilt Tonları")]
    [Tooltip("Inspector’da ekleyebileceğiniz 4–5 ten rengi")]
    public Color[] skinTones = new Color[]
    {
        new Color(0.992f, 0.878f, 0.769f), // Açık ten
        new Color(0.941f, 0.741f, 0.502f), // Buğday ten
        new Color(0.780f, 0.537f, 0.318f), // Orta esmer
        new Color(0.552f, 0.345f, 0.196f), // Koyu esmer
        new Color(0.200f, 0.090f, 0.050f)  // Siyahi ton
    };

    private SkinnedMeshRenderer skinRenderer;

    void Awake()
    {
        skinRenderer = GetComponent<SkinnedMeshRenderer>();

        if (skinTones.Length == 0)
        {
            Debug.LogWarning("Skin tones dizisi boş!");
            return;
        }

        // Rastgele bir indeks seç
        int idx = Random.Range(0, skinTones.Length);
        Color chosen = skinTones[idx];

        // URP/HDRP kullanıyorsanız materyalinizin Base Color (_BaseColor) property’sini; 
        // standart shader ise "_Color" property’sini kullanabilirsiniz:
        if (skinRenderer.material.HasProperty("_BaseColor"))
            skinRenderer.material.SetColor("_BaseColor", chosen);
        else if (skinRenderer.material.HasProperty("_Color"))
            skinRenderer.material.SetColor("_Color", chosen);
        else
            Debug.LogError("Materyal üzerinde renk property’si bulunamadı!");
    }
}