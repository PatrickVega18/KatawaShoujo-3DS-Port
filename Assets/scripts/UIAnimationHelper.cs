using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public static class UIAnimationHelper
{
    public static IEnumerator Fade(CanvasGroup cg, float start, float end, float duration)
    {
        if (cg == null) yield break;
        
        if (duration <= 0) 
        { 
            cg.alpha = end; 
            yield break; 
        }
        
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        cg.alpha = end;
    }

    public static IEnumerator FadeImage(Image img, float start, float end, float duration)
    {
        if (img == null) yield break;
        
        Color c = img.color;
        
        if (duration <= 0) 
        { 
            c.a = end; 
            img.color = c; 
            yield break; 
        }
        
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(start, end, elapsed / duration);
            img.color = c;
            yield return null;
        }
        
        c.a = end; 
        img.color = c;
    }

    public static IEnumerator Move(RectTransform rt, Vector3 start, Vector3 end, float duration)
    {
        if (rt == null) yield break;
        
        if (duration <= 0) 
        { 
            rt.anchoredPosition = new Vector2(end.x, end.y); 
            yield break; 
        }
        
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector2 pos = Vector2.Lerp(new Vector2(start.x, start.y), new Vector2(end.x, end.y), elapsed / duration);
            rt.anchoredPosition = pos;
            yield return null;
        }
        rt.anchoredPosition = new Vector2(end.x, end.y);
    }

    public static IEnumerator MoveAndFadeImage(RectTransform rt, Image img, Vector3 startPos, Vector3 endPos, float startAlpha, float endAlpha, float duration)
    {
        if (rt == null || img == null) yield break;
        
        Color c = img.color;

        if (duration <= 0) 
        { 
            rt.anchoredPosition = new Vector2(endPos.x, endPos.y); 
            c.a = endAlpha;
            img.color = c;
            yield break; 
        }
        
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Vector2 pos = Vector2.Lerp(new Vector2(startPos.x, startPos.y), new Vector2(endPos.x, endPos.y), t);
            rt.anchoredPosition = pos;
            c.a = Mathf.Lerp(startAlpha, endAlpha, t);
            img.color = c;
            yield return null;
        }
        rt.anchoredPosition = new Vector2(endPos.x, endPos.y);
        c.a = endAlpha;
        img.color = c;
    }
}