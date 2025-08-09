using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// Clase auxiliar para organizar los elementos de cada opción en el Inspector
// Es magia :D wiii
[System.Serializable]
public class DecisionOptionUI
{
    public GameObject container;
    public Text optionText;
    public CanvasGroup canvasGroup;
}


public class DecisionController : MonoBehaviour
{
    private GameController gameController;
    public ControlsGameController controlsGameController;
    [Header("UI de Decisiones")]
    public List<DecisionOptionUI> optionUIs;


    [Header("Apariencia de Opciones")]
    [Range(0f, 1f)] public float normalAlpha = 0.7f;
    [Range(0f, 1f)] public float selectedAlpha = 1.0f;

    [Space(10)]

    [Header("Animación")]
    public float fadeInDuration = 0.3f;

    // --- Variables Internas ---
    private int currentSelectionIndex = 0;
    private bool isDecisionActive = false;
    private List<string> currentOptionValues = new List<string>();

    private List<string> currentOptionNextIDs = new List<string>();

    private int activeOptionsCount = 0;
    private bool _inputEnabled = true;
    public bool IsDecisionActive { get { return isDecisionActive; } }

    void Start()
    {
        gameController = FindObjectOfType<GameController>();
        controlsGameController = FindObjectOfType<ControlsGameController>();

        foreach (var optionUI in optionUIs)
        {
            if (optionUI.container != null) optionUI.container.SetActive(false);
        }
    }

    void Update()
    {
        if (!isDecisionActive || !_inputEnabled) return;

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            currentSelectionIndex = (currentSelectionIndex - 1 + activeOptionsCount) % activeOptionsCount;
            UpdateHighlight();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            currentSelectionIndex = (currentSelectionIndex + 1) % activeOptionsCount;
            UpdateHighlight();
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            // Verificamos que no estemos ya procesando una selección
            if (isDecisionActive)
            {
                StartCoroutine(SelectOptionAndAdvance());
            }
        }
    }

    // El GameController llamará a este método para iniciar una decisión
    public void ShowDecision(Dictionary<string, string> decisionData)
    {
        _inputEnabled = false;
        // Pausamos los controles normales del juego AHORA
        if (controlsGameController != null) controlsGameController.enabled = false;

        StartCoroutine(ShowDecisionCoroutine(decisionData));
    }

    // ===== Corrutina para mostrar las decisiones con animación =====
    private IEnumerator ShowDecisionCoroutine(Dictionary<string, string> decisionData)
    {
        currentOptionValues.Clear();
        currentOptionNextIDs.Clear();
        activeOptionsCount = 0;
        List<DecisionOptionUI> uiToAnimate = new List<DecisionOptionUI>();

        // Preparamos las opciones que vamos a mostrar
        for (int i = 1; i <= 5; i++)
        {
            string textKey = "Opcion" + i + "_Texto";
            string valueKey = "Opcion" + i + "_Valor";
            string nextIdKey = "Opcion" + i + "_SgtID";

            if (decisionData.ContainsKey(textKey) && !string.IsNullOrEmpty(decisionData[textKey]) && i - 1 < optionUIs.Count)
            {
                DecisionOptionUI ui = optionUIs[i - 1];
                ui.optionText.text = decisionData[textKey];

                // Ponemos su alfa a 0 y la preparamos para la animación
                if (ui.canvasGroup != null) ui.canvasGroup.alpha = 0f;

                ui.container.SetActive(true);
                uiToAnimate.Add(ui);
                currentOptionValues.Add(decisionData[valueKey]);
                currentOptionNextIDs.Add(decisionData.ContainsKey(nextIdKey) ? decisionData[nextIdKey] : "");
                activeOptionsCount++;
            }
            else if (i - 1 < optionUIs.Count)
            {
                optionUIs[i - 1].container.SetActive(false);
            }
        }

        if (activeOptionsCount > 0)
        {
            // Hacemos el fadeIn de todas las opciones a la vez
            float elapsedTime = 0f;
            while (elapsedTime < fadeInDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeInDuration;
                for (int i = 0; i < activeOptionsCount; i++)
                {
                    // CAMBIO: Animamos el alfa del CanvasGroup.
                    if (uiToAnimate[i].canvasGroup != null)
                    {
                        bool isHighlighted = (i == currentSelectionIndex);
                        float targetAlpha = isHighlighted ? selectedAlpha : normalAlpha;
                        uiToAnimate[i].canvasGroup.alpha = Mathf.Lerp(0f, targetAlpha, progress);
                    }
                }
                yield return null;
            }

            // Aseguramos el estado final
            UpdateHighlight();
            isDecisionActive = true;
            _inputEnabled = true;
        }
        else
        {
            // Si no hay opciones, reactivamos los controles y avanzamos
            if (controlsGameController != null) controlsGameController.enabled = true;
            gameController.AdvanceToNextNode();
        }
    }

    // El método para resaltar ahora solo cambia los colores
    private void UpdateHighlight()
    {
        for (int i = 0; i < activeOptionsCount; i++)
        {
            if (optionUIs[i].canvasGroup != null)
            {
                optionUIs[i].canvasGroup.alpha = (i == currentSelectionIndex) ? selectedAlpha : normalAlpha;
            }
            //Sigo sin entender los [] y : xd
        }
    }

    // Creamos esta corrutina para controlar el timing de la selección y el avance.
    private IEnumerator SelectOptionAndAdvance()
    {
        _inputEnabled = false;
        isDecisionActive = false;

        string selectedValue = currentOptionValues[currentSelectionIndex];
        string nextNodeID = currentOptionNextIDs[currentSelectionIndex];
        Debug.Log("Decisión final tomada: " + selectedValue + ". Saltando a ID: " + nextNodeID);
        ParseAndExecuteValue(selectedValue);

        foreach (var optionUI in optionUIs)
        {
            if (optionUI.container != null) optionUI.container.SetActive(false);
        }

        if (!string.IsNullOrEmpty(nextNodeID))
        {
            gameController.DisplayNode(nextNodeID);
        }
        else
        {
            Debug.LogWarning("No se especificó un OpcionX_SgtID para la decisión. Usando SiguienteID del guion (si existe).");
            gameController.AdvanceToNextNode();
        }

        yield return new WaitForEndOfFrame();

        if (controlsGameController != null) controlsGameController.enabled = true;

        _inputEnabled = true;
    }

    // Se llama cuando el jugador presiona 'A'
    private void SelectCurrentOption()
    {
        isDecisionActive = false; // Desactivamos la navegación
        string selectedValue = currentOptionValues[currentSelectionIndex];

        Debug.Log("Decisión tomada: " + selectedValue);
        ParseAndExecuteValue(selectedValue);

        // Ocultamos la UI de decisiones
        foreach (var optionUI in optionUIs)
        {
            if (optionUI.container != null) optionUI.container.SetActive(false);
        }

        // Reactivamos los controles normales del juego
        if (controlsGameController != null) controlsGameController.enabled = true;

        // Le decimos al GameController que avance al ID ESPECÍFICO
        string nextNodeID = currentOptionNextIDs[currentSelectionIndex];
        if (!string.IsNullOrEmpty(nextNodeID))
        {
            gameController.DisplayNode(nextNodeID);
        }
        else
        {
            gameController.AdvanceToNextNode();
        }
    }

    public void SetInputActive(bool active)
    {
        _inputEnabled = active;
    }
    // ===== Lógica de parseo corregida para manejar "AND" =====
    private void ParseAndExecuteValue(string valueString)
    {
        if (valueString.ToLower() == "none") return;

        string[] parts = valueString.Split('-');

        if (parts[0].ToLower() == "flag")
        {
            // Lógica para FLAGS (ej: "flag-training" o "flag-training-AND-mission")
            string fullFlagKey = string.Join("-", parts, 1, parts.Length - 1);
            string[] flagNames = fullFlagKey.Split(new string[] { "-and-" }, System.StringSplitOptions.None);
            foreach (string flagName in flagNames)
            {
                GameStateManager.Instance.SetFlag(flagName.Trim(), true);
            }
        }
        else
        {
            // Lógica para SUMS (ej: "shizune-plus-1" o "hanako-AND-lilly-plus-1")
            int valueToAdd;
            if (!int.TryParse(parts[parts.Length - 1], out valueToAdd)) return; // Salida segura si el valor no es un número

            string command = parts[parts.Length - 2]; // "plus"

            // Obtenemos todas las partes que componen los nombres de las variables
            string fullVariableKey = string.Join("-", parts, 0, parts.Length - 2);

            // Separamos por el conector "-and-" (insensible a mayúsculas)
            string[] variableNames = fullVariableKey.Split(new string[] { "-and-", "-AND-" }, System.StringSplitOptions.None);

            foreach (string varName in variableNames)
            {
                if (command.ToLower() == "plus")
                {
                    GameStateManager.Instance.AddToSum(varName.Trim(), valueToAdd);
                }
            }
        }
    }
    
    public void HideAndReset()
    {
        Debug.Log("DecisionController: Ocultando y reseteando estado.");

        // Ocultamos todos los contenedores de las opciones.
        foreach (var optionUI in optionUIs)
        {
            if (optionUI.container != null) optionUI.container.SetActive(false);
        }

        // Reseteamos todas las variables internas a su estado por defecto.
        isDecisionActive = false;
        _inputEnabled = false; // IMPORTANTE DESACTIVARLO O EXPLOTA TODO CRJO
        currentSelectionIndex = 0;
        activeOptionsCount = 0;
        currentOptionValues.Clear();
    }
}