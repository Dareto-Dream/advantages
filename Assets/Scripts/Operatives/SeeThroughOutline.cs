using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SeeThroughOutline : MonoBehaviour
{
    private static readonly string[] SkipNames =
    {
        "ReconMarker", "HitHealthBar", "Backing", "Fill", "AoEDisc", "Dome", "SeeThroughClone"
    };

    private Material material;
    private readonly List<MeshRenderer> cloneRenderers = new List<MeshRenderer>();
    private float activeUntil = -1f;
    private bool built;
    private bool shown;
    private Color color = new Color(1f, 0.35f, 0.2f, 0.85f);

    public static SeeThroughOutline For(Component host)
    {
        if (host == null)
        {
            return null;
        }

        SeeThroughOutline existing = host.GetComponent<SeeThroughOutline>();
        return existing != null ? existing : host.gameObject.AddComponent<SeeThroughOutline>();
    }

    public void Show(Color tint, float until)
    {
        color = new Color(tint.r, tint.g, tint.b, 0.85f);
        activeUntil = Mathf.Max(activeUntil, until);

        EnsureBuilt();
        if (material != null)
        {
            material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        SetShown(true);
    }

    private void Update()
    {
        if (shown && Time.time >= activeUntil)
        {
            SetShown(false);
        }
    }

    private void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        built = true;

        Shader shader = Shader.Find("Advantage/SeeThroughSilhouette");
        if (shader == null)
        {

            shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        }

        material = new Material(shader) { name = "SeeThroughSilhouette (inst)" };
        material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        foreach (MeshRenderer source in CollectBodyRenderers())
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                continue;
            }

            GameObject clone = new GameObject("SeeThroughClone");
            clone.transform.SetParent(source.transform, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            clone.layer = source.gameObject.layer;

            clone.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer renderer = clone.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            cloneRenderers.Add(renderer);
        }
    }

    private IEnumerable<MeshRenderer> CollectBodyRenderers()
    {
        List<MeshRenderer> found = new List<MeshRenderer>();

        Transform visuals = transform.Find("Visuals");
        if (visuals != null)
        {
            found.AddRange(visuals.GetComponentsInChildren<MeshRenderer>(true));
        }

        Transform head = transform.Find("Head");
        if (head != null)
        {
            found.AddRange(head.GetComponentsInChildren<MeshRenderer>(true));
        }

        found.RemoveAll(r => r == null || System.Array.IndexOf(SkipNames, r.gameObject.name) >= 0);
        return found;
    }

    private void SetShown(bool value)
    {
        shown = value;

        cloneRenderers.RemoveAll(r => r == null);
        foreach (MeshRenderer renderer in cloneRenderers)
        {
            renderer.enabled = value;
        }
    }

    private void OnDisable()
    {
        SetShown(false);
        activeUntil = -1f;
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
