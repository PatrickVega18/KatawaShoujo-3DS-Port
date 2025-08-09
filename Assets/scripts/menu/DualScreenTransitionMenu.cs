using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DualScreenTransitionMenu : MonoBehaviour
{
    [Header("Referencias UI")]
    public RectTransform lowerWipeRect;
    public Image lowerWipeImage;
    public RectTransform upperWipeRect;
    public Image upperWipeImage;

    [Header("Tiempos de Animación")]
    public float wipeDuration = 0.4f;
    [Tooltip("Delay final ANTES de ejecutar la acción final.")]
    public float delayBeforeAction = 0.1f;

    // --- Posiciones Fijas ---
    private readonly Vector2 upperWipe_VisiblePos = new Vector2(-200f, -120f);
    private readonly Vector2 lowerWipe_VisiblePos = new Vector2(-160f, -120f);

    private readonly Vector2 upperWipe_SubMenu_VisiblePos = new Vector2(-200f, 120f);
    private readonly Vector2 lowerWipe_SubMenu_VisiblePos = new Vector2(-160f, 120f);

    private readonly Vector2 upperWipe_HiddenTopPos = new Vector2(-200f, 420f);
    private readonly Vector2 lowerWipe_HiddenTopPos = new Vector2(-160f, 420f);

    private readonly Vector2 upperWipe_HiddenBottomPos = new Vector2(-200f, -420f);
    private readonly Vector2 lowerWipe_HiddenBottomPos = new Vector2(-160f, -420f);
    
    public System.Action OnTransitionComplete;
    private bool isTransitioning = false;
    private Coroutine activeSequenceCoroutine = null;

    void Start() 
    {
        if (upperWipeRect == null || lowerWipeRect == null || upperWipeImage == null || lowerWipeImage == null)
        {
            Debug.LogError("DualScreenTransitionMenu: Faltan referencias a los RectTransforms o Images de los wipes.", gameObject);
            this.enabled = false;
            return;
        }
        upperWipeRect.gameObject.SetActive(false);
        lowerWipeRect.gameObject.SetActive(false);
    }

    // Llama a la animación para empezar el juego.
    public void BeginStartGameTransition()
    {
        if (isTransitioning) return;
        Debug.Log("Iniciando Transición A (Inicio de Juego)...");
        if (activeSequenceCoroutine != null) StopCoroutine(activeSequenceCoroutine);
        activeSequenceCoroutine = StartCoroutine(RunStartGameSequence());
    }

    // METODO A La secuencia de 'empezar juego': barras subiendo desde abajo.
    private IEnumerator RunStartGameSequence()
    {
        isTransitioning = true;

        SetWipesAlpha(1.0f); // Totalmente opaco
        upperWipeRect.localScale = new Vector3(1, 1, 1); // Escala Y normal (por algún motivo es importante . _.)
        lowerWipeRect.localScale = new Vector3(1, 1, 1);
        upperWipeRect.anchoredPosition = upperWipe_HiddenBottomPos; // Posicionar abajo
        lowerWipeRect.anchoredPosition = lowerWipe_HiddenBottomPos;
        upperWipeRect.gameObject.SetActive(true);
        lowerWipeRect.gameObject.SetActive(true);

        yield return StartCoroutine(AnimateWipe(lowerWipeRect, lowerWipe_HiddenBottomPos, lowerWipe_VisiblePos, wipeDuration));
        yield return StartCoroutine(AnimateWipe(upperWipeRect, upperWipe_HiddenBottomPos, upperWipe_VisiblePos, wipeDuration));

        if (delayBeforeAction > 0) yield return new WaitForSeconds(delayBeforeAction);
        isTransitioning = false;
        if (OnTransitionComplete != null) OnTransitionComplete();
    }
    
    // METODO B: Llama a la animación para abrir un submenú.
    public void BeginOpenSubMenuTransition()
    {
        if (isTransitioning) return;
        Debug.Log("Iniciando Transición B (Abrir Submenú)...");
        if (activeSequenceCoroutine != null) StopCoroutine(activeSequenceCoroutine);
        activeSequenceCoroutine = StartCoroutine(RunOpenSubMenuSequence());
    }

    private IEnumerator RunOpenSubMenuSequence()
    {
        isTransitioning = true;
        
        SetWipesAlpha(0.5f); // Semi-transparente
        upperWipeRect.localScale = new Vector3(1, -1, 1); // Escala Y invertida
        lowerWipeRect.localScale = new Vector3(1, -1, 1);
        upperWipeRect.anchoredPosition = upperWipe_HiddenTopPos; // Posicionar arriba
        lowerWipeRect.anchoredPosition = lowerWipe_HiddenTopPos;
        upperWipeRect.gameObject.SetActive(true);
        lowerWipeRect.gameObject.SetActive(true);

        yield return StartCoroutine(AnimateWipe(upperWipeRect, upperWipe_HiddenTopPos, upperWipe_SubMenu_VisiblePos, wipeDuration));
        yield return StartCoroutine(AnimateWipe(lowerWipeRect, lowerWipe_HiddenTopPos, lowerWipe_SubMenu_VisiblePos, wipeDuration));

        if (delayBeforeAction > 0) yield return new WaitForSeconds(delayBeforeAction);
        isTransitioning = false;
        if (OnTransitionComplete != null) OnTransitionComplete();
    }

    // METODO C: es la B pero alrevez XD
    public void BeginCloseSubMenuTransition()
    {
        if (isTransitioning) return;
        Debug.Log("Iniciando Transición C (Cerrar Submenú)...");
        if (activeSequenceCoroutine != null) StopCoroutine(activeSequenceCoroutine);
        activeSequenceCoroutine = StartCoroutine(RunCloseSubMenuSequence());
    }

    private IEnumerator RunCloseSubMenuSequence()
    {
        isTransitioning = true;
        
        SetWipesAlpha(0.5f); // Semi-transparente
        upperWipeRect.localScale = new Vector3(1, -1, 1); // Escala Y invertida
        lowerWipeRect.localScale = new Vector3(1, -1, 1);
        upperWipeRect.gameObject.SetActive(true);
        lowerWipeRect.gameObject.SetActive(true);

        yield return StartCoroutine(AnimateWipe(lowerWipeRect, lowerWipeRect.anchoredPosition, lowerWipe_HiddenTopPos, wipeDuration));
        yield return StartCoroutine(AnimateWipe(upperWipeRect, upperWipeRect.anchoredPosition, upperWipe_HiddenTopPos, wipeDuration));

        upperWipeRect.gameObject.SetActive(false);
        lowerWipeRect.gameObject.SetActive(false);

        if (delayBeforeAction > 0) yield return new WaitForSeconds(delayBeforeAction);
        isTransitioning = false;
        if (OnTransitionComplete != null) OnTransitionComplete();
    }
    
    private IEnumerator AnimateWipe(RectTransform rt, Vector2 startPos, Vector2 endPos, float duration) 
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

    // --- Helper para cambiar el Alfa de las imágenes ---
    private void SetWipesAlpha(float alpha)
    {
        if (upperWipeImage != null)
        {
            Color c = upperWipeImage.color;
            c.a = alpha;
            upperWipeImage.color = c;
        }
        if (lowerWipeImage != null)
        {
            Color c = lowerWipeImage.color;
            c.a = alpha;
            lowerWipeImage.color = c;
        }
    }
}