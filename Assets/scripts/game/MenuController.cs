using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MenuController : MonoBehaviour
{
    [Header("Controladores Principales")]
    public ControlsGameController controlsGameController;
    public MenuNavegacionGame menuNavigationController;
    public DualScreenTransitionGame dualScreenTransitionController;
    public DecisionController decisionController;

    [Header("Contenedores Principales")]
    public GameObject menuUpperContainer;
    public GameObject menuLowerContainer;
    public CanvasGroup mainContainerCanvasGroup; 
    public GameObject mainMenuScreen;
    public GameObject saveLoadScreen;

    private bool isMenuOpen = false;
    private bool isAnimatingMenu = false; // Para evitar doble pulsación

    public CanvasGroup mainMenuCanvasGroup; // Para Menu-General
    public CanvasGroup saveLoadCanvasGroup; // Para Guardar-Menu
    public float fadeDuration = 0.3f;    // Duración del fundido en segundos

    void Start()
    {
        if (menuUpperContainer != null) menuUpperContainer.SetActive(false);
        if (menuLowerContainer != null) menuLowerContainer.SetActive(false);
        if (menuNavigationController != null) menuNavigationController.enabled = false;
        if (saveLoadScreen != null && saveLoadCanvasGroup != null)
        {
            saveLoadCanvasGroup.alpha = 0f;
            saveLoadScreen.SetActive(true);
        }
    }

    void Update()
    {
        if (isAnimatingMenu) return;
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (!isMenuOpen)
            {
                TriggerOpeningSequence();
            }
            else
            {
                TriggerClosingSequence();
            }
        }
    }

    // ===== Método público para ser llamado por el botón de la UI =====
    public void TriggerOpeningSequence()
    {
        if (isMenuOpen || isAnimatingMenu) return;
        StartCoroutine(OpenMenuCoroutine());
    }
    public void TriggerClosingSequence()
    {
        if (!isMenuOpen || isAnimatingMenu) return;
        StartCoroutine(CloseMenuCoroutine());
    }

    private IEnumerator OpenMenuCoroutine()
    {
        isAnimatingMenu = true;

        if (controlsGameController != null) controlsGameController.enabled = false;
        if (decisionController != null) decisionController.SetInputActive(false);

        if (menuUpperContainer != null) menuUpperContainer.SetActive(true);
        if (menuLowerContainer != null) menuLowerContainer.SetActive(true);

        // Inicia la secuencia de transición...
        if (dualScreenTransitionController != null)
        {
            if (menuNavigationController != null)
            {
                menuNavigationController.enabled = true;
                dualScreenTransitionController.OnTransitionComplete = menuNavigationController.StartMenuDisplay;
            }
            dualScreenTransitionController.BeginOpeningTransition();
        }

        isMenuOpen = true;
        isAnimatingMenu = false;
        yield return null;
    }

    private IEnumerator CloseMenuCoroutine()
    {
        isAnimatingMenu = true;

        // IDENTIFICAR QUÉ PANEL ESTÁ VISIBLE
        CanvasGroup groupToFadeOut = null;
        SaveLoadUIController saveUI = null;

        if (saveLoadScreen != null)
        {
            saveUI = saveLoadScreen.GetComponent<SaveLoadUIController>();
            if (saveUI != null)
            {
                groupToFadeOut = saveUI.GetVisibleConfirmationCanvasGroup();
            }
        }

        if (groupToFadeOut == null && saveLoadCanvasGroup != null && saveLoadCanvasGroup.alpha > 0)
        {
            groupToFadeOut = saveLoadCanvasGroup;
        }

        // INICIAR EL FADE-OUT DE TODO LO VISIBLE
        if (groupToFadeOut != null)
        {
            StartCoroutine(FadeOut(groupToFadeOut, menuNavigationController.menuCloseFadeOutDuration, null));
        }

        // También iniciamos el fade-out del menú de navegación principal.
        if (menuNavigationController != null)
        {
            menuNavigationController.StartMenuHide(null);
        }

        // ESPERAR A QUE LAS ANIMACIONES DE FADE-OUT TERMINEN
        yield return new WaitForSeconds(menuNavigationController.menuCloseFadeOutDuration);

        // AHORA, con todo ya invisible, reseteamos el estado del SaveLoadUIController.
        if (saveUI != null)
        {
            saveUI.ResetStateInstantly();
        }

        // Iniciar la transición de los wipes (esta parte no cambia).
        if (dualScreenTransitionController != null)
        {
            dualScreenTransitionController.OnTransitionComplete = FinalizeMenuClose;
            dualScreenTransitionController.BeginClosingTransition();
        }
        else
        {
            FinalizeMenuClose();
        }
    }

    private void FinalizeMenuClose()
    {
        // Ocultamos los contenedores principales
        if (menuUpperContainer != null) menuUpperContainer.SetActive(false);
        if (menuLowerContainer != null) menuLowerContainer.SetActive(false);

        if (saveLoadScreen != null) saveLoadScreen.SetActive(true);
        if (mainMenuScreen != null) mainMenuScreen.SetActive(true);

        if (decisionController != null && decisionController.IsDecisionActive)
        {
            Debug.Log("Devolviendo control al DecisionController.");
            decisionController.SetInputActive(true);
        }
        else
        {
            // Si NO hay una decisión activa, devolvemos el control al juego normal.
            Debug.Log("Devolviendo control al ControlsGameController.");
            if (controlsGameController != null)
            {
                controlsGameController.enabled = true;
            }
        }

        isMenuOpen = false;
        isAnimatingMenu = false;
    }
    //Me da miedo borrarlo x,D
    private void SetImageAlpha(Image image, float alpha)
    {
        if (image != null) image.color = new Color(image.color.r, image.color.g, image.color.b, alpha);
    }
    private void SetTextAlpha(Text text, float alpha)
    {
        if (text != null) text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);
    }

    public void ShowSaveLoadScreen(bool show, SaveLoadMode mode = SaveLoadMode.Save)
    {
        // Esta corrutina a veces puede interferir, detenerla es una buena práctica.
        StopAllCoroutines(); 

        if (show)
        {
            StartCoroutine(FadeOut(mainMenuCanvasGroup, fadeDuration, () => {
                mainMenuCanvasGroup.interactable = false;
                mainMenuCanvasGroup.blocksRaycasts = false;
            }));

            if (menuNavigationController != null) menuNavigationController.enabled = false;

            SaveLoadUIController saveUI = saveLoadScreen.GetComponent<SaveLoadUIController>();
            if (saveUI != null)
            {
                saveUI.AbrirMenuGuardado(mode);
            }
            StartCoroutine(FadeIn(saveLoadCanvasGroup, fadeDuration, null));
        }
        else 
        {
            StartCoroutine(FadeOut(saveLoadCanvasGroup, fadeDuration, () => {
                saveLoadCanvasGroup.interactable = false;
                saveLoadCanvasGroup.blocksRaycasts = false;
            }));

            StartCoroutine(FadeIn(mainMenuCanvasGroup, fadeDuration, null));
            if (menuNavigationController != null) menuNavigationController.enabled = true;
        }
    }
    private IEnumerator FadeOut(CanvasGroup canvasGroup, float duration, System.Action onComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, time / duration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        if (onComplete != null)
        {
            onComplete();
        }
    }

    private IEnumerator FadeIn(CanvasGroup canvasGroup, float duration, System.Action onComplete)
    {
        // Aseguramos que el objeto esté activo para poder ver el fundido.
        canvasGroup.gameObject.SetActive(true);

        // Empezamos la animación desde un alfa de 0.
        float startAlpha = 0f;
        canvasGroup.alpha = startAlpha;

        float time = 0f;

        // Lo hacemos no-interactuable durante el fundido para evitar clics accidentales.
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        while (time < duration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, time / duration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true; // Se vuelve interactuable al final.

        if (onComplete != null)
        {
            onComplete();
        }
    }
}