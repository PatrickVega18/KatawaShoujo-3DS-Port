using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[System.Serializable]
public class MenuOptionElements
{
    public CanvasGroup canvasGroup;
    public Text text;
}

public class MenuNavegacionGame : MonoBehaviour
{
    [Header("Controladores Externos")]
    public MenuController menuController;
    public GameController gameController;
    public EventsController eventsController;

    [Header("Opciones del Menú (Upper)")]
    public CanvasGroup menuBackgroundCanvasGroup;
    public MenuOptionElements[] menuOptions;

    [Header("Elementos del Menú (Lower)")]
    public CanvasGroup escenaTextCanvasGroup;
    public CanvasGroup musicaTextCanvasGroup;

    [Header("Apariencia de Opciones (Upper)")]
    public Color baseTextColor = Color.black;
    [Range(0f, 1f)] public float normalAlpha = 0.4f;
    [Range(0f, 1f)] public float selectedAlpha = 1.0f;
    [Tooltip("Color del texto para opciones no disponibles (Guardar/Cargar).")]
    public Color disabledOptionColor = Color.gray;

    [Header("Tiempos de Fundido (Fade)")]
    public float initialFadeInDuration = 0.5f;
    public float optionsFadeInDuration = 0.4f;
    public float menuCloseFadeOutDuration = 0.3f;
    public float navigationFadeDuration = 0.2f;

    [Header("Transición a Menú Principal")]
    public Image[] mainMenuTransitionImages;
    public float mainMenuFadeDuration = 1.0f;


    private int currentSelectionIndex = 0;
    private bool navigationEnabled = false;
    private Dictionary<Text, Coroutine> activeOptionFades = new Dictionary<Text, Coroutine>();

    void Start()
    {
        if (menuBackgroundCanvasGroup != null) menuBackgroundCanvasGroup.alpha = 0f;
        foreach (var option in menuOptions)
        {
            if (option.canvasGroup != null) option.canvasGroup.alpha = 0f;
        }
        if (escenaTextCanvasGroup != null) escenaTextCanvasGroup.alpha = 0f;
        if (musicaTextCanvasGroup != null) musicaTextCanvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (!navigationEnabled) return;

        UpdateMenuOptionsAvailability();

        int previousSelection = currentSelectionIndex;
        bool selectionChanged = false;

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            currentSelectionIndex = (currentSelectionIndex - 1 + menuOptions.Length) % menuOptions.Length;
            selectionChanged = true;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            currentSelectionIndex = (currentSelectionIndex + 1) % menuOptions.Length;
            selectionChanged = true;
        }

        if (selectionChanged)
        {
            UpdateHighlight();
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            ConfirmSelection(currentSelectionIndex);
        }
    }
    private void UpdateHighlight()
    {
        for (int i = 0; i < menuOptions.Length; i++)
        {
            if (menuOptions[i].canvasGroup != null)
            {
                float targetAlpha = (i == currentSelectionIndex) ? selectedAlpha : normalAlpha;
                // Usamos la corrutina que ya tenemos
                StartCoroutine(FadeCanvasGroup(menuOptions[i].canvasGroup, targetAlpha, navigationFadeDuration));
            }
        }
    }
    // Método público que inicia la secuencia de aparición
    public void StartMenuDisplay()
    {
        StartCoroutine(ShowMenuUICoroutine());
    }

    private IEnumerator ShowMenuUICoroutine()
    {
        if (menuBackgroundCanvasGroup != null)
        {
            StartCoroutine(FadeCanvasGroup(menuBackgroundCanvasGroup, 1f, initialFadeInDuration));
        }
        if (escenaTextCanvasGroup != null)
        {
            StartCoroutine(FadeCanvasGroup(escenaTextCanvasGroup, 1f, initialFadeInDuration));
        }
        if (musicaTextCanvasGroup != null)
        {
            StartCoroutine(FadeCanvasGroup(musicaTextCanvasGroup, 1f, initialFadeInDuration));
        }

        yield return new WaitForSeconds(initialFadeInDuration);

        for (int i = 0; i < menuOptions.Length; i++)
        {
            if (menuOptions[i].canvasGroup != null)
            {
                float targetAlpha = (i == currentSelectionIndex) ? selectedAlpha : normalAlpha;
                StartCoroutine(FadeCanvasGroup(menuOptions[i].canvasGroup, targetAlpha, optionsFadeInDuration));
            }
        }
        yield return new WaitForSeconds(optionsFadeInDuration);

        navigationEnabled = true;
    }

    // Método público que inicia la secuencia de ocultación
    public void StartMenuHide(System.Action onHideComplete)
    {
        StartCoroutine(HideMenuUICoroutine(onHideComplete));
    }

    private IEnumerator HideMenuUICoroutine(System.Action onHideComplete)
    {
        navigationEnabled = false;

        // Usa la duración de cierre del menú
        foreach (var option in menuOptions)
        {
            if (option.canvasGroup != null)
            {
                StartCoroutine(FadeCanvasGroup(option.canvasGroup, 0f, menuCloseFadeOutDuration));
            }
        }
        yield return new WaitForSeconds(menuCloseFadeOutDuration);

        // Usa la duración de cierre del menú
        if (menuBackgroundCanvasGroup != null) StartCoroutine(FadeCanvasGroup(menuBackgroundCanvasGroup, 0f, menuCloseFadeOutDuration));
        if (escenaTextCanvasGroup != null) StartCoroutine(FadeCanvasGroup(escenaTextCanvasGroup, 0f, menuCloseFadeOutDuration));
        if (musicaTextCanvasGroup != null) StartCoroutine(FadeCanvasGroup(musicaTextCanvasGroup, 0f, menuCloseFadeOutDuration));
        yield return new WaitForSeconds(menuCloseFadeOutDuration);

        if (onHideComplete != null)
        {
            onHideComplete();
        }
    }
    // ===== Reestructuramos el método para eliminar el error y la redundancia =====
    private void ConfirmSelection(int index)
    {
        if (!navigationEnabled) return;

        if (index == 2 || index == 3)
        {
            if (!CanSaveOrLoad())
            {
                Debug.Log("Acción no permitida en este momento (Guardar/Cargar).");
                return;
            }
        }

        Debug.Log("Opción seleccionada: " + menuOptions[index].text.text);

        switch (index)
        {
            case 0: // Regresar
                if (menuController != null)
                {
                    menuController.TriggerClosingSequence();
                }
                break;

            case 1: // Opciones
                // Esta opción no debe hacer nada por ahora.
                Debug.Log("Opción 'Opciones' seleccionada, no se realiza ninguna acción.");
                return;

            case 2:
                if (menuController != null)
                {
                    menuController.ShowSaveLoadScreen(true, SaveLoadMode.Save);
                }
                break;

            case 3: // Cargar
                if (menuController != null)
                {
                    menuController.ShowSaveLoadScreen(true, SaveLoadMode.Load);
                }
                break;

            case 4: // Menú Principal
                navigationEnabled = false;
                StartCoroutine(GoToMainMenuCoroutine());
                break;
        }
    }
    // ===== Método que comprueba si se puede guardar o cargar =====
    private bool CanSaveOrLoad()
    {
        if (gameController == null || eventsController == null || eventsController.bigDialogueController == null)
        {
            return false;
        }

        // El TipoNodo actual no debe ser "Evento"
        string tipoNodoActual = gameController.currentGuionNode.ContainsKey("TipoNodo") ? gameController.currentGuionNode["TipoNodo"] : "Dialogo";
        if (tipoNodoActual.ToLowerInvariant() == "evento")
        {
            string eventoEspecifico = eventsController.GetCurrentEventTypeFromEventsCSV();
            if (eventoEspecifico != "sfx" && eventoEspecifico != "sfxloop" && eventoEspecifico != "sfxout")
            {
                return false;
            }
        }

        // El modo BigBoxSay no debe estar activo
        if (eventsController.bigDialogueController.IsBigBoxModeActive)
        {
            return false;
        }

        // El modo SliderText no debe estar activo
        if (eventsController.IsSliderTextActive)
        {
            return false;
        }

        // Si ninguna condición de bloqueo se cumple, se puede guardar/cargar
        return true;
    }
    private void UpdateMenuOptionsAvailability()
    {
        bool canSave = CanSaveOrLoad();
        for (int i = 0; i < menuOptions.Length; i++)
        {
            var optionText = menuOptions[i].text; // Accedemos al componente Text de nuestra estructura
            if (optionText == null) continue;

            if (i == 2 || i == 3) // Opciones de Guardar y Cargar
            {
                // Solo cambiamos el color del texto, no su alfa.
                optionText.color = canSave ? baseTextColor : disabledOptionColor;
            }
            else
            {
                // El resto de opciones siempre usan el color base.
                optionText.color = baseTextColor;
            }
        }
    }

    // --- Helpers de Fades y Alfas ---
    private void SetImageAlpha(Image image, float alpha) { if (image != null) image.color = new Color(image.color.r, image.color.g, image.color.b, alpha); }
    private void SetAlphaOnly(Text text, float alpha) { if (text != null) text.color = new Color(text.color.r, text.color.g, text.color.b, alpha); }
    private void SetOptionAlpha(Text text, float alpha) { if (text != null) text.color = new Color(baseTextColor.r, baseTextColor.g, baseTextColor.b, alpha); }

    private IEnumerator FadeImage(Image image, float targetAlpha, float duration)
    {
        if (image == null) yield break;
        float startAlpha = image.color.a;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            SetImageAlpha(image, Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration));
            yield return null;
        }
        SetImageAlpha(image, targetAlpha);
    }

    private IEnumerator FadeLowerTextCoroutine(Text targetText, float targetAlpha, float duration)
    {
        if (targetText == null) yield break;
        float startAlpha = targetText.color.a;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            SetAlphaOnly(targetText, Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration));
            yield return null;
        }
        SetAlphaOnly(targetText, targetAlpha);
    }
    // ===== Corrutina para manejar el fade y el cambio de escena =====
    private IEnumerator GoToMainMenuCoroutine()
    {
        Debug.Log("Iniciando transición a la escena del menú principal...");

        // Iniciamos el fadeIn para todas las imágenes de transición a la vez
        if (mainMenuTransitionImages != null && mainMenuTransitionImages.Length > 0)
        {
            foreach (var img in mainMenuTransitionImages)
            {
                if (img != null)
                {
                    img.gameObject.SetActive(true);
                    SetImageAlpha(img, 0f);
                    StartCoroutine(FadeImage(img, 1f, mainMenuFadeDuration));
                }
            }
        }

        // Esperamos a que el fundido termine
        yield return new WaitForSeconds(mainMenuFadeDuration);

        Debug.Log("Fundido completo. Cargando escena 'menu'...");

        // Cargamos la escena del menú
        SceneManager.LoadScene("menu");
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        if (cg == null) yield break;
        float startAlpha = cg.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }
        cg.alpha = targetAlpha;
    }
}