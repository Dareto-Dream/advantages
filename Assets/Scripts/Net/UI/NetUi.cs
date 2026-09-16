using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class NetUi
{
    public static readonly Color Panel = new Color(0.05f, 0.06f, 0.08f, 0.92f);
    public static readonly Color Raised = new Color(1f, 1f, 1f, 0.07f);
    public static readonly Color Accent = new Color(0.36f, 0.68f, 1f, 1f);
    public static readonly Color Good = new Color(0.42f, 0.85f, 0.5f, 1f);
    public static readonly Color Warn = new Color(0.95f, 0.72f, 0.32f, 1f);
    public static readonly Color Bad = new Color(0.93f, 0.38f, 0.38f, 1f);
    public static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);

    public static Canvas Canvas(string name, int sortingOrder)
    {
        GameObject host = new GameObject(name);

        Canvas canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        host.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static VerticalLayoutGroup Column(Transform parent, string name, float spacing, int padding)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childAlignment = TextAnchor.UpperLeft;
        return layout;
    }

    public static HorizontalLayoutGroup Row(Transform parent, string name, float spacing, float height)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        go.AddComponent<LayoutElement>().minHeight = height;
        return layout;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;

        go.AddComponent<LayoutElement>().minHeight = size * 1.6f;
        return label;
    }

    public static Button Button(Transform parent, string text, Action onClick, Color? tint = null, float height = 34f)
    {
        GameObject go = new GameObject($"Button_{text}");
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = tint ?? Raised;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.22f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.32f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.04f);
        button.colors = colors;

        go.AddComponent<LayoutElement>().minHeight = height;

        GameObject labelObject = new GameObject("Text");
        labelObject.transform.SetParent(go.transform, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = new Vector2(-6f, 0f);

        return button;
    }

    public static void SetButtonText(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = text;
        }
    }

    public static TMP_InputField Field(Transform parent, string placeholder, float height = 34f)
    {
        GameObject go = new GameObject($"Field_{placeholder}");
        go.transform.SetParent(parent, false);

        Image background = go.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.1f);

        go.AddComponent<LayoutElement>().minHeight = height;

        GameObject viewport = new GameObject("Text Area");
        viewport.transform.SetParent(go.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(8f, 2f);
        viewportRect.offsetMax = new Vector2(-8f, -2f);
        viewport.AddComponent<RectMask2D>();

        TextMeshProUGUI text = NewChildText(viewport.transform, "Text", Color.white);
        TextMeshProUGUI hint = NewChildText(viewport.transform, "Placeholder", Dim);
        hint.text = placeholder;

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.textViewport = viewportRect;
        field.textComponent = text;
        field.placeholder = hint;
        field.targetGraphic = background;
        field.fontAsset = text.font;
        field.pointSize = 16f;

        return field;
    }

    private static TextMeshProUGUI NewChildText(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = 16f;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    public static Color PingColor(float milliseconds)
    {
        if (milliseconds < 0f)
        {
            return Bad;
        }

        if (milliseconds < 60f)
        {
            return Good;
        }

        return milliseconds < 140f ? Warn : Bad;
    }

    public static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
