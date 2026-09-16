using System;
using System.Collections.Generic;
using UnityEngine;

public enum HudSymbol
{
    Disc,
    Ring,
    DotRing,
    SlashRing,
    Plus,
    Ex,
    Chevron,
    DoubleChevron,
    Triangle,
    SquareOutline,
    Hex,
    Burst,
    Concentric,
    Shield,
    ArrowUp,
    Person,
    WeaponSmg,
    WeaponPistol,
    Parallelogram,
    FuelArc
}

public static class HudArt
{
    private const int Resolution = 128;

    private static readonly Dictionary<HudSymbol, Sprite> Cache = new Dictionary<HudSymbol, Sprite>();
    private static Sprite solid;
    private static Sprite disc;

    public static Sprite Solid
    {
        get
        {
            if (solid == null)
            {
                Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "HudArt_Solid" };
                Color32[] pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;
                solid = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
                solid.hideFlags = HideFlags.HideAndDontSave;
            }

            return solid;
        }
    }

    public static Sprite Disc
    {
        get
        {
            if (disc == null)
            {
                disc = Get(HudSymbol.Disc);
            }

            return disc;
        }
    }

    public static Sprite Get(HudSymbol symbol)
    {
        if (Cache.TryGetValue(symbol, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite made = Rasterise(symbol);
        Cache[symbol] = made;
        return made;
    }

    public static HudSymbol ForAbility(Ability ability)
    {
        if (ability == null)
        {
            return HudSymbol.Ring;
        }

        switch (ability.Ai)
        {
            case Ability.AiHint.OffensiveBurst: return HudSymbol.Burst;
            case Ability.AiHint.OffensivePoke: return HudSymbol.Triangle;
            case Ability.AiHint.Control: return HudSymbol.SlashRing;
            case Ability.AiHint.Deployable: return HudSymbol.SquareOutline;
            case Ability.AiHint.GapClose: return HudSymbol.DoubleChevron;
            case Ability.AiHint.Escape: return HudSymbol.Chevron;
            case Ability.AiHint.Nova: return HudSymbol.Concentric;
            case Ability.AiHint.TeamHeal: return HudSymbol.Plus;
            case Ability.AiHint.TeamShield: return HudSymbol.Shield;
            case Ability.AiHint.Revive: return HudSymbol.ArrowUp;
            case Ability.AiHint.SelfMobility: return HudSymbol.ArrowUp;
            case Ability.AiHint.UltOffensive: return HudSymbol.Ex;
            case Ability.AiHint.UltDefensive: return HudSymbol.Hex;
            case Ability.AiHint.UltUtility: return HudSymbol.DotRing;
        }

        return ability.IsUltimate ? HudSymbol.DotRing : HudSymbol.Ring;
    }

    private static float AspectFor(HudSymbol symbol)
    {
        switch (symbol)
        {
            case HudSymbol.WeaponSmg:
                return 0.50f;
            case HudSymbol.WeaponPistol:
                return 0.62f;
            case HudSymbol.FuelArc:
                return 1.75f;
            case HudSymbol.Parallelogram:
                return 0.72f;
            default:
                return 1f;
        }
    }

    private static Sprite Rasterise(HudSymbol symbol)
    {
        Func<float, float, bool> shape = ShapeFor(symbol);

        int width = Resolution;
        int height = Mathf.Max(8, Mathf.RoundToInt(Resolution * AspectFor(symbol)));

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HudArt_" + symbol,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[width * height];
        const int samples = 3;

        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                int hits = 0;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float u = (px + (sx + 0.5f) / samples) / width;
                        float v = (py + (sy + 0.5f) / samples) / height;
                        if (shape(u * 2f - 1f, v * 2f - 1f))
                        {
                            hits++;
                        }
                    }
                }

                byte alpha = (byte)Mathf.RoundToInt(255f * hits / (samples * samples));
                pixels[py * width + px] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Func<float, float, bool> ShapeFor(HudSymbol symbol)
    {
        switch (symbol)
        {
            case HudSymbol.Disc:
                return (x, y) => Len(x, y) <= 0.92f;

            case HudSymbol.Ring:
                return (x, y) => Band(Len(x, y), 0.72f, 0.15f);

            case HudSymbol.DotRing:
                return (x, y) => Band(Len(x, y), 0.72f, 0.14f) || Len(x, y) <= 0.30f;

            case HudSymbol.SlashRing:
                return (x, y) =>
                {
                    float r = Len(x, y);
                    bool ring = Band(r, 0.72f, 0.14f);
                    bool slash = Mathf.Abs(x + y) * 0.70710678f <= 0.10f && r <= 0.80f;
                    return ring || slash;
                };

            case HudSymbol.Plus:
                return (x, y) => (Mathf.Abs(x) <= 0.16f && Mathf.Abs(y) <= 0.86f)
                                 || (Mathf.Abs(y) <= 0.16f && Mathf.Abs(x) <= 0.86f);

            case HudSymbol.Ex:
                return (x, y) =>
                {
                    float u = (x + y) * 0.70710678f;
                    float v = (x - y) * 0.70710678f;
                    return (Mathf.Abs(u) <= 0.15f && Mathf.Abs(v) <= 0.82f)
                           || (Mathf.Abs(v) <= 0.15f && Mathf.Abs(u) <= 0.82f);
                };

            case HudSymbol.Chevron:
                return (x, y) => ChevronAt(x, y, 0f);

            case HudSymbol.DoubleChevron:
                return (x, y) => ChevronAt(x, y, -0.34f) || ChevronAt(x, y, 0.34f);

            case HudSymbol.Triangle:
                return (x, y) => y >= -0.62f && y <= 0.84f && Mathf.Abs(x) <= (0.84f - y) * 0.52f;

            case HudSymbol.SquareOutline:
                return (x, y) =>
                {
                    float m = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                    return m <= 0.84f && m >= 0.62f;
                };

            case HudSymbol.Hex:
                return (x, y) =>
                {
                    float h = HexDistance(x, y);
                    return h <= 0.84f && h >= 0.62f;
                };

            case HudSymbol.Burst:
                return (x, y) =>
                {
                    float r = Len(x, y);
                    if (r <= 0.24f)
                    {
                        return true;
                    }

                    if (r > 0.90f || r < 0.34f)
                    {
                        return false;
                    }

                    float a = Mathf.Atan2(y, x) / (Mathf.PI * 0.25f);
                    float frac = Mathf.Abs(a - Mathf.Round(a));
                    return frac * Mathf.PI * 0.25f * r <= 0.085f;
                };

            case HudSymbol.Concentric:
                return (x, y) =>
                {
                    float r = Len(x, y);
                    return Band(r, 0.84f, 0.12f) || Band(r, 0.46f, 0.12f);
                };

            case HudSymbol.Shield:
                return (x, y) => ShieldAt(x, y, 1f) && !ShieldAt(x, y, 0.70f);

            case HudSymbol.ArrowUp:
                return (x, y) =>
                {
                    bool stem = Mathf.Abs(x) <= 0.17f && y >= -0.86f && y <= 0.30f;
                    bool head = y >= 0.14f && y <= 0.86f && Mathf.Abs(x) <= (0.86f - y) * 0.80f;
                    return stem || head;
                };

            case HudSymbol.Person:
                return (x, y) =>
                {
                    bool head = Len(x, y - 0.46f) <= 0.31f;
                    float bx = x / 0.70f;
                    float by = (y + 0.74f) / 0.86f;
                    bool body = y <= 0.02f && bx * bx + by * by <= 1f;
                    return head || body;
                };

            case HudSymbol.WeaponSmg:
                return (x, y) =>
                    Rect(x, y, -0.95f, 0.26f, 0.06f, 0.64f)
                    || Rect(x, y, 0.26f, 0.98f, 0.22f, 0.50f)
                    || Rect(x, y, -0.44f, -0.12f, -0.88f, 0.10f)
                    || Rect(x, y, -0.94f, -0.62f, -0.58f, 0.10f);

            case HudSymbol.WeaponPistol:
                return (x, y) =>
                    Rect(x, y, -0.80f, 0.94f, 0.30f, 0.88f)
                    || Rect(x, y, -0.80f, 0.34f, 0.04f, 0.32f)

                    || (y >= -0.92f && y <= 0.16f && Mathf.Abs(x + 0.50f - 0.32f * y) <= 0.21f);

            case HudSymbol.Parallelogram:
                return (x, y) =>
                {
                    float u = x - 0.22f * y;
                    float outer = Mathf.Max(Mathf.Abs(u) / 0.90f, Mathf.Abs(y) / 0.86f);
                    return outer <= 1f && outer >= 0.88f;
                };

            case HudSymbol.FuelArc:
                return (x, y) =>
                {

                    float cx = x + 0.55f;
                    float r = Len(cx, y);
                    if (!Band(r, 0.95f, 0.20f))
                    {
                        return false;
                    }

                    return Mathf.Abs(Mathf.Atan2(y, cx)) <= 1.12f;
                };
        }

        return (x, y) => Len(x, y) <= 0.9f;
    }

    private static bool ChevronAt(float x, float y, float offset)
    {
        float px = x - offset;
        if (Mathf.Abs(y) > 0.70f)
        {
            return false;
        }

        float centre = 0.34f - 0.66f * Mathf.Abs(y);
        return Mathf.Abs(px - centre) <= 0.19f;
    }

    private static bool ShieldAt(float x, float y, float scale)
    {
        float sx = x / scale;
        float sy = y / scale;
        if (sy > 0.84f || sy < -0.90f)
        {
            return false;
        }

        float halfWidth = sy >= 0f ? 0.72f : Mathf.Lerp(0.72f, 0f, Mathf.Pow(-sy / 0.90f, 0.85f));
        return Mathf.Abs(sx) <= halfWidth;
    }

    private static bool Rect(float x, float y, float x0, float x1, float y0, float y1)
    {
        return x >= x0 && x <= x1 && y >= y0 && y <= y1;
    }

    private static float HexDistance(float x, float y)
    {

        float ax = Mathf.Abs(x);
        float ay = Mathf.Abs(y);
        return Mathf.Max(ay, ax * 0.86602540f + ay * 0.5f);
    }

    private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

    private static bool Band(float value, float centre, float halfWidth) =>
        Mathf.Abs(value - centre) <= halfWidth;
}
