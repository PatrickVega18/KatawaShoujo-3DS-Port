using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using System.Collections;
using UnityEngine.UI;

//Script principal >:D

public class GameController : MonoBehaviour
{
    [Header("Referencias a Controladores")]
    public CSVLoader csvLoader;
    public DialogueUIManager dialogueManager;
    //public TypewriterDialogueUI dialogueManager;
    public BigDialogueController bigDialogueController;
    public MusicController musicController;
    public BackgroundController backgroundController;

    // ========= SPRITES =========
    public SpritesController spritesController;
    public FacesController facesController;

    public HeadsController headsController;
    public LeftArmController leftArmController;
    public RightArmController rightArmController;

    public EventsController eventsController;
    // ===== La referencia ahora es privada, la buscaremos por código =====
    private DecisionController decisionController;

    // ===== Añadimos de nuevo las referencias a los Canvas =====
    [Header("Referencias a Canvas")]
    public Canvas upperCanvas;
    public Canvas lowerCanvas;

    [HideInInspector] // La ocultamos del Inspector, se controla por código.
    public bool IsLogicPaused = false;


    private List<Dictionary<string, string>> guionPrincipalData;
    private List<Dictionary<string, string>> dialogoIdiomaData;
    public string currentNodeID = "intro_01";

    public string currentSceneDescription = "";
    public string sceneDescriptionNodeID = "";

    public Dictionary<string, string> currentGuionNode;
    public bool autoAdvanceEnabledForCurrentNode = false;
    private Coroutine activeDisplayCoroutine = null;
    private bool _isLoadingState = false;

    void Start()
    {
        decisionController = FindObjectOfType<DecisionController>();

        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.IsLoadingGame)
        {
            Debug.Log("GameController.Start: Carga de partida en progreso. Se omite el inicio del nodo por defecto.");
            return;
        }

        if (csvLoader == null)
        {
            Debug.LogError("GameController: CSVLoader no asignado en Inspector!", gameObject);
            this.enabled = false;
            return;
        }
        if (dialogueManager == null)
        {
            Debug.LogError("GameController: DialogueUIManager no asignado en Inspector!", gameObject);
            this.enabled = false;
            return;
        }
        if (bigDialogueController == null)
        {
            Debug.LogWarning("GameController: BigDialogueController no asignado en Inspector! El modo BigBoxSay no funcionará.", gameObject);
        }
        if (musicController == null)
        {
            Debug.LogError("GameController: MusicController no asignado en Inspector!", gameObject);
            this.enabled = false;
            return;
        }
        if (backgroundController == null)
        {
            Debug.LogError("GameController: BackgroundController no asignado en Inspector!", gameObject);
            this.enabled = false;
            return;
        }
        if (spritesController == null)
        {
            Debug.LogError("GameController: SpritesController no asignado en Inspector!", gameObject);
            this.enabled = false;
            return;
        }
        if (facesController == null)
        {
            Debug.LogError("GameController: FacesController no asignado en Inspector!", gameObject);
            this.enabled = false; return;
        }
        if (eventsController == null)
        {
            Debug.LogError("GameController: EventsController no asignado en Inspector!", gameObject);
            this.enabled = false;

            return;
        }

        guionPrincipalData = csvLoader.GetMainGuionData();
        dialogoIdiomaData = csvLoader.GetDialogData();

        if (guionPrincipalData == null || guionPrincipalData.Count == 0)
        {
            Debug.LogError("GameController: Los datos del CSV principal no se cargaron correctamente desde CSVLoader.", gameObject);
            this.enabled = false;
            return;
        }
        if (dialogoIdiomaData == null || dialogoIdiomaData.Count == 0)
        {
            Debug.LogWarning("GameController: Datos de diálogo no cargados o vacíos.");
        }

        if (!string.IsNullOrEmpty(currentNodeID))
        {
            DisplayNode(currentNodeID);
        }
        else
        {
            Debug.LogError("GameController: currentNodeID está vacío o nulo al inicio.");
        }
    }


    bool AreParallelGuionTasksCompleted()
    {
        if (dialogueManager != null && !dialogueManager.TextoCompletado) return false;
        if (bigDialogueController != null && bigDialogueController.IsAnimating) return false;

        if (spritesController != null && !spritesController.SpriteAnimacionCompletada) return false;
        if (facesController != null && !facesController.SpriteAnimacionCompletada) return false;

        if (headsController != null && !headsController.SpriteAnimacionCompletada) return false;
        if (leftArmController != null && !leftArmController.SpriteAnimacionCompletada) return false;
        if (rightArmController != null && !rightArmController.SpriteAnimacionCompletada) return false;
        return true;
    }

    public bool AreAllTasksCompleted()
    {
        if (!AreParallelGuionTasksCompleted()) return false; // Revisa diálogo, sprites, caras

        if (backgroundController != null && backgroundController.IsFondoAnimando)
        {
            string currentBgAnimType = backgroundController.GetCurrentFPAnimationType();
            if (currentBgAnimType == "zoomin" ||
                currentBgAnimType == "zoomout" ||
                currentBgAnimType == "slide")
            {
                // Si es un zoom o slide, NO bloquea AreAllTasksCompleted.
                // Estos continuarán en segundo plano si el jugador avanza por 'A'.
            }
            else
            {
                // Para otros tipos de animación de fondo (fades, shakes), sí deben completarse.
                if (!backgroundController.FondoAnimacionCompletada) return false;
            }
        }
        else if (backgroundController != null && !backgroundController.FondoAnimacionCompletada)
        {
            // Si no está animando pero su flag de completado es falso (estado inconsistente o post-animación)
            return false;
        }


        if (eventsController != null && !eventsController.EventoAnimacionCompletada) return false;
        return true;
    }

    public void DisplayNode(string nodeID)
    {
        // Detiene cualquier proceso de nodo anterior que aún pudiera estar corriendo
        if (activeDisplayCoroutine != null)
        {
            StopCoroutine(activeDisplayCoroutine);
        }
        // Inicia la nueva corrutina para mostrar el nodo actual
        activeDisplayCoroutine = StartCoroutine(DisplayNodeCoroutine(nodeID));
    }

    private IEnumerator DisplayNodeCoroutine(string nodeID)
    {
        // --- PREPARACIÓN Y CARGA DE DATOS ---
        if (string.IsNullOrEmpty(nodeID))
        {
            Debug.LogError("GC: ID de nodo nulo o vacío.");
            yield break;
        }
        currentNodeID = nodeID;
        currentGuionNode = csvLoader.GetGuionEntryByID(nodeID);
        Dictionary<string, string> dialogoNode = csvLoader.GetDialogEntryByID(nodeID);

        if (currentGuionNode == null)
        {
            Debug.LogErrorFormat("GC: No se encontró entrada para ID '{0}' en guion.csv.", nodeID);
            if (dialogueManager != null) dialogueManager.ShowDialogue("ERROR", "Guion no encontrado ID: " + nodeID, 0.05f, false);
            this.enabled = false;
            yield break;
        }

        // --- PROCESAMIENTO INICIAL Y NODOS ESPECIALES (SIN ESPERA) ---
        if (currentGuionNode.ContainsKey("Escena"))
        {
            string nuevaEscena = currentGuionNode["Escena"];
            if (!string.IsNullOrEmpty(nuevaEscena.Trim()))
            {
                currentSceneDescription = nuevaEscena;
                sceneDescriptionNodeID = currentNodeID;
            }
        }

        autoAdvanceEnabledForCurrentNode = false;
        string tipoNodoActual = currentGuionNode.ContainsKey("TipoNodo") ? currentGuionNode["TipoNodo"] : "Dialogo";

        if (tipoNodoActual.StartsWith("escena-"))
        {
            string sceneNameToLoad = tipoNodoActual.Substring("escena-".Length);
            if (!string.IsNullOrEmpty(sceneNameToLoad))
            {
                SceneManager.LoadScene(sceneNameToLoad);
            }
            yield break;
        }

        if (tipoNodoActual.ToLowerInvariant() == "checksum" || tipoNodoActual.ToLowerInvariant() == "checkflag")
        {
            ProcessConditionalNode(tipoNodoActual);
            yield break;
        }

        if (tipoNodoActual.Equals("FinalizarEvento", System.StringComparison.OrdinalIgnoreCase) && eventsController != null)
        {
            eventsController.FinalizarTodosLosEventosPersistentesVisuales();
        }

        // --- EJECUCIÓN DE TODOS LOS COMANDOS VISUALES DEL GUION ---
        bool esteNodoTieneDialogo = (dialogoNode != null &&
                            ((dialogoNode.ContainsKey("Texto") && !string.IsNullOrEmpty(dialogoNode["Texto"])) ||
                                (dialogoNode.ContainsKey("Nombre") && !string.IsNullOrEmpty(dialogoNode["Nombre"]))));

        if (bigDialogueController != null && !bigDialogueController.IsBigBoxModeActive && dialogueManager != null)
        {
            if (esteNodoTieneDialogo)
            {
                // Si el NODO NUEVO tiene diálogo, solo limpiamos el contenido, sin tocar las cajas.
                dialogueManager.ClearDialogueContent();
            }
            else
            {
                // Si el NODO NUEVO NO tiene diálogo, hacemos una limpieza completa que sí oculta las cajas.
                dialogueManager.ShowDialogue("", "", 0f, false);
            }
        }

        bool hayAnimacionDeSpriteEnGuion = CheckForSpriteActionsInNode();

        if (!_isLoadingState)
        {
            ProcessGuionVisualCommands(tipoNodoActual);
        }

        if (!_isLoadingState)
        {
            if (hayAnimacionDeSpriteEnGuion)
            {
                yield return new WaitUntil(() => !AreInitialSpriteAnimationsRunning());
            }

            // Mostrar diálogo o decisión animado
            if (tipoNodoActual.Equals("Decision", System.StringComparison.OrdinalIgnoreCase))
            {
                if (esteNodoTieneDialogo)
                {
                    ShowDialogueFromNode(dialogoNode, tipoNodoActual);
                }
                if (decisionController != null)
                {
                    Dictionary<string, string> decisionData = csvLoader.GetDecisionEntryByID(nodeID);
                    if (decisionData != null)
                    {
                        decisionController.ShowDecision(decisionData);
                    }
                    else
                    {
                        Debug.LogErrorFormat("GC: TipoNodo='Decision' pero no se encontró entrada en Decisiones.csv para ID '{0}'.", nodeID);
                        AdvanceToNextNode();
                    }
                }
                yield break; // Salir para que no procese eventos de nodo normal
            }
            else if (esteNodoTieneDialogo)
            {
                ShowDialogueFromNode(dialogoNode, tipoNodoActual);
            }
            else if (!hayAnimacionDeSpriteEnGuion)
            {
                dialogueManager.ShowDialogue("", "", 0.05f, false);
            }
        }
        // SI SÍ ESTAMOS CARGANDO, ejecutamos la lógica instantánea.
        else
        {
            // Mostrar diálogo o decisión instantáneo
            if (tipoNodoActual.Equals("Decision", System.StringComparison.OrdinalIgnoreCase))
            {
                if (esteNodoTieneDialogo) { ShowDialogueFromNode(dialogoNode, tipoNodoActual); }
                if (dialogueManager != null) dialogueManager.CompleteTypewriter();
                if (decisionController != null)
                {
                    Dictionary<string, string> decisionData = csvLoader.GetDecisionEntryByID(nodeID);
                    if (decisionData != null)
                    {
                        decisionController.ShowDecision(decisionData);
                    }
                    else
                    {
                        Debug.LogErrorFormat("GC: TipoNodo='Decision' pero no se encontró entrada en Decisiones.csv para ID '{0}'.", nodeID);
                        AdvanceToNextNode();
                    }
                }
                yield break;
            }
            else if (esteNodoTieneDialogo)
            {
                ShowDialogueFromNode(dialogoNode, tipoNodoActual);
                // Inmediatamente después de pedir que se muestre, lo completamos.
                if (dialogueManager != null)
                {
                    dialogueManager.CompleteTypewriter();
                }
            }

            _isLoadingState = false;
        }
        ProcessGuionEventCommands(tipoNodoActual);
    }

    private string ParseOrdenDeCapa(string coordenadasCsv)
    {
        if (string.IsNullOrEmpty(coordenadasCsv)) return "";

        string[] parts = coordenadasCsv.ToLower().Split(' ');
        // Buscamos la palabra clave en las partes del string.
        foreach (string part in parts)
        {
            if (part == "encima" || part == "debajo")
            {
                return part; // La hemos encontrado, la devolvemos.
            }
        }
        return ""; // No se encontró ninguna palabra clave.
    }

    public void UpdateSpriteLayering(int slotIndex, MonoBehaviour partController, string orderKeyword)
    {
        // Salimos si no hay una orden válida.
        if (string.IsNullOrEmpty(orderKeyword)) return;

        // Obtenemos los transforms de las imágenes del cuerpo principal.
        Transform bodyMain = spritesController.allSlots[slotIndex].mainImage.transform;
        Transform bodySwitch = spritesController.allSlots[slotIndex].switchImage.transform;

        // Obtenemos los transforms de la parte que queremos ordenar.
        Image partMainImage = null;
        Image partSwitchImage = null;

        if (partController is LeftArmController)
        {
            partMainImage = (partController as LeftArmController).allSlots[slotIndex].mainImage;
            partSwitchImage = (partController as LeftArmController).allSlots[slotIndex].switchImage;
        }
        else if (partController is RightArmController)
        {
            partMainImage = (partController as RightArmController).allSlots[slotIndex].mainImage;
            partSwitchImage = (partController as RightArmController).allSlots[slotIndex].switchImage;
        }

        // Si no encontramos las partes, no podemos continuar.
        if (partMainImage == null || partSwitchImage == null) return;

        Transform partMain = partMainImage.transform;
        Transform partSwitch = partSwitchImage.transform;

        // ----- La Lógica de Reordenamiento -----
        if (orderKeyword == "debajo")
        {
            int bodyIndex = bodyMain.GetSiblingIndex();
            partMain.SetSiblingIndex(bodyIndex);
            partSwitch.SetSiblingIndex(bodyIndex); // Al llamar dos veces, el último se queda en el índice deseado.
        }
        else if (orderKeyword == "encima")
        {
            int bodySwitchIndex = bodySwitch.GetSiblingIndex();
            partMain.SetSiblingIndex(bodySwitchIndex + 1);
            partSwitch.SetSiblingIndex(bodySwitchIndex + 1);
        }
    }
    // ===== Modificamos AdvanceToNextNode para que acepte un parámetro opcional =====
    public void AdvanceToNextNode(bool? useTruePath = null)
    {
        string nextID = "";
        if (useTruePath.HasValue) // Si se especificó un camino (para checksum/checkflag)
        {
            if (useTruePath.Value) // Usar el camino verdadero
            {
                nextID = currentGuionNode.ContainsKey("SiguienteID_Si_Verdadero") ? currentGuionNode["SiguienteID_Si_Verdadero"] : "";
            }
            else // Usar el camino falso
            {
                nextID = currentGuionNode.ContainsKey("SiguienteID_Si_Falso") ? currentGuionNode["SiguienteID_Si_Falso"] : "";
            }
        }
        else // Comportamiento normal: usar SiguienteID
        {
            nextID = currentGuionNode.ContainsKey("SiguienteID") ? currentGuionNode["SiguienteID"] : "";
        }

        if (!string.IsNullOrEmpty(nextID))
        {
            DisplayNode(nextID);
        }
        else
        {
            Debug.Log("Fin de la rama (SiguienteID no encontrado o vacío). Nodo: " + currentNodeID);
        }
    }
    // ===== Métodos de ayuda para evaluar las condiciones =====
    private bool EvaluateChecksumCondition(string variables, string condition)
    {
        if (string.IsNullOrEmpty(variables) || string.IsNullOrEmpty(condition)) return false;

        // Parsear la condición (ej: ">=10")
        Match match = Regex.Match(condition, @"([<>=!]+)\s*(-?\d+)");
        if (!match.Success) return false;

        string op = match.Groups[1].Value;
        int requiredValue = int.Parse(match.Groups[2].Value);

        // Parsear las variables (ej: "hanako-AND-lilly")
        string[] variableNames = variables.Split(new string[] { "-and-" }, System.StringSplitOptions.None);

        // Evaluar
        foreach (string varName in variableNames)
        {
            int currentVal = GameStateManager.Instance.GetSumValue(varName.Trim());
            bool conditionMetForThisVar = false;
            switch (op)
            {
                case ">=": conditionMetForThisVar = currentVal >= requiredValue; break;
                case "<=": conditionMetForThisVar = currentVal <= requiredValue; break;
                case ">": conditionMetForThisVar = currentVal > requiredValue; break;
                case "<": conditionMetForThisVar = currentVal < requiredValue; break;
                case "=": conditionMetForThisVar = currentVal == requiredValue; break;
                case "!=": conditionMetForThisVar = currentVal != requiredValue; break;
            }
            // Si UNA SOLA de las condiciones en una cadena "AND" es falsa, el resultado total es falso.
            if (!conditionMetForThisVar) return false;
        }

        // Si hemos llegado hasta aquí, todas las condiciones eran verdaderas.
        return true;
    }

    private bool EvaluateCheckflagCondition(string variables, string condition)
    {
        if (string.IsNullOrEmpty(variables)) return false;

        // Parsear la condición ("on" o "off")
        bool requiredValue = condition.ToLowerInvariant() == "on";

        // Parsear las variables
        string[] flagNames = variables.Split(new string[] { "-and-" }, System.StringSplitOptions.None);

        // Evaluar
        foreach (string flagName in flagNames)
        {
            bool currentVal = GameStateManager.Instance.GetFlag(flagName.Trim());
            // Si el valor actual no es el requerido, la condición "AND" falla.
            if (currentVal != requiredValue) return false;
        }

        // Si hemos llegado hasta aquí, todas las flags tenían el estado requerido :D
        return true;
    }
    public IEnumerator RestoreGameState(NovelaSaveData data)
    {
        Debug.Log("GameController recibiendo datos de guardado para restaurar.");

        ControlsGameController cgc = FindObjectOfType<ControlsGameController>();
        SFXController sfxC = FindObjectOfType<SFXController>();
        if (cgc != null) cgc.enabled = false;

        if (decisionController != null)
        {
            decisionController.HideAndReset();
        }
        _isLoadingState = true;
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.RestoreState(data.sumVariables, data.flagVariables, data.playtimeInSeconds);
        }
        if (musicController != null) musicController.RestoreState(data.musicState);

        //Che problema de carga y guardado . _.
        if (sfxC != null) sfxC.RestoreState(data.sfxState);
        if (backgroundController != null) yield return StartCoroutine(backgroundController.RestoreState(data.backgroundState));
        Debug.Log("--- [DEBUG-LOAD] Restaurando datos de Sprites ---");
        if (spritesController != null)
        {
            for (int i = 0; i < data.spriteStates.Count; i++)
            {
                Debug.Log(string.Format("[DEBUG-LOAD] SpritesController Slot {0}: isActive={1}, Path='{2}'", i, data.spriteStates[i].isActive, data.spriteStates[i].spritePath));
            }
            yield return StartCoroutine(spritesController.RestoreStates(data.spriteStates));
        }
        if (facesController != null)
        {
            for (int i = 0; i < data.faceStates.Count; i++)
            {
                Debug.Log(string.Format("[DEBUG-LOAD] FacesController Slot {0}: isActive={1}, Path='{2}'", i, data.faceStates[i].isActive, data.faceStates[i].spritePath));
            }
            yield return StartCoroutine(facesController.RestoreStates(data.faceStates));
        }
        if (headsController != null)
        {
            for (int i = 0; i < data.headStates.Count; i++)
            {
                Debug.Log(string.Format("[DEBUG-LOAD] HeadsController Slot {0}: isActive={1}, Path='{2}'", i, data.headStates[i].isActive, data.headStates[i].spritePath));
            }
            yield return StartCoroutine(headsController.RestoreStates(data.headStates));
        }
        if (leftArmController != null)
        {
            for (int i = 0; i < data.leftArmStates.Count; i++)
            {
                Debug.Log(string.Format("[DEBUG-LOAD] LeftArmController Slot {0}: isActive={1}, Path='{2}'", i, data.leftArmStates[i].isActive, data.leftArmStates[i].spritePath));
            }
            yield return StartCoroutine(leftArmController.RestoreStates(data.leftArmStates));
        }
        if (rightArmController != null)
        {
            for (int i = 0; i < data.rightArmStates.Count; i++)
            {
                Debug.Log(string.Format("[DEBUG-LOAD] RightArmController Slot {0}: isActive={1}, Path='{2}'", i, data.rightArmStates[i].isActive, data.rightArmStates[i].spritePath));
            }
            yield return StartCoroutine(rightArmController.RestoreStates(data.rightArmStates));
        }
        Debug.Log("--- [DEBUG-LOAD] Fin de la restauración de Sprites ---");

        // Restauramos el diálogo "de golpe"
        currentNodeID = data.currentNodeID;
        currentGuionNode = csvLoader.GetGuionEntryByID(currentNodeID);
        sceneDescriptionNodeID = data.sceneDescriptionNodeID;
        if (!string.IsNullOrEmpty(sceneDescriptionNodeID))
        {
            Dictionary<string, string> sceneNodeData = csvLoader.GetGuionEntryByID(sceneDescriptionNodeID);
            if (sceneNodeData != null && sceneNodeData.ContainsKey("Escena"))
            {
                currentSceneDescription = sceneNodeData["Escena"];
            }
            else
            {
                currentSceneDescription = "";
            }
        }
        else
        {
            currentSceneDescription = "";
        }
        string tipoNodoRestaurado = "";
        if (currentGuionNode != null && currentGuionNode.ContainsKey("TipoNodo"))
        {
            tipoNodoRestaurado = currentGuionNode["TipoNodo"];
        }

        DisplayNode(currentNodeID);

        StartCoroutine(RefreshAllCanvases());

        if (cgc != null && tipoNodoRestaurado.ToLowerInvariant() != "decision")
        {
            cgc.enabled = true;
        }

        Debug.Log("Restauración completa.");

    }



    // ===== Añadimos de nuevo la corrutina de refresco =====
    private IEnumerator RefreshAllCanvases()
    {
        if (upperCanvas != null) upperCanvas.enabled = false;
        if (lowerCanvas != null) lowerCanvas.enabled = false;

        yield return null;

        if (upperCanvas != null) upperCanvas.enabled = true;
        if (lowerCanvas != null) lowerCanvas.enabled = true;

        Debug.Log("Refresco de Canvas forzado.");
    }

    private bool AreInitialSpriteAnimationsRunning()
    {
        if (spritesController != null && spritesController.IsSpriteAnimando) return true;
        if (facesController != null && facesController.IsSpriteAnimando) return true;
        if (headsController != null && headsController.IsSpriteAnimando) return true;
        if (leftArmController != null && leftArmController.IsSpriteAnimando) return true;
        if (rightArmController != null && rightArmController.IsSpriteAnimando) return true;
        return false;
    }

    private IEnumerator WaitForSpritesAndThenShowDialogue(Dictionary<string, string> dialogoNode)
    {
        yield return new WaitUntil(() => !AreInitialSpriteAnimationsRunning());

        if (dialogueManager != null)
        {
            string characterName = dialogoNode.ContainsKey("Nombre") ? dialogoNode["Nombre"] : "";
            string fullDialogueText = dialogoNode.ContainsKey("Texto") ? dialogoNode["Texto"] : "";
            float textSpeed = 0.05f;
            if (dialogoNode.ContainsKey("Velocidad") && !string.IsNullOrEmpty(dialogoNode["Velocidad"]))
            {
                if (!float.TryParse(dialogoNode["Velocidad"], NumberStyles.Any, CultureInfo.InvariantCulture, out textSpeed)) textSpeed = 0.05f;
            }

            string tipoNodoActual = currentGuionNode.ContainsKey("TipoNodo") ? currentGuionNode["TipoNodo"] : "";
            bool esEventoEnTexto = tipoNodoActual.Equals("EventInText", System.StringComparison.OrdinalIgnoreCase);

            dialogueManager.ShowDialogue(characterName, fullDialogueText, textSpeed, esEventoEnTexto);
        }
    }

    private void ProcessConditionalNode(string tipoNodoActual)
    {
        if (tipoNodoActual.ToLowerInvariant() == "checksum")
        {
            string variableToCheck = currentGuionNode.ContainsKey("Variable") ? currentGuionNode["Variable"] : "";
            string valueCondition = currentGuionNode.ContainsKey("Valor") ? currentGuionNode["Valor"] : "";
            bool conditionMet = EvaluateChecksumCondition(variableToCheck, valueCondition);
            AdvanceToNextNode(conditionMet);
        }
        else if (tipoNodoActual.ToLowerInvariant() == "checkflag")
        {
            string flagToCheck = currentGuionNode.ContainsKey("Variable") ? currentGuionNode["Variable"] : "";
            string valueCondition = currentGuionNode.ContainsKey("Valor") ? currentGuionNode["Valor"] : "on";
            bool conditionMet = EvaluateCheckflagCondition(flagToCheck, valueCondition);
            AdvanceToNextNode(conditionMet);
        }
    }

    private bool CheckForSpriteActionsInNode()
    {
        for (int i = 1; i <= 3; i++)
        {
            if (currentGuionNode.ContainsKey("Sprite" + i) && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "_Accion"])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "Cara") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cara"])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "Cara_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cara_Accion"])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "Cabeza") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cabeza"])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "Cabeza_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cabeza_Accion"])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoIzq"])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoIzq_Accion"])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoDer"])) return true;
            if (currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoDer_Accion"])) return true;
        }
        return false;
        // La complejidad ciclomática es un invento de los papás xd
    }

    private void ProcessGuionVisualCommands(string tipoNodoActual)
    {
        if (musicController != null && (currentGuionNode.ContainsKey("Musica_Accion") && !string.IsNullOrEmpty(currentGuionNode["Musica_Accion"])))
        {
            string musicaPath = currentGuionNode.ContainsKey("Musica") ? currentGuionNode["Musica"] : null;
            string musicaAccion = currentGuionNode["Musica_Accion"];
            float musicaFadeTiempo = 1.0f;
            if (currentGuionNode.ContainsKey("Musica_AccionTiempo") && !string.IsNullOrEmpty(currentGuionNode["Musica_AccionTiempo"]))
            {
                if (!float.TryParse(currentGuionNode["Musica_AccionTiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out musicaFadeTiempo)) musicaFadeTiempo = 1.0f;
            }
            musicController.ProcessMusicCommand(musicaPath, musicaAccion, musicaFadeTiempo);
        }

        string fpPathCmd = currentGuionNode.ContainsKey("FondoPrincipal") ? currentGuionNode["FondoPrincipal"] : null;
        string fpAccionCmd = currentGuionNode.ContainsKey("FondoPrincipal_Accion") ? currentGuionNode["FondoPrincipal_Accion"] : null;
        string fpTamanoCmd = currentGuionNode.ContainsKey("FondoPrincipal_Tamano") ? currentGuionNode["FondoPrincipal_Tamano"] : null;
        string fpCoordsCmd = currentGuionNode.ContainsKey("FondoPrincipal_Coordenadas") ? currentGuionNode["FondoPrincipal_Coordenadas"] : null;
        bool tieneComandoFondoPrincipal = !string.IsNullOrEmpty(fpPathCmd) || !string.IsNullOrEmpty(fpAccionCmd) || !string.IsNullOrEmpty(fpTamanoCmd) || !string.IsNullOrEmpty(fpCoordsCmd);

        if (backgroundController != null)
        {
            if (tieneComandoFondoPrincipal)
            {
                if (string.IsNullOrEmpty(fpAccionCmd) && (!string.IsNullOrEmpty(fpPathCmd) || !string.IsNullOrEmpty(fpTamanoCmd)))
                {
                    fpAccionCmd = "punch";
                }
                float fpTiempo = 0f;
                if (currentGuionNode.ContainsKey("FondoPrincipal_AccionTiempo") && !string.IsNullOrEmpty(currentGuionNode["FondoPrincipal_AccionTiempo"]))
                {
                    if (!float.TryParse(currentGuionNode["FondoPrincipal_AccionTiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out fpTiempo))
                        fpTiempo = (fpAccionCmd == "punch") ? 0f : 1.0f;
                }
                else if (fpAccionCmd != null && !fpAccionCmd.ToLowerInvariant().Contains("punch"))
                {
                    fpTiempo = 1.0f;
                }
                string fsPathCmd_paraSwitch = currentGuionNode.ContainsKey("FondoSecundario") ? currentGuionNode["FondoSecundario"] : null;
                string fsTamanoCmd_paraSwitch = currentGuionNode.ContainsKey("FondoSecundario_Tamano") ? currentGuionNode["FondoSecundario_Tamano"] : null;
                string fsCoordsCmd_paraSwitch = currentGuionNode.ContainsKey("FondoSecundario_Coordenadas") ? currentGuionNode["FondoSecundario_Coordenadas"] : null;
                backgroundController.ProcessFondoPrincipalCommand(
                    fpPathCmd,
                    fpAccionCmd,
                    fpTiempo,
                    fpCoordsCmd,
                    fpTamanoCmd,
                    fsPathCmd_paraSwitch,
                    fsTamanoCmd_paraSwitch,
                    fsCoordsCmd_paraSwitch
                );
            }
            else if (!tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase))
            {
                string currentBgAnimType = backgroundController.GetCurrentFPAnimationType();
                if (currentBgAnimType != "zoomin" && currentBgAnimType != "zoomout" && currentBgAnimType != "slide")
                {
                    backgroundController.ProcessFondoPrincipalCommand(null, null, 0, null, null, null, null, null);
                }
            }
        }

        string fsPathCmd = currentGuionNode.ContainsKey("FondoSecundario") ? currentGuionNode["FondoSecundario"] : null;
        string fsTamanoCmd = currentGuionNode.ContainsKey("FondoSecundario_Tamano") ? currentGuionNode["FondoSecundario_Tamano"] : null;
        string fsCoordsCmd = currentGuionNode.ContainsKey("FondoSecundario_Coordenadas") ? currentGuionNode["FondoSecundario_Coordenadas"] : null;
        bool tieneComandoFondoSecundario = !string.IsNullOrEmpty(fsPathCmd) || !string.IsNullOrEmpty(fsTamanoCmd) || !string.IsNullOrEmpty(fsCoordsCmd);

        if (backgroundController != null && tieneComandoFondoSecundario)
        {
            backgroundController.ProcessFondoSecundarioCommand(fsPathCmd, fsTamanoCmd, fsCoordsCmd);
        }

        // =================== CUERPO ===================
        bool esteNodoTieneAccionSprite = false;
        for (int i = 1; i <= 3; i++) { if ((currentGuionNode.ContainsKey("Sprite" + i + "_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "_Accion"])) || (currentGuionNode.ContainsKey("Sprite" + i) && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i]))) { esteNodoTieneAccionSprite = true; break; } }

        if (spritesController != null)
        {
            if (esteNodoTieneAccionSprite)
            {
                for (int i = 1; i <= 3; i++)
                {
                    string sPath = currentGuionNode.ContainsKey("Sprite" + i) ? currentGuionNode["Sprite" + i] : null;
                    string sAccion = currentGuionNode.ContainsKey("Sprite" + i + "_Accion") ? currentGuionNode["Sprite" + i + "_Accion"] : null;
                    string sSwitch = currentGuionNode.ContainsKey("Sprite" + i + "_Switch") ? currentGuionNode["Sprite" + i + "_Switch"] : null;
                    string sCoords = currentGuionNode.ContainsKey("Sprite" + i + "_Coordenadas") ? currentGuionNode["Sprite" + i + "_Coordenadas"] : null;
                    float sTiempo = 0f;
                    string sTamano = currentGuionNode.ContainsKey("Sprite" + i + "_Tamano") ? currentGuionNode["Sprite" + i + "_Tamano"] : null;

                    if (currentGuionNode.ContainsKey("Sprite" + i + "_Tiempo") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "_Tiempo"]))
                    {
                        if (!float.TryParse(currentGuionNode["Sprite" + i + "_Tiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out sTiempo)) sTiempo = (sAccion != null && sAccion.ToLowerInvariant() == "punch") ? 0f : 0.5f;
                    }
                    else if (sAccion != null && sAccion.ToLowerInvariant() != "punch") sTiempo = 0.5f;
                    if (string.IsNullOrEmpty(sAccion) && !string.IsNullOrEmpty(sPath)) sAccion = "punch";

                    if (!string.IsNullOrEmpty(sAccion) || !string.IsNullOrEmpty(sPath))
                    {
                        spritesController.ProcessSpriteCommand(i - 1, sPath, sAccion, sSwitch, sCoords, sTiempo, sTamano);
                    }
                    else
                    {
                        spritesController.ProcessSpriteCommand(i - 1, null, null, null, null, 0, null);
                    }
                }
            }
            else if (!tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int k = 0; k < 3; k++) spritesController.ProcessSpriteCommand(k, null, null, null, null, 0, null);
            }
        }

        // =================== CARA ===================
        bool esteNodoTieneAccionCara = false;
        for (int i = 1; i <= 3; i++) { if ((currentGuionNode.ContainsKey("Sprite" + i + "Cara_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cara_Accion"])) || (currentGuionNode.ContainsKey("Sprite" + i + "Cara") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cara"]))) { esteNodoTieneAccionCara = true; break; } }
        if (facesController != null)
        {
            if (esteNodoTieneAccionCara)
            {
                for (int i = 1; i <= 3; i++)
                {
                    string fcPath = currentGuionNode.ContainsKey("Sprite" + i + "Cara") ? currentGuionNode["Sprite" + i + "Cara"] : null;
                    string fcAccion = currentGuionNode.ContainsKey("Sprite" + i + "Cara_Accion") ? currentGuionNode["Sprite" + i + "Cara_Accion"] : null;
                    string fcSwitch = currentGuionNode.ContainsKey("Sprite" + i + "Cara_Switch") ? currentGuionNode["Sprite" + i + "Cara_Switch"] : null;
                    string fcCoords = currentGuionNode.ContainsKey("Sprite" + i + "Cara_Coordenadas") ? currentGuionNode["Sprite" + i + "Cara_Coordenadas"] : null;
                    float fcTiempo = 0f;
                    string fcTamano = currentGuionNode.ContainsKey("Sprite" + i + "Cara_Tamano") ? currentGuionNode["Sprite" + i + "Cara_Tamano"] : null;

                    if (currentGuionNode.ContainsKey("Sprite" + i + "Cara_Tiempo") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cara_Tiempo"]))
                    {
                        if (!float.TryParse(currentGuionNode["Sprite" + i + "Cara_Tiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out fcTiempo)) fcTiempo = (fcAccion != null && fcAccion.ToLowerInvariant() == "punch") ? 0f : 0.5f;
                    }
                    else if (fcAccion != null && fcAccion.ToLowerInvariant() != "punch") fcTiempo = 0.5f;
                    if (string.IsNullOrEmpty(fcAccion) && !string.IsNullOrEmpty(fcPath)) fcAccion = "punch";

                    if (!string.IsNullOrEmpty(fcAccion) || !string.IsNullOrEmpty(fcPath))
                    {
                        facesController.ProcessSpriteCommand(i - 1, fcPath, fcAccion, fcSwitch, fcCoords, fcTiempo, fcTamano);
                    }
                    else
                    {
                        facesController.ProcessSpriteCommand(i - 1, null, null, null, null, 0, null);
                    }
                }
            }
            else if (!tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int k = 0; k < 3; k++) facesController.ProcessSpriteCommand(k, null, null, null, null, 0, null);
            }
        }

        // =========== CABEZA SPRITE ===========
        bool esteNodoTieneAccionCabeza = false;
        for (int i = 1; i <= 3; i++) { if ((currentGuionNode.ContainsKey("Sprite" + i + "Cabeza_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cabeza_Accion"])) || (currentGuionNode.ContainsKey("Sprite" + i + "Cabeza") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cabeza"]))) { esteNodoTieneAccionCabeza = true; break; } }
        if (headsController != null)
        {
            if (esteNodoTieneAccionCabeza)
            {
                for (int i = 1; i <= 3; i++)
                {
                    string hPath = currentGuionNode.ContainsKey("Sprite" + i + "Cabeza") ? currentGuionNode["Sprite" + i + "Cabeza"] : null;
                    string hAccion = currentGuionNode.ContainsKey("Sprite" + i + "Cabeza_Accion") ? currentGuionNode["Sprite" + i + "Cabeza_Accion"] : null;
                    string hSwitch = currentGuionNode.ContainsKey("Sprite" + i + "Cabeza_Switch") ? currentGuionNode["Sprite" + i + "Cabeza_Switch"] : null;
                    string hCoords = currentGuionNode.ContainsKey("Sprite" + i + "Cabeza_Coordenadas") ? currentGuionNode["Sprite" + i + "Cabeza_Coordenadas"] : null;
                    float hTiempo = 0f;
                    string hTamano = currentGuionNode.ContainsKey("Sprite" + i + "Cabeza_Tamano") ? currentGuionNode["Sprite" + i + "Cabeza_Tamano"] : null;

                    if (currentGuionNode.ContainsKey("Sprite" + i + "Cabeza_Tiempo") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "Cabeza_Tiempo"]))
                    {
                        if (!float.TryParse(currentGuionNode["Sprite" + i + "Cabeza_Tiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out hTiempo)) hTiempo = (hAccion != null && hAccion.ToLowerInvariant() == "punch") ? 0f : 0.5f;
                    }
                    else if (hAccion != null && hAccion.ToLowerInvariant() != "punch") hTiempo = 0.5f;
                    if (string.IsNullOrEmpty(hAccion) && !string.IsNullOrEmpty(hPath)) hAccion = "punch";

                    if (!string.IsNullOrEmpty(hAccion) || !string.IsNullOrEmpty(hPath))
                    {
                        headsController.ProcessSpriteCommand(i - 1, hPath, hAccion, hSwitch, hCoords, hTiempo, hTamano);
                    }
                    else
                    {
                        headsController.ProcessSpriteCommand(i - 1, null, null, null, null, 0, null);
                    }
                }
            }
            else if (!tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int k = 0; k < 3; k++) headsController.ProcessSpriteCommand(k, null, null, null, null, 0, null);
            }
        }
        // =========== BRAZOIZQUIERDO SPRITE ===========
        bool esteNodoTieneAccionBrazoIzq = false;
        for (int i = 1; i <= 3; i++) { if ((currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoIzq_Accion"])) || (currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoIzq"]))) { esteNodoTieneAccionBrazoIzq = true; break; } }
        if (leftArmController != null)
        {
            if (esteNodoTieneAccionBrazoIzq)
            {
                for (int i = 1; i <= 3; i++)
                {
                    string biPath = currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq") ? currentGuionNode["Sprite" + i + "BrazoIzq"] : null;
                    string biAccion = currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq_Accion") ? currentGuionNode["Sprite" + i + "BrazoIzq_Accion"] : null;
                    string biSwitch = currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq_Switch") ? currentGuionNode["Sprite" + i + "BrazoIzq_Switch"] : null;
                    string biCoords = currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq_Coordenadas") ? currentGuionNode["Sprite" + i + "BrazoIzq_Coordenadas"] : null;
                    float biTiempo = 0f;
                    string biTamano = currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq_Tamano") ? currentGuionNode["Sprite" + i + "BrazoIzq_Tamano"] : null;

                    if (currentGuionNode.ContainsKey("Sprite" + i + "BrazoIzq_Tiempo") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoIzq_Tiempo"]))
                    {
                        if (!float.TryParse(currentGuionNode["Sprite" + i + "BrazoIzq_Tiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out biTiempo)) biTiempo = (biAccion != null && biAccion.ToLowerInvariant() == "punch") ? 0f : 0.5f;
                    }
                    else if (biAccion != null && biAccion.ToLowerInvariant() != "punch") biTiempo = 0.5f;
                    if (string.IsNullOrEmpty(biAccion) && !string.IsNullOrEmpty(biPath)) biAccion = "punch";

                    if (!string.IsNullOrEmpty(biAccion) || !string.IsNullOrEmpty(biPath))
                    {
                        leftArmController.ProcessSpriteCommand(i - 1, biPath, biAccion, biSwitch, biCoords, biTiempo, biTamano);
                        string ordenDeCapa = ParseOrdenDeCapa(biCoords);
                        if (!string.IsNullOrEmpty(ordenDeCapa))
                        {
                            UpdateSpriteLayering(i - 1, leftArmController, ordenDeCapa);
                        }
                    }
                    else
                    {
                        leftArmController.ProcessSpriteCommand(i - 1, null, null, null, null, 0, null);
                    }
                }
            }
            else if (!tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int k = 0; k < 3; k++) leftArmController.ProcessSpriteCommand(k, null, null, null, null, 0, null);
            }
        }
        // =========== BRAZODERECHO SPRITE ===========
        bool esteNodoTieneAccionBrazoDer = false;
        for (int i = 1; i <= 3; i++) { if ((currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer_Accion") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoDer_Accion"])) || (currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoDer"]))) { esteNodoTieneAccionBrazoDer = true; break; } }
        if (rightArmController != null)
        {
            if (esteNodoTieneAccionBrazoDer)
            {
                for (int i = 1; i <= 3; i++)
                {
                    string bdPath = currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer") ? currentGuionNode["Sprite" + i + "BrazoDer"] : null;
                    string bdAccion = currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer_Accion") ? currentGuionNode["Sprite" + i + "BrazoDer_Accion"] : null;
                    string bdSwitch = currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer_Switch") ? currentGuionNode["Sprite" + i + "BrazoDer_Switch"] : null;
                    string bdCoords = currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer_Coordenadas") ? currentGuionNode["Sprite" + i + "BrazoDer_Coordenadas"] : null;
                    float bdTiempo = 0f;
                    string bdTamano = currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer_Tamano") ? currentGuionNode["Sprite" + i + "BrazoDer_Tamano"] : null;

                    if (currentGuionNode.ContainsKey("Sprite" + i + "BrazoDer_Tiempo") && !string.IsNullOrEmpty(currentGuionNode["Sprite" + i + "BrazoDer_Tiempo"]))
                    {
                        if (!float.TryParse(currentGuionNode["Sprite" + i + "BrazoDer_Tiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out bdTiempo)) bdTiempo = (bdAccion != null && bdAccion.ToLowerInvariant() == "punch") ? 0f : 0.5f;
                    }
                    else if (bdAccion != null && bdAccion.ToLowerInvariant() != "punch") bdTiempo = 0.5f;
                    if (string.IsNullOrEmpty(bdAccion) && !string.IsNullOrEmpty(bdPath)) bdAccion = "punch";

                    if (!string.IsNullOrEmpty(bdAccion) || !string.IsNullOrEmpty(bdPath))
                    {
                        rightArmController.ProcessSpriteCommand(i - 1, bdPath, bdAccion, bdSwitch, bdCoords, bdTiempo, bdTamano);
                        string ordenDeCapa = ParseOrdenDeCapa(bdCoords);
                        if (!string.IsNullOrEmpty(ordenDeCapa))
                        {
                            UpdateSpriteLayering(i - 1, rightArmController, ordenDeCapa);
                        }
                    }
                    else
                    {
                        rightArmController.ProcessSpriteCommand(i - 1, null, null, null, null, 0, null);
                    }
                }
            }
            else if (!tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int k = 0; k < 3; k++) rightArmController.ProcessSpriteCommand(k, null, null, null, null, 0, null);
            }
        }
    }

    private void ShowDialogueFromNode(Dictionary<string, string> dialogoNode, string tipoNodoActual)
    {
        if (bigDialogueController != null && bigDialogueController.IsBigBoxModeActive)
        {
            string command = dialogoNode.ContainsKey("Nombre") ? dialogoNode["Nombre"] : "";
            string fullDialogueText = dialogoNode.ContainsKey("Texto") ? dialogoNode["Texto"] : "";
            float lineDuration = 1f;
            if (dialogoNode.ContainsKey("Tiempo") && !string.IsNullOrEmpty(dialogoNode["Tiempo"]))
            {
                if (!float.TryParse(dialogoNode["Tiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out lineDuration)) lineDuration = 1f;
            }
            bigDialogueController.DisplayDialogue(command, fullDialogueText, lineDuration);
        }
        else if (dialogueManager != null)
        {
            string characterName = dialogoNode.ContainsKey("Nombre") ? dialogoNode["Nombre"] : "";
            string fullDialogueText = dialogoNode.ContainsKey("Texto") ? dialogoNode["Texto"] : "";
            float textSpeed = 0.05f;
            if (dialogoNode.ContainsKey("Velocidad") && !string.IsNullOrEmpty(dialogoNode["Velocidad"]))
            {
                if (!float.TryParse(dialogoNode["Velocidad"], NumberStyles.Any, CultureInfo.InvariantCulture, out textSpeed)) textSpeed = 0.05f;
            }
            bool esEventoEnTexto = tipoNodoActual.Equals("EventInText", System.StringComparison.OrdinalIgnoreCase);
            dialogueManager.ShowDialogue(characterName, fullDialogueText, textSpeed, esEventoEnTexto);
        }
    }

    private void ProcessGuionEventCommands(string tipoNodoActual)
    {
        if (tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase))
        {
            if (eventsController != null)
            {
                eventsController.TriggerEvent(currentNodeID, true);
                string eventoDefinidoEnEventsCSV = eventsController.GetCurrentEventTypeFromEventsCSV();
                bool autoFlagEvento = eventsController.GetCurrentEventAuto();
                bool inicialFlagEvento = eventsController.GetCurrentEventInicial();
                if (eventoDefinidoEnEventsCSV == "auto")
                {
                    autoAdvanceEnabledForCurrentNode = true;
                }
                else if (autoFlagEvento && inicialFlagEvento)
                {
                    AdvanceToNextNode();
                }
            }
            else
            {
                Debug.LogErrorFormat("GC: TipoNodo='Evento' pero EventsController no asignado! Nodo: {0}", currentNodeID);
            }
        }
    }

    //Tqm Gemini por los sprites :D
}