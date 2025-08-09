using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class DualScreenTransition : MonoBehaviour
{
    [Header("Referencias - Barras de Transición")]
    public List<ImageAlphaFader> lowerBars;
    public List<ImageAlphaFader> upperBars;
    [Header("Referencias - Cuadros Negros (Wipes)")]
    public RectTransform lowerWipeRect;
    public RectTransform upperWipeRect;
    [Header("Tiempos de Animación - Barras")]
    public float barFadeDuration = 5.0f;
    public float barActivationInterval = 2.0f;
    public float timeBetweenScreensInterval = 1.0f;
    [Header("Tiempos de Animación - Wipes")]
    public float lowerWipeDuration = 5.0f;
    public float upperWipeDuration = 5.0f;
    public float upperWipeStartDelay = 1.0f;

    [Tooltip("Delay final ANTES de cargar la siguiente escena (después de que todo termine).")]
    public float delayBeforeSceneLoad = 0.5f;

    // --- Variables Privadas ---
    private bool isTransitioning = false;
    private Vector2 wipeTargetPos = new Vector2(0, 0); // Y=0
    private Vector2 lowerWipeStartPos = new Vector2(0, -290f);
    private Vector2 upperWipeStartPos = new Vector2(0, -270f);
    public System.Action OnTransitionComplete;
    private Coroutine activeSequenceCoroutine = null;

    void Start()
    {
        if (lowerWipeRect != null)
        {
            lowerWipeStartPos = new Vector2(lowerWipeRect.anchoredPosition.x, -290f); // Actualizar X por si acaso
            lowerWipeRect.anchoredPosition = lowerWipeStartPos;
            lowerWipeRect.gameObject.SetActive(false);
        }
        else { Debug.LogError("Referencia a lowerWipeRect no asignada."); }

        if (upperWipeRect != null)
        {
            upperWipeStartPos = new Vector2(upperWipeRect.anchoredPosition.x, -270f); // Actualizar X por si acaso
            upperWipeRect.anchoredPosition = upperWipeStartPos;
            upperWipeRect.gameObject.SetActive(false);
        }
        else { Debug.LogError("Referencia a upperWipeRect no asignada."); }
    }

    // --- Iniciar Transición ---
    public void BeginTransition()
    {
        if (isTransitioning) return;
        if (lowerBars == null || upperBars == null || lowerWipeRect == null || upperWipeRect == null || lowerBars.Count == 0 || upperBars.Count == 0) { Debug.LogError("DualScreenTransition: Faltan referencias."); return; }

        Debug.Log("Iniciando DualScreenTransition...");
        isTransitioning = true;
        InitializeBarAlphas();
        if (activeSequenceCoroutine != null) StopCoroutine(activeSequenceCoroutine);
        activeSequenceCoroutine = StartCoroutine(RunFullSequence());
    }

    void InitializeBarAlphas()
    {
        foreach (var fader in lowerBars) { if (fader) fader.GetComponent<Image>().color = new Color(fader.visibleColor.r, fader.visibleColor.g, fader.visibleColor.b, 0f); }
        foreach (var fader in upperBars) { if (fader) fader.GetComponent<Image>().color = new Color(fader.visibleColor.r, fader.visibleColor.g, fader.visibleColor.b, 0f); }
    }


    IEnumerator RunFullSequence()
    {
        Coroutine lowerWipeCoroutine = null;
        Coroutine barsCoroutine = null; // Una sola corutina para todas las barras
        Coroutine upperWipeCoroutine = null;

        // --- Arranque Inicial ---
        if (lowerWipeRect != null)
        {
            lowerWipeRect.gameObject.SetActive(true);
            lowerWipeCoroutine = StartCoroutine(AnimateWipe(lowerWipeRect, lowerWipeStartPos, wipeTargetPos, lowerWipeDuration));
        }
        barsCoroutine = StartCoroutine(FullBarsSequence());

        if (lowerWipeCoroutine != null)
        {
            yield return lowerWipeCoroutine;
            Debug.Log("Wipe inferior terminado.");
        }

        if (upperWipeStartDelay > 0)
        {
            yield return new WaitForSeconds(upperWipeStartDelay);
        }

        if (upperWipeRect != null)
        {
            upperWipeRect.gameObject.SetActive(true);
            upperWipeCoroutine = StartCoroutine(AnimateWipe(upperWipeRect, upperWipeStartPos, wipeTargetPos, upperWipeDuration));
            Debug.Log("Wipe superior iniciado.");
        }

        Debug.Log("Esperando finalización de secuencia de barras y wipe superior...");
        if (barsCoroutine != null) yield return barsCoroutine;
        if (upperWipeCoroutine != null) yield return upperWipeCoroutine;
        Debug.Log("Todas las animaciones principales relevantes han terminado.");

        // --- Delay Final ---
        if (delayBeforeSceneLoad > 0)
        {
            Debug.Log("Esperando delay final: " + delayBeforeSceneLoad + "s");
            yield return new WaitForSeconds(delayBeforeSceneLoad);
        }

        // --- Transición Completa ---
        Debug.Log("Secuencia DualScreenTransition COMPLETA.");
        isTransitioning = false;
        activeSequenceCoroutine = null;

        // Llamar al Callback para cargar escena
        if (OnTransitionComplete != null) { OnTransitionComplete(); }
    }

    IEnumerator FullBarsSequence()
    {
        Debug.Log("Iniciando secuencia barras inferiores...");
        for (int i = 0; i < lowerBars.Count; i++)
        {
            if (lowerBars[i] != null) { lowerBars[i].fadeDuration = this.barFadeDuration; lowerBars[i].DoFadeIn(); }
            if (i < lowerBars.Count - 1) yield return new WaitForSeconds(barActivationInterval);
        }
        Debug.Log("Fin secuencia inicio fades barras inferiores.");

        if (timeBetweenScreensInterval > 0)
        {
            yield return new WaitForSeconds(timeBetweenScreensInterval);
            Debug.Log("Intervalo entre pantallas esperado.");
        }

        Debug.Log("Iniciando secuencia barras superiores...");
        for (int i = 0; i < upperBars.Count; i++)
        {
            if (upperBars[i] != null) { upperBars[i].fadeDuration = this.barFadeDuration; upperBars[i].DoFadeIn(); }
            if (i < upperBars.Count - 1) yield return new WaitForSeconds(barActivationInterval);
        }
        Debug.Log("Fin secuencia inicio fades barras superiores.");
    }

    // --- Corutina de Animación para Wipes ---
    IEnumerator AnimateWipe(RectTransform rt, Vector2 startPos, Vector2 endPos, float duration)
    {
        if (duration <= 0f) { rt.anchoredPosition = endPos; yield break; }
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float newY = Mathf.Lerp(startPos.y, endPos.y, elapsedTime / duration);
            rt.anchoredPosition = new Vector2(startPos.x, newY);
            yield return null;
        }
        rt.anchoredPosition = endPos;
    }
    //La firme, algunas partes las hizo gemini, porque no entendía algunas funciones xd
}