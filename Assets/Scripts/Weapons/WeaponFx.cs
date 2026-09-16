using System.Collections.Generic;
using UnityEngine;

public class WeaponFx : MonoBehaviour
{
    private static WeaponFx instance;

    [SerializeField] private int tracerPoolSize = 24;
    [SerializeField] private float tracerLifetime = 0.055f;
    [SerializeField] private int impactPoolSize = 24;
    [SerializeField] private float impactLifetime = 0.35f;

    private readonly List<LineRenderer> tracers = new List<LineRenderer>();
    private readonly List<float> tracerExpiry = new List<float>();
    private readonly List<Transform> impacts = new List<Transform>();
    private readonly List<float> impactExpiry = new List<float>();

    private Material lineMaterial;
    private int nextTracer;
    private int nextImpact;

    public static WeaponFx Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("~WeaponFx");
                instance = host.AddComponent<WeaponFx>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildPools();
    }

    private void Update()
    {
        float now = Time.time;

        for (int i = 0; i < tracers.Count; i++)
        {
            if (tracers[i].enabled && now >= tracerExpiry[i])
            {
                tracers[i].enabled = false;
            }
        }

        for (int i = 0; i < impacts.Count; i++)
        {
            if (impacts[i].gameObject.activeSelf && now >= impactExpiry[i])
            {
                impacts[i].gameObject.SetActive(false);
            }
        }
    }

    public void PlayTracer(Vector3 from, Vector3 to, Color color)
    {
        if (tracers.Count == 0)
        {
            return;
        }

        int index = nextTracer;
        LineRenderer line = tracers[index];
        nextTracer = (nextTracer + 1) % tracers.Count;

        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0f);
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.enabled = true;
        tracerExpiry[index] = Time.time + tracerLifetime;
    }

    public void PlayImpact(Vector3 point, Vector3 normal, Color color)
    {
        if (impacts.Count == 0)
        {
            return;
        }

        Transform impact = impacts[nextImpact];
        int index = nextImpact;
        nextImpact = (nextImpact + 1) % impacts.Count;

        impact.position = point + normal * 0.01f;
        impact.rotation = Quaternion.LookRotation(normal);
        impact.gameObject.SetActive(true);

        Renderer renderer = impact.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        impactExpiry[index] = Time.time + impactLifetime;
    }

    private void BuildPools()
    {
        lineMaterial = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < tracerPoolSize; i++)
        {
            GameObject go = new GameObject($"Tracer_{i}");
            go.transform.SetParent(transform, false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.positionCount = 2;
            line.startWidth = 0.035f;
            line.endWidth = 0.005f;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            line.enabled = false;

            tracers.Add(line);
            tracerExpiry.Add(0f);
        }

        for (int i = 0; i < impactPoolSize; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Impact_{i}";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.16f;
            Destroy(go.GetComponent<Collider>());

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(lineMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            impacts.Add(go.transform);
            impactExpiry.Add(0f);
        }
    }
}
