using UnityEngine;
using System.Collections.Generic;

public static class CharacterColors
{
    private static Dictionary<string, Color> colorCache;

    private static void InitCache()
    {
        if (colorCache != null) return;
        colorCache = new Dictionary<string, Color>();
        colorCache["cratobeta"] = ParseColor("#4d85fdff");

        colorCache["hisao"] = ParseColor("#629276");
        colorCache["misha"] = ParseColor("#ff809f");
        colorCache["shizune"] = ParseColor("#72adee");
        colorCache["hanako"] = ParseColor("#897cbf");
    }

    private static Color ParseColor(string html)
    {
        Color col;
        if (ColorUtility.TryParseHtmlString(html, out col)) return col;
        return Color.white;
    }

    public static Color GetColor(string name)
    {
        if (string.IsNullOrEmpty(name)) return Color.white;
        InitCache();
        Color c;
        if (colorCache.TryGetValue(name.ToLower().Trim(), out c)) return c;
        return Color.white;
    }
}