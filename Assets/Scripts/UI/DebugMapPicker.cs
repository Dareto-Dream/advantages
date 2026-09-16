using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebugMapPicker : MonoBehaviour
{
    private void Start()
    {
        BuildUi();
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("DebugMapPickerCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        panel.AddComponent<Image>().color = new Color(0.85f, 0.15f, 0.15f, 0.75f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(12f, 12f);
        panelRect.sizeDelta = new Vector2(220f, 200f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 4f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        AddLabel(panel.transform, "DEBUG: FORCE MAP");

        foreach (GameModeInfo.Entry entry in GameModeInfo.All)
        {
            GameMode mode = entry.mode;
            AddButton(panel.transform, entry.displayName, () => SceneFlow.StartMatch(mode));
        }
    }

    private void AddLabel(Transform parent, string text)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 14f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        go.AddComponent<LayoutElement>().minHeight = 20f;
    }

    private void AddButton(Transform parent, string text, System.Action onClick)
    {
        GameObject go = new GameObject($"Button_{text}");
        go.transform.SetParent(parent, false);

        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        Button button = go.AddComponent<Button>();
        button.onClick.AddListener(() => onClick());
        go.AddComponent<LayoutElement>().minHeight = 28f;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(go.transform, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text.ToUpperInvariant();
        label.fontSize = 13f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }
}
