using UnityEngine;

public class OperativeCosmetics : MonoBehaviour
{
    [SerializeField] private GameObject genericBody;
    [SerializeField] private GameObject genericHead;
    [SerializeField] private OperativeId modelOperative = OperativeId.Cipher;
    [SerializeField] private GameObject modelVisual;

    public void SetOperative(OperativeId id)
    {
        bool useModel = modelVisual != null;

        if (genericBody != null) genericBody.SetActive(!useModel);
        if (genericHead != null) genericHead.SetActive(!useModel);
        if (modelVisual != null) modelVisual.SetActive(useModel);
    }

    public void ConfigureOwnership(bool isLocalPlayer)
    {
        int layer = isLocalPlayer ? LayerMask.NameToLayer("LocalBody") : 0;
        if (layer < 0)
        {
            return;
        }

        SetLayerRecursive(genericBody, layer);
        SetLayerRecursive(genericHead, layer);
        SetLayerRecursive(modelVisual, layer);
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}
