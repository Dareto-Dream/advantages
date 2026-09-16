using UnityEngine;

public class AbilityFx : MonoBehaviour
{
    private float age;
    private float life = 0.35f;
    private float targetRadius = 2f;
    private MeshRenderer meshRenderer;
    private Color color = Color.white;

    public static void Flash(Vector3 position, float radius, Color color, float life = 0.35f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "AbilityFlash";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.4f;

        AbilityFx fx = go.AddComponent<AbilityFx>();
        fx.targetRadius = Mathf.Max(0.5f, radius);
        fx.life = life;
        fx.color = color;
        fx.meshRenderer = go.GetComponent<MeshRenderer>();
        fx.meshRenderer.sharedMaterial = AbilityVisuals.Transparent(new Color(color.r, color.g, color.b, 0.5f));
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);

        float diameter = Mathf.Lerp(0.8f, targetRadius * 2f, t);
        transform.localScale = Vector3.one * diameter;

        if (meshRenderer != null)
        {
            Color c = meshRenderer.material.color;
            c.a = Mathf.Lerp(0.5f, 0f, t);
            meshRenderer.material.color = c;
        }

        if (age >= life)
        {
            Destroy(gameObject);
        }
    }
}
