using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class ImageAlphaFaderGame : MonoBehaviour
{
    [Tooltip("Duración del fundido en segundos (ej: 5.0).")]
    public float fadeDuration = 5.0f;
    [Tooltip("Color que tendrá la imagen al ser opaca.")]
    public Color visibleColor = Color.black;

    private Image targetImage;
    private Color baseColorOpaque;
    private Coroutine activeFadeCoroutine = null;

    void Awake()
    {
        targetImage = GetComponent<Image>();
        if (targetImage == null) { this.enabled = false; return; }
        baseColorOpaque = visibleColor;
        baseColorOpaque.a = 1.0f;
    }

    public Coroutine DoFadeIn()
    {
        if (targetImage == null) return null;
        targetImage.color = new Color(baseColorOpaque.r, baseColorOpaque.g, baseColorOpaque.b, 0f);
        targetImage.enabled = true;
        return StartFadeTo(1.0f, fadeDuration);
    }

    public Coroutine DoFadeOut()
    {
        return StartFadeTo(0.0f, fadeDuration);
    }

    private Coroutine StartFadeTo(float targetAlpha, float duration)
    {
        if (targetImage == null) return null;
        targetImage.enabled = true;
        if (activeFadeCoroutine != null) StopCoroutine(activeFadeCoroutine);
        activeFadeCoroutine = StartCoroutine(FadeAlphaCoroutine(targetAlpha, duration));
        return activeFadeCoroutine;
    }

    IEnumerator FadeAlphaCoroutine(float targetAlpha, float duration)
    {
        if (duration <= 0f) { SetAlpha(targetAlpha); activeFadeCoroutine = null; yield break; }
        float startAlpha = targetImage.color.a; float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            SetAlpha(currentAlpha); yield return null;
        }
        SetAlpha(targetAlpha); activeFadeCoroutine = null;
    }

    void SetAlpha(float alpha)
    {
        if (targetImage != null)
        {
            targetImage.color = new Color(baseColorOpaque.r, baseColorOpaque.g, baseColorOpaque.b, Mathf.Clamp01(alpha));
        }
    }
    
    //Que dejes de revisar los scripts oe XD
}