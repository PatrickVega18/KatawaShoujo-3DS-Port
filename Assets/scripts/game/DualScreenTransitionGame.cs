using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DualScreenTransitionGame : MonoBehaviour
{
    [Header("Referencias - Cuadros Negros (Wipes)")]
    public RectTransform lowerWipeRect;
    public RectTransform upperWipeRect;

    [Header("Tiempos de Animación")]
    public float wipeDuration = 0.3f;
    [Tooltip("Delay final ANTES de ejecutar la acción final.")]
    public float delayBeforeAction = 0.1f;

    private Vector2 wipeStartPos = new Vector2(0f, 420f);
    private Vector2 upperWipeEndPos = new Vector2(-200f, 120f);
    private Vector2 lowerWipeEndPos = new Vector2(-160f, 120f);

    public System.Action OnTransitionComplete;
    
    private bool isTransitioning = false;
    private Coroutine activeSequenceCoroutine = null;

    void Start() 
    {
        if (upperWipeRect != null)
        {
            upperWipeRect.localScale = new Vector3(1, -1, 1);
            upperWipeRect.gameObject.SetActive(false);
        }
        if (lowerWipeRect != null)
        {
            lowerWipeRect.localScale = new Vector3(1, -1, 1);
            lowerWipeRect.gameObject.SetActive(false);
        }
    }

    public void BeginOpeningTransition() 
    {
        if (isTransitioning) return;
        Debug.Log("Iniciando transición de APERTURA...");
        isTransitioning = true;
        if (activeSequenceCoroutine != null) StopCoroutine(activeSequenceCoroutine);
        activeSequenceCoroutine = StartCoroutine(RunOpeningSequence());
    }

    private IEnumerator RunOpeningSequence() 
    {
        upperWipeRect.anchoredPosition = new Vector2(upperWipeEndPos.x, wipeStartPos.y);
        lowerWipeRect.anchoredPosition = new Vector2(lowerWipeEndPos.x, wipeStartPos.y);
        upperWipeRect.gameObject.SetActive(true);
        lowerWipeRect.gameObject.SetActive(true);

        // 1. El Wipe Superior baja
        yield return StartCoroutine(AnimateWipe(upperWipeRect, upperWipeRect.anchoredPosition, upperWipeEndPos, wipeDuration));
        
        // 2. El Wipe Inferior baja
        yield return StartCoroutine(AnimateWipe(lowerWipeRect, lowerWipeRect.anchoredPosition, lowerWipeEndPos, wipeDuration));

        if (delayBeforeAction > 0) yield return new WaitForSeconds(delayBeforeAction);
        
        isTransitioning = false;
        if (OnTransitionComplete != null) OnTransitionComplete();
    }

    public void BeginClosingTransition()
    {
        if (isTransitioning) return;
        Debug.Log("Iniciando transición de CIERRE...");
        isTransitioning = true;
        if (activeSequenceCoroutine != null) StopCoroutine(activeSequenceCoroutine);
        activeSequenceCoroutine = StartCoroutine(RunClosingSequence());
    }

    private IEnumerator RunClosingSequence()
    {
        Vector2 upperTargetPos = new Vector2(upperWipeEndPos.x, wipeStartPos.y);
        Vector2 lowerTargetPos = new Vector2(lowerWipeEndPos.x, wipeStartPos.y);

        // 1. El Wipe Inferior sube
        yield return StartCoroutine(AnimateWipe(lowerWipeRect, lowerWipeRect.anchoredPosition, lowerTargetPos, wipeDuration));

        // 2. El Wipe Superior sube
        yield return StartCoroutine(AnimateWipe(upperWipeRect, upperWipeRect.anchoredPosition, upperTargetPos, wipeDuration));
        
        // Ocultamos los wipes al final
        upperWipeRect.gameObject.SetActive(false);
        lowerWipeRect.gameObject.SetActive(false);

        if (delayBeforeAction > 0) yield return new WaitForSeconds(delayBeforeAction);

        isTransitioning = false;
        if (OnTransitionComplete != null) OnTransitionComplete();
    }

    // Corrutina genérica para animar los wipes
    IEnumerator AnimateWipe(RectTransform rt, Vector2 startPos, Vector2 endPos, float duration) 
    {
        if (duration <= 0f) { rt.anchoredPosition = endPos; yield break; }
        float elapsedTime = 0f;
        while (elapsedTime < duration) {
            elapsedTime += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, elapsedTime / duration);
            yield return null;
        }
        rt.anchoredPosition = endPos;
    }
}