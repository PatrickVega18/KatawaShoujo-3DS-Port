using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MenuNavegacion : MonoBehaviour
{
    // --- Variables Públicas ---
    [Header("Opciones del Menú")]
    public Text[] menuOptions;

    [Header("Apariencia (Alfa)")]
    public Color baseColor = Color.black;
    [Range(0f, 1f)] public float normalAlpha = 100f / 255f;
    [Range(0f, 1f)] public float selectedAlpha = 1.0f;
    public float fadeDuration = 0.3f;

    [Header("Controladores Externos")]
    public DualScreenTransitionMenu transitionController;
    public Controls generalControls;
    public BackgroundMusicPlayer bgmPlayer;
    public MainMenu_LoadController loadMenuController;

    [Header("Cambio de Escena")]
    public string sceneToLoad = "intro_game";
    public string sceneToLoad2 = "acto_01_game";

    [Header("Opciones")]
    public OptionsMenuController optionsMenuController;

    // --- Variables Privadas ---
    private int currentSelectionIndex = 0;
    private Dictionary<Text, Coroutine> activeFadeCoroutines = new Dictionary<Text, Coroutine>();
    private bool navigationBlocked = false;

    void Start()
    {
        if (menuOptions == null || menuOptions.Length == 0)
        {
            Debug.LogError("MenuNavegacion: No se asignaron opciones de menú.", gameObject);
            this.enabled = false;
            return;
        }
        InitializeOptionAlphas();
    }

    void Update()
    {
        if (navigationBlocked || !this.enabled) return;

        int previousSelectionIndex = currentSelectionIndex;
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
            StartAlphaFades(previousSelectionIndex, currentSelectionIndex);
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            ConfirmSelection(currentSelectionIndex);
        }
    }


    public void OnSubMenuClosed()
    {
        Debug.Log("Submenú cerrado. Devolviendo control a MenuNavegacion.");

        // Llamamos a la transición de cierre de los wipes.
        if (transitionController != null)
        {
            // Cuando la transición de cierre termine, reactivamos la navegación.
            transitionController.OnTransitionComplete = () =>
            {
                navigationBlocked = false;
                this.enabled = true;
            };
            transitionController.BeginCloseSubMenuTransition();
        }
    }

    void ConfirmSelection(int index)
    {
        if (navigationBlocked || !IsValidIndex(index)) return;

        if (index == 0) // "Iniciar"
        {
            if (transitionController != null)
            {
                navigationBlocked = true;
                this.enabled = false;
                if (generalControls != null) generalControls.enabled = false;
                if (bgmPlayer != null) bgmPlayer.StartFadeOut();

                if (index == 0) transitionController.OnTransitionComplete = LoadGameScene;
                else transitionController.OnTransitionComplete = LoadGameScene2;

                transitionController.BeginStartGameTransition();
            }
        }
        else if (index == 1) // "Cargar"
        {
            if (transitionController != null && loadMenuController != null)
            {
                navigationBlocked = true; // Bloqueamos la navegación del menú principal
                this.enabled = false;

                transitionController.OnTransitionComplete = loadMenuController.AbrirMenuCarga;
                transitionController.BeginOpenSubMenuTransition();
            }
            else
            {
                Debug.LogError("¡Falta la referencia a transitionController o loadMenuController!");
            }
        }
        else if (index == 2) // "Extras""
        {
            Debug.Log("Hola owo");
        }
        else if (index == 3) // "Opciones"
        {
            if (transitionController != null && optionsMenuController != null)
            {
                navigationBlocked = true; // Bloqueamos la navegación del menú principal
                this.enabled = false;
                transitionController.OnTransitionComplete = optionsMenuController.AbrirMenu;
                transitionController.BeginOpenSubMenuTransition();
            }
            else
            {
                Debug.LogError("¡Falta la referencia a transitionController o optionsMenuController!");
            }
        }
    }

    // --- Métodos de Gestión de Resaltado (Fade Alfa Texto) ---
    void InitializeOptionAlphas()
    {
        for (int i = 0; i < menuOptions.Length; i++)
        {
            if (menuOptions[i] != null)
            {
                float alpha = (i == currentSelectionIndex) ? selectedAlpha : normalAlpha;
                menuOptions[i].color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }
        }
    }

    void StartAlphaFades(int previousIndex, int newIndex)
    {
        if (IsValidIndex(previousIndex) && menuOptions[previousIndex] != null)
            StartIndividualFade(menuOptions[previousIndex], normalAlpha);
        if (IsValidIndex(newIndex) && menuOptions[newIndex] != null)
            StartIndividualFade(menuOptions[newIndex], selectedAlpha);
    }

    bool IsValidIndex(int index) { return index >= 0 && index < menuOptions.Length; }

    void StartIndividualFade(Text targetText, float targetAlpha)
    {
        if (activeFadeCoroutines.ContainsKey(targetText))
        {
            if (activeFadeCoroutines[targetText] != null) StopCoroutine(activeFadeCoroutines[targetText]);
            activeFadeCoroutines.Remove(targetText);
        }
        Coroutine c = StartCoroutine(FadeAlphaCoroutine(targetText, targetAlpha, fadeDuration));
        activeFadeCoroutines.Add(targetText, c);
    }

    IEnumerator FadeAlphaCoroutine(Text targetText, float targetAlpha, float duration)
    {
        if (duration <= 0.01f)
        {
            targetText.color = new Color(baseColor.r, baseColor.g, baseColor.b, targetAlpha); CleanUpCoroutineReference(targetText);
            yield break;
        }
        float startAlpha = targetText.color.a; float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            targetText.color = new Color(baseColor.r, baseColor.g, baseColor.b, currentAlpha);
            yield return null;
        }
        targetText.color = new Color(baseColor.r, baseColor.g, baseColor.b, targetAlpha);
        CleanUpCoroutineReference(targetText);
    }

    void CleanUpCoroutineReference(Text targetText)
    {
        if (activeFadeCoroutines.ContainsKey(targetText)) activeFadeCoroutines.Remove(targetText);
    }

    // --- Función para Cargar Escena (Llamada por el controlador de transición) ---
    void LoadGameScene()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            try
            {
                SceneManager.LoadScene(sceneToLoad);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error al cargar escena '" + sceneToLoad + "': " + e.Message + ". ¿Está la escena en Build Settings?");
            }
        }
        else { Debug.LogError("¡El nombre de la escena ('sceneToLoad') está vacío!"); }
    }
    // --- Función para Cargar Escena (Llamada por el controlador de transición) ---
    void LoadGameScene2()
    {
        if (!string.IsNullOrEmpty(sceneToLoad2))
        {
            try
            {
                SceneManager.LoadScene(sceneToLoad2);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error al cargar escena '" + sceneToLoad2 + "': " + e.Message + ". ¿Está la escena en Build Settings?");
            }
        }
        else { Debug.LogError("¡El nombre de la escena ('sceneToLoad') está vacío!"); }
    }
    
    public void ReEnableNavigation()
    {
        Debug.Log("Re-habilitando la navegación del menú principal.");
        this.enabled = true;
        navigationBlocked = false;
    }
}