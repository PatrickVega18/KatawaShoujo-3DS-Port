using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class OptionsMenuController : MonoBehaviour
{
    [Header("Controladores Externos")]
    public MenuNavegacion menuNavegacion;
    public DualScreenTransitionMenu transitionController;

    [Header("UI del Menú de Opciones")]
    public CanvasGroup mainCanvasGroup;
    public CanvasGroup[] optionCanvasGroups; // 0: Español, 1: Inglés, 2: Volver

    [Header("Apariencia")]
    [Range(0f, 1f)] public float normalAlpha = 0.5f;
    [Range(0f, 1f)] public float selectedAlpha = 1.0f;
    public float fadeDuration = 0.3f;

    private int currentSelectionIndex = 0;
    private bool navigationEnabled = false;

    void Start()
    {
        // Asegurarse de que el menú esté invisible pero activo al inicio
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.interactable = false;
            mainCanvasGroup.blocksRaycasts = false;
        }
    }

    void Update()
    {
        if (!navigationEnabled) return;

        // --- Navegación ---
        int previousSelection = currentSelectionIndex;
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            currentSelectionIndex = (currentSelectionIndex - 1 + optionCanvasGroups.Length) % optionCanvasGroups.Length;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            currentSelectionIndex = (currentSelectionIndex + 1) % optionCanvasGroups.Length;
        }

        if (previousSelection != currentSelectionIndex)
        {
            UpdateHighlight();
        }

        // --- Confirmación ---
        if (Input.GetKeyDown(KeyCode.A))
        {
            ConfirmSelection(currentSelectionIndex);
        }
    }

    // --- Métodos Públicos de Control ---
    public void AbrirMenu()
    {
        Debug.Log("Transición B completada. Abriendo menú de opciones...");
        StartCoroutine(FadeCanvasGroup(mainCanvasGroup, 1f, fadeDuration, () =>
        {
            navigationEnabled = true;
            currentSelectionIndex = 0; // Empezar siempre en la primera opción
            UpdateHighlight();
        }));
    }

    private void CerrarMenu()
    {
        navigationEnabled = false;
        StartCoroutine(FadeCanvasGroup(mainCanvasGroup, 0f, fadeDuration, () =>
        {
            // Cuando el fade termina, llamamos a la Transición C
            if (transitionController != null)
            {
                transitionController.OnTransitionComplete = DevolverControlAlMenuPrincipal;
                transitionController.BeginCloseSubMenuTransition();
            }
        }));
    }

    private void DevolverControlAlMenuPrincipal()
    {
        Debug.Log("Transición C completada. Devolviendo control a MenuNavegacion.");
        if (menuNavegacion != null)
        {
            menuNavegacion.ReEnableNavigation();
        }
    }

    // --- Lógica Interna ---
    private void ConfirmSelection(int index)
    {
        switch (index)
        {
            case 0: // Español
                // Le decimos al LanguageManager que el nuevo idioma es "ES"
                if (LanguageManager.Instance != null)
                {
                    LanguageManager.Instance.SetLanguage("ES");
                    Debug.Log("Idioma cambiado a Español.");
                }
                CerrarMenu(); 
                break;

            case 1: // Inglés
                // Le decimos al LanguageManager que el nuevo idioma es "EN"
                if (LanguageManager.Instance != null)
                {
                    LanguageManager.Instance.SetLanguage("EN");
                    Debug.Log("Idioma cambiado a Inglés.");
                }
                // Después de cambiar, simplemente volvemos al menú anterior.
                CerrarMenu();
                break;
                
            case 2: // Volver
                CerrarMenu();
                break;
        }
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < optionCanvasGroups.Length; i++)
        {
            float targetAlpha = (i == currentSelectionIndex) ? selectedAlpha : normalAlpha;

            if (optionCanvasGroups[i] != null) optionCanvasGroups[i].alpha = targetAlpha;
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration, System.Action onComplete = null)
    {
        if (cg == null) { if (onComplete != null) onComplete(); yield break; }
        float startAlpha = cg.alpha;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }
        cg.alpha = targetAlpha;
        if (onComplete != null) onComplete();
    }
    
    //Tener cuidado con el CÓDIGO de idioma!
}