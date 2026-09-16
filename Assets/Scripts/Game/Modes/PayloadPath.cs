using UnityEngine;

[ExecuteAlways]
public class PayloadPath : MonoBehaviour
{
    [Tooltip("Ordered attacker-end -> ... -> defender-end. Needs at least three nodes.")]
    [SerializeField] private Transform[] nodes = new Transform[0];

    [Tooltip("Index of the node the payload starts on (path position 0).")]
    [SerializeField] private int middleIndex = 1;

    [Tooltip("Optional - drawn along the nodes so the rail is visible in game.")]
    [SerializeField] private LineRenderer line;

    [SerializeField] private Color gizmoColor = new Color(1f, 0.82f, 0.22f, 1f);

    public bool IsValid => nodes != null && nodes.Length >= 2 && MiddleIndex >= 0 && MiddleIndex < nodes.Length;

    private int MiddleIndex => nodes == null ? 0 : Mathf.Clamp(middleIndex, 0, Mathf.Max(0, nodes.Length - 1));

    public Transform AttackerEnd => IsValid ? nodes[0] : null;
    public Transform DefenderEnd => IsValid ? nodes[nodes.Length - 1] : null;
    public Transform Middle => IsValid ? nodes[MiddleIndex] : null;

    private void OnEnable()
    {
        RefreshLine();
    }

    private void OnValidate()
    {

        RefreshLine();
    }

    private void Update()
    {
        RefreshLine();
    }

    public Vector3 Evaluate(float position)
    {
        if (!IsValid)
        {
            return transform.position;
        }

        int middle = MiddleIndex;

        if (Mathf.Approximately(position, 0f))
        {
            return NodePosition(middle);
        }

        int target = position < 0f ? 0 : nodes.Length - 1;
        float t = Mathf.Clamp01(Mathf.Abs(position) / 100f);
        return WalkLeg(middle, target, t);
    }

    private Vector3 WalkLeg(int from, int to, float t01)
    {
        if (from == to)
        {
            return NodePosition(from);
        }

        int step = to > from ? 1 : -1;

        float total = 0f;
        for (int i = from; i != to; i += step)
        {
            total += Vector3.Distance(NodePosition(i), NodePosition(i + step));
        }

        if (total <= 0.0001f)
        {
            return NodePosition(to);
        }

        float travelled = total * t01;

        for (int i = from; i != to; i += step)
        {
            Vector3 a = NodePosition(i);
            Vector3 b = NodePosition(i + step);
            float segment = Vector3.Distance(a, b);

            if (travelled <= segment || i + step == to)
            {
                return Vector3.Lerp(a, b, segment <= 0.0001f ? 1f : Mathf.Clamp01(travelled / segment));
            }

            travelled -= segment;
        }

        return NodePosition(to);
    }

    private Vector3 NodePosition(int index)
    {
        Transform node = nodes[Mathf.Clamp(index, 0, nodes.Length - 1)];
        return node != null ? node.position : transform.position;
    }

    private void RefreshLine()
    {
        if (line == null || !IsValid)
        {
            return;
        }

        if (line.positionCount != nodes.Length)
        {
            line.positionCount = nodes.Length;
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            line.SetPosition(i, NodePosition(i));
        }
    }

    private void OnDrawGizmos()
    {
        if (!IsValid)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        for (int i = 0; i < nodes.Length - 1; i++)
        {
            Gizmos.DrawLine(NodePosition(i), NodePosition(i + 1));
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            bool endpoint = i == 0 || i == nodes.Length - 1;
            Gizmos.DrawWireSphere(NodePosition(i), endpoint ? 1.2f : i == MiddleIndex ? 1.0f : 0.5f);
        }
    }
}
