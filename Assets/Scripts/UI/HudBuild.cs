using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class HudBuild
{

    public static Canvas Canvas(Transform parent, string name, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(Transform parent, string name, float inset)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    public static Image Block(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = Rect(parent, name, anchor, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = HudArt.Solid;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Image Glyph(Transform parent, string name, HudSymbol symbol, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = Rect(parent, name, anchor, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = HudArt.Get(symbol);
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = Rect(parent, name, anchor, position, size);

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    public static bool HudSuppressed
    {
        get
        {
            MatchManager match = MatchManager.Instance;
            return match != null && match.CurrentPhase == MatchManager.Phase.HeroSelect;
        }
    }

    public static Image AsFill(Image image, Image.FillMethod method, int origin)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = method;
        image.fillOrigin = origin;
        image.fillAmount = 0f;
        return image;
    }
}
