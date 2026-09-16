using System.Collections.Generic;
using UnityEngine;

public static class AbilityVisuals
{
    private static readonly Dictionary<int, Material> transparentCache = new Dictionary<int, Material>();
    private static readonly Dictionary<int, Material> solidCache = new Dictionary<int, Material>();

    public static Material Transparent(Color color)
    {
        int key = ColorKey(color);
        if (transparentCache.TryGetValue(key, out Material cached) && cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { color = color };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        transparentCache[key] = material;
        return material;
    }

    public static Material SolidUnlit(Color color)
    {
        int key = ColorKey(color);
        if (solidCache.TryGetValue(key, out Material cached) && cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Universal Render Pipeline/Lit");
        Material material = new Material(shader) { color = color };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        solidCache[key] = material;
        return material;
    }

    public static GameObject GroundDisc(Transform parent, float radius, Color color)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "AoEDisc";
        Object.Destroy(disc.GetComponent<Collider>());
        if (parent != null)
        {
            disc.transform.SetParent(parent, false);
        }

        disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        disc.GetComponent<MeshRenderer>().sharedMaterial = Transparent(new Color(color.r, color.g, color.b, 0.22f));
        return disc;
    }

    public static GameObject Dome(Transform parent, float radius, Color color)
    {
        GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.name = "Dome";
        Object.Destroy(dome.GetComponent<Collider>());
        if (parent != null)
        {
            dome.transform.SetParent(parent, false);
        }

        dome.transform.localPosition = Vector3.zero;
        dome.transform.localScale = Vector3.one * (radius * 2f);
        dome.GetComponent<MeshRenderer>().sharedMaterial = Transparent(new Color(color.r, color.g, color.b, 0.12f));
        return dome;
    }

    private static int ColorKey(Color color)
    {
        return Mathf.RoundToInt(color.r * 255f) << 24
            | Mathf.RoundToInt(color.g * 255f) << 16
            | Mathf.RoundToInt(color.b * 255f) << 8
            | Mathf.RoundToInt(color.a * 255f);
    }
}
