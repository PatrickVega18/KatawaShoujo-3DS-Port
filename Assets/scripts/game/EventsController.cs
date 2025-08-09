using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public enum VisualTargetType { None, ImageBoxUpper, ImageBoxLower }

public class PersistentEventInfo
{
    public VisualTargetType targetType;
    public Image targetImage;
    public string eventActionType;
    public Coroutine runningCoroutine;
    public string sfxPathToStop;
    public bool isForzado;
    public float targetAlphaOnCompletion;

    public PersistentEventInfo(Image img, VisualTargetType type, string action, Coroutine coroutine, string sfxPath, bool forzadoFlag, float finalAlpha)
    {
        targetType = type; targetImage = img; eventActionType = action;
        runningCoroutine = coroutine; sfxPathToStop = sfxPath;
        isForzado = forzadoFlag;
        targetAlphaOnCompletion = finalAlpha;
    }
}

public class EventsController : MonoBehaviour
{
    [Header("Referencias a Controladores")]
    public CSVLoader csvLoader;
    public SFXController sfxController;
    public BigDialogueController bigDialogueController;

    [Header("Referencias UI para Eventos Visuales")]
    public Image imageBoxUpper;
    public Image imageBoxLower;

    public Image allImageBoxUpper;
    public Image allImageBoxLower;

    public Image flashbackImage1;
    public Image flashbackImage2;

    [Header("Configuración Eventos Visuales")]
    public Color defaultBlackBoxColor = Color.black;
    public Color defaultWhiteBoxColor = Color.white;
    public Sprite heartAttackImage;

    // ===== Referencias para el evento "CentralText" =====
    [Header("Referencias para Evento 'CentralText'")]
    public List<RectTransform> centralTextMasks;
    public List<Text> centralTextUIElements;
    public float centralTextTargetWidth = 380f;

    [Header("Referencias para Evento 'SliderText'")]
    public Image sliderBoxImage;
    public List<Image> sliderImages;
    public SpriteListProvider spriteListProvider;

    [Header("Configuración Animación 'SliderText'")]
    public float sliderBoxFadeInDuration = 0.5f;
    public float sliderBoxFadeOutDuration = 0.5f;
    public float imageFadeDuration = 0.3f;

    [Header("Referencias para Evento 'PaneoTime'")]
    public List<Image> paneoImagesUpper;
    public List<Image> paneoImagesLower;
    private List<Vector3> paneoOriginalPositions = new List<Vector3>();
    
    private Coroutine activeSliderSequenceCoroutine = null;
    private Coroutine activeWordSpawnerCoroutine = null;
    public bool IsSliderTextActive { get { return activeSliderSequenceCoroutine != null; } }
    private List<Coroutine> activeWordFades = new List<Coroutine>();

    private Coroutine activeEventCoroutine = null;
    private Coroutine activeLowerEventCoroutine = null;
    private Coroutine activeOverallEventWaiterCoroutine = null;

    // Corrutinas para transiciones (que no se deben poder saltar)
    private Coroutine activeFlashbackInCoroutine = null;
    private Coroutine activeFlashbackOutCoroutine = null;

    private List<PersistentEventInfo> activePersistentEvents = new List<PersistentEventInfo>();

    private string currentEventType = null;
    private string currentEventSFXPath = null;
    private bool currentEventAuto = false;
    private bool currentEventInicial = false;
    private bool currentEventForzado = true;
    private float currentEventTiempo = 0f;
    private string currentEventLowerType = null;
    private string currentEventLowerTamano = null;
    private string currentEventLowerCoords = null;
    private bool waitForSFXCompletionForCurrentEvent = false;
    private bool currentEventEfectoEsperar = true;


    private float eventUpperTargetAlpha;
    private float eventLowerTargetAlpha;

    public bool IsEventoAnimando
    {
        get
        {
            return activeEventCoroutine != null ||
                   activeLowerEventCoroutine != null ||
                   activeOverallEventWaiterCoroutine != null ||
                   (sfxController != null && sfxController.IsPlayingAndWaiting && waitForSFXCompletionForCurrentEvent);
        }
    }
    public bool EventoAnimacionCompletada { get; private set; }

    public bool GetCurrentEventAuto() { return currentEventAuto; }
    public bool GetCurrentEventInicial() { return currentEventInicial; }
    public bool GetCurrentEventForzado() { return currentEventForzado; }
    public string GetCurrentEventTypeFromEventsCSV() { return currentEventType; }

    void Start()
    {
        if (csvLoader == null)
        {
            Debug.LogError("EC: CSVLoader no asignado.");
            this.enabled = false;
            return;
        }
        if (sfxController == null)
        {
            Debug.LogError("EC: SFXController no asignado.");
            this.enabled = false;
            return;
        }
        if (bigDialogueController == null)
        {
            Debug.LogWarning("EC: BigDialogueController no asignado. Los eventos 'bigboxsay' no funcionarán.");
        }
        if (imageBoxUpper != null)
        {
            imageBoxUpper.gameObject.SetActive(false);
            SetImageAlpha(imageBoxUpper, 0f);
        }
        else
        {
            Debug.LogWarning("EC: imageBoxUpper no asignado.");
        }
        if (imageBoxLower != null)
        {
            imageBoxLower.gameObject.SetActive(false);
            SetImageAlpha(imageBoxLower, 0f);
        }
        else
        {
            Debug.LogWarning("EC: imageBoxLower no asignado.");
        }
        if (allImageBoxUpper != null)
        {
            allImageBoxUpper.gameObject.SetActive(false);
            SetImageAlpha(allImageBoxUpper, 0f);
        }
        else
        {
            Debug.LogWarning("EC: allImageBoxUpper no asignado. Los eventos con prefijo 'all_' en la columna 'Evento' no funcionarán.");
        }
        if (allImageBoxLower != null)
        {
            allImageBoxLower.gameObject.SetActive(false);
            SetImageAlpha(allImageBoxLower, 0f);
        }
        else
        {
            Debug.LogWarning("EC: allImageBoxLower no asignado. Los eventos con prefijo 'all_' en la columna 'Lower' no funcionarán.");
        }

        if (flashbackImage1 != null)
        {
            flashbackImage1.gameObject.SetActive(false);
            SetImageAlpha(flashbackImage1, 0f);
        }
        else
        {
            Debug.LogWarning("EC: flashbackImage1 no asignado. La transición Flashback no funcionará.");
        }
        if (flashbackImage2 != null)
        {
            flashbackImage2.gameObject.SetActive(false);
            SetImageAlpha(flashbackImage2, 0f);
        }
        else
        {
            Debug.LogWarning("EC: flashbackImage2 no asignado. La transición Flashback no funcionará.");
        }
        if (sliderBoxImage != null)
        {
            sliderBoxImage.gameObject.SetActive(false);
        }
        if (sliderImages != null)
        {
            foreach (var img in sliderImages)
            {
                if (img != null)
                {
                    img.gameObject.SetActive(false);
                }
            }
        }
        if (paneoImagesUpper != null)
        {
            foreach (var img in paneoImagesUpper)
            {
                if (img != null)
                {
                    img.gameObject.SetActive(false);
                }
            }
        }
        if (paneoImagesLower != null)
        {
            foreach (var img in paneoImagesLower)
            {
                if (img != null)
                {
                    img.gameObject.SetActive(false);
                }
            }
        }
        HideCentralText();
        EventoAnimacionCompletada = true;
        //Usar If es mi pasión xdddd
    }


    public void TriggerEvent(string eventNodeID, bool isMasterEventTriggerIgnored)
    {
        if (activeOverallEventWaiterCoroutine != null)
        {
            StopCoroutine(activeOverallEventWaiterCoroutine);
            activeOverallEventWaiterCoroutine = null;
        }

        if (sfxController != null && !string.IsNullOrEmpty(currentEventSFXPath) && waitForSFXCompletionForCurrentEvent)
        {
            sfxController.StopCurrentSFX();
        }

        EventoAnimacionCompletada = false;
        currentEventType = ""; currentEventSFXPath = null; currentEventAuto = false;
        currentEventInicial = false; currentEventForzado = true; currentEventTiempo = 0f;
        currentEventLowerType = null; currentEventLowerTamano = null; currentEventLowerCoords = null;
        waitForSFXCompletionForCurrentEvent = false;
        eventUpperTargetAlpha = 0f; eventLowerTargetAlpha = 0f;
        currentEventEfectoEsperar = true;

        if (string.IsNullOrEmpty(eventNodeID))
        {
            FinalizeEvent();
            return;
        }
        Dictionary<string, string> eventData = csvLoader.GetEventEntryByID(eventNodeID);
        if (eventData == null)
        {
            Debug.LogError("EC: No datos para Evento ID: " + eventNodeID);
            FinalizeEvent();
            return;
        }
        currentEventType = eventData.ContainsKey("Evento") ? eventData["Evento"].ToLowerInvariant() : "";
        currentEventAuto = (eventData.ContainsKey("Auto") && eventData["Auto"] == "1");
        currentEventInicial = (eventData.ContainsKey("Inicial") && eventData["Inicial"] == "1");
        currentEventForzado = (!eventData.ContainsKey("Forzado") || eventData["Forzado"] != "0");
        string tiempoCsvStr = eventData.ContainsKey("Tiempo") ? eventData["Tiempo"] : "0";

        if (!float.TryParse(tiempoCsvStr, NumberStyles.Any, CultureInfo.InvariantCulture, out currentEventTiempo))
        {
            currentEventTiempo = IsEventTypeSFXOnlyOrAuto(currentEventType) ? 0f : 1.0f;
        }

        string tamanoUpperStr = eventData.ContainsKey("Tamano") ? eventData["Tamano"] : null;
        string coordsUpperStr = eventData.ContainsKey("Coordenadas") ? eventData["Coordenadas"] : null;
        currentEventLowerType = eventData.ContainsKey("Lower") ? eventData["Lower"].ToLowerInvariant() : null;
        currentEventLowerTamano = eventData.ContainsKey("Lower_Tamano") ? eventData["Lower_Tamano"] : null;
        currentEventLowerCoords = eventData.ContainsKey("Lower_Coordenadas") ? eventData["Lower_Coordenadas"] : null;
        currentEventSFXPath = eventData.ContainsKey("Efecto") ? eventData["Efecto"] : null;
        float efectoVolumen = 1.0f;
        if (eventData.ContainsKey("Efecto_Volumen") && !float.TryParse(eventData["Efecto_Volumen"], NumberStyles.Any, CultureInfo.InvariantCulture, out efectoVolumen))
        {
            efectoVolumen = 1.0f;
        }
        // Leer el nuevo flag "Efecto_Esperar"
        if (eventData.ContainsKey("Efecto_Esperar"))
        {
            currentEventEfectoEsperar = (eventData["Efecto_Esperar"] != "0");
        }
        else
        {
            currentEventEfectoEsperar = true; // Comportamiento por defecto si la columna no existe
        }

        // Calcular si debemos esperar al SFX
        if (string.IsNullOrEmpty(currentEventSFXPath) || !currentEventEfectoEsperar)
        {
            waitForSFXCompletionForCurrentEvent = false;
        }
        else
        {
            waitForSFXCompletionForCurrentEvent = !(currentEventAuto && currentEventInicial);
        }

        // Reproducir SFX si existe
        if (sfxController != null && !string.IsNullOrEmpty(currentEventSFXPath))
        {
            sfxController.PlaySFX(currentEventSFXPath, efectoVolumen, waitForSFXCompletionForCurrentEvent);
        }


        bool shouldNewEventBeTrackedAsPersistent;
        if (!currentEventForzado)
        {
            bool hasEffectiveDuration = currentEventTiempo > 0;
            if (currentEventType.StartsWith("heartattack-"))
            {
                hasEffectiveDuration = !string.IsNullOrEmpty(tiempoCsvStr) && tiempoCsvStr != "0";
            }
            shouldNewEventBeTrackedAsPersistent = (hasEffectiveDuration || waitForSFXCompletionForCurrentEvent);
        }
        else
        {
            shouldNewEventBeTrackedAsPersistent = (currentEventAuto && currentEventInicial);
        }

        // Detenemos visuales persistentes del evento anterior en el mismo objetivo
        VisualTargetType mainVisualTargetForNewEvent = GetVisualTargetTypeFromName(currentEventType);
        if (mainVisualTargetForNewEvent == VisualTargetType.ImageBoxUpper && imageBoxUpper != null)
        {
            StopPersistentVisualsOnTarget(VisualTargetType.ImageBoxUpper, imageBoxUpper, false);
        }
        if (IsVisualImageBoxEventType(currentEventLowerType) && imageBoxLower != null)
        {
            StopPersistentVisualsOnTarget(VisualTargetType.ImageBoxLower, imageBoxLower, false);
        }

        // ===== Nos aseguramos de ocultar el texto central si el nuevo evento NO es CentralText =====
        if (currentEventType != "centraltext")
        {
            HideCentralText();
        }

        bool mainVisualCoroutineWasStarted = false;
        if (!string.IsNullOrEmpty(currentEventType))
        {
            string eventTypeBase = currentEventType;

            // ===== La condición ahora ignora "bigboxsay" y "bigboxsayquit" =====
            bool isVisualUpperEvent = IsVisualImageBoxEventType(eventTypeBase) || eventTypeBase.StartsWith("heart") || eventTypeBase.StartsWith("allfade");
            bool isBigBoxEvent = eventTypeBase.StartsWith("bigbox");

            if (isVisualUpperEvent && !isBigBoxEvent)
            {
                if (activeEventCoroutine != null)
                {
                    StopCoroutine(activeEventCoroutine);
                    activeEventCoroutine = null;
                }
                if (!eventTypeBase.StartsWith("all")) StopPersistentVisualsOnTarget(VisualTargetType.ImageBoxUpper, imageBoxUpper, false);
            }

            float determinedTargetAlpha;
            if (currentEventType == "bigboxsay")
            {
                if (bigDialogueController != null)
                {
                    // =====Ya no pasamos el tiempo como parámetro =====
                    Coroutine boxFadeCoroutine = bigDialogueController.ShowBox();
                    activeOverallEventWaiterCoroutine = StartCoroutine(WaitForExternalCoroutine(boxFadeCoroutine));
                    mainVisualCoroutineWasStarted = true;
                }
                else
                {
                    Debug.LogError("EventsController: 'bigboxsay' llamado pero BigDialogueController no está asignado.");
                    FinalizeEvent();
                }
            }
            else if (currentEventType == "bigboxsayquit")
            {
                if (bigDialogueController != null)
                {
                    // ===== Ya no pasamos el tiempo como parámetro =====
                    Coroutine boxFadeCoroutine = bigDialogueController.HideBox();
                    activeOverallEventWaiterCoroutine = StartCoroutine(WaitForExternalCoroutine(boxFadeCoroutine));
                    mainVisualCoroutineWasStarted = true;
                }
                else
                {
                    Debug.LogError("EventsController: 'bigboxsayquit' llamado pero BigDialogueController no está asignado.");
                    FinalizeEvent();
                }
            }
            // ===== Lógica para el evento "CentralText" =====
            else if (eventTypeBase == "centraltext")
            {
                // Verificamos que tenemos los componentes necesarios
                if (centralTextMasks == null || centralTextUIElements == null || centralTextMasks.Count == 0 || centralTextUIElements.Count == 0)
                {
                    Debug.LogError("Evento 'CentralText' llamado, pero las referencias no están asignadas en el Inspector de EventsController.");
                    FinalizeEvent();
                    return;
                }

                // Buscamos la entrada de diálogo correspondiente
                Dictionary<string, string> dialogoData = csvLoader.GetDialogEntryByID(eventNodeID);
                if (dialogoData != null && dialogoData.ContainsKey("Texto"))
                {
                    string textoAMostrar = dialogoData["Texto"];
                    float tiempoDeAparicion = 1f; // Valor por defecto
                    if (dialogoData.ContainsKey("Tiempo"))
                    {
                        float.TryParse(dialogoData["Tiempo"], NumberStyles.Any, CultureInfo.InvariantCulture, out tiempoDeAparicion);
                    }

                    // Iniciamos la corrutina de animación
                    activeEventCoroutine = StartCoroutine(AnimateCentralText(textoAMostrar, tiempoDeAparicion));
                    mainVisualCoroutineWasStarted = true;
                }
                else
                {
                    Debug.LogError("Evento 'CentralText' no encontró texto para el ID: " + eventNodeID + " en Dialogo.csv");
                    FinalizeEvent();
                }
            }
            // --- INICIO DEL BLOQUE DE CAMBIOS ---
            else if (currentEventType == "flashback")
            {
                if (activeFlashbackInCoroutine != null) StopCoroutine(activeFlashbackInCoroutine);
                if (activeFlashbackOutCoroutine != null) StopCoroutine(activeFlashbackOutCoroutine);

                activeFlashbackInCoroutine = StartCoroutine(AnimateFlashbackIn(currentEventTiempo));

                Debug.Log("EventsController: Evento 'flashback' disparado. Finalizando inmediatamente para avanzar ID.");
                FinalizeEvent();
                return; // IMPORTANTE: Salimos para que la animación corra en segundo plano
            }

            else if (currentEventType == "flashbackquit")
            {
                if (activeFlashbackInCoroutine != null) StopCoroutine(activeFlashbackInCoroutine);
                if (activeFlashbackOutCoroutine != null) StopCoroutine(activeFlashbackOutCoroutine);

                activeFlashbackOutCoroutine = StartCoroutine(AnimateFlashbackOut(currentEventTiempo));

                Debug.Log("EventsController: Evento 'flashbackquit' disparado. Finalizando inmediatamente para avanzar ID.");
                FinalizeEvent();
                return; // Salimos, la animación correrá en segundo plano
            }
            else if (currentEventType.StartsWith("heartattack-"))
            {
                if (imageBoxUpper != null && heartAttackImage != null)
                {
                    string[] parts = currentEventType.Split('-');
                    float targetAlphaHeartAttack = 0.5f;
                    if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
                    {
                        if (!float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out targetAlphaHeartAttack))
                        {
                            targetAlphaHeartAttack = 0.5f;
                        }
                        targetAlphaHeartAttack = Mathf.Clamp01(targetAlphaHeartAttack);
                    }
                    string[] duracionesStr = tiempoCsvStr.Split('-');
                    float fadeInDuration = 0.5f, fadeOutDuration = 0.5f;
                    if (duracionesStr.Length == 2)
                    {
                        float.TryParse(duracionesStr[0], NumberStyles.Any, CultureInfo.InvariantCulture, out fadeInDuration);
                        float.TryParse(duracionesStr[1], NumberStyles.Any, CultureInfo.InvariantCulture, out fadeOutDuration);
                    }
                    eventUpperTargetAlpha = 0f;
                    activeEventCoroutine = StartCoroutine(AnimateHeartAttack(imageBoxUpper, heartAttackImage, targetAlphaHeartAttack, fadeInDuration, fadeOutDuration, tamanoUpperStr, coordsUpperStr, true));
                    if (shouldNewEventBeTrackedAsPersistent && activeEventCoroutine != null)
                    {
                        activePersistentEvents.Add(new PersistentEventInfo(imageBoxUpper, VisualTargetType.ImageBoxUpper, currentEventType, activeEventCoroutine, currentEventSFXPath, currentEventForzado, 0f));
                    }
                    mainVisualCoroutineWasStarted = true;
                }
            }
            // ===== Lógica para el evento persistente "heartStop" =====
            else if (currentEventType.StartsWith("heartstop-"))
            {
                if (imageBoxUpper != null && heartAttackImage != null)
                {
                    string[] parts = currentEventType.Split('-');
                    float targetAlpha = 1f;
                    if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
                    {
                        if (!float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out targetAlpha))
                        {
                            Debug.LogWarningFormat("EventsController: Error al parsear alfa para heartStop desde '{0}'. Usando 1.0 por defecto.", currentEventType);
                            targetAlpha = 1.0f;
                        }
                    }
                    targetAlpha = Mathf.Clamp01(targetAlpha);
                    eventUpperTargetAlpha = targetAlpha; // Guardamos el alfa final para el "skip"

                    imageBoxUpper.sprite = heartAttackImage;
                    imageBoxUpper.color = Color.white;

                    activeEventCoroutine = StartCoroutine(ExecuteVisualEventCoroutine(
                        imageBoxUpper,
                        currentEventType, // Pasamos el tipo de evento
                        currentEventTiempo, // Duración del fade-in
                        tamanoUpperStr,
                        coordsUpperStr,
                        true, // Es un evento "Upper"
                        shouldNewEventBeTrackedAsPersistent,
                        currentEventSFXPath,
                        targetAlpha // El alfa final que hemos parseado
                    ));

                    if (shouldNewEventBeTrackedAsPersistent && activeEventCoroutine != null)
                    {
                        activePersistentEvents.Add(new PersistentEventInfo(imageBoxUpper, VisualTargetType.ImageBoxUpper, currentEventType, activeEventCoroutine, currentEventSFXPath, currentEventForzado, targetAlpha));
                    }
                    mainVisualCoroutineWasStarted = true;
                }
            }
            // ===== Lógica para el evento de limpieza "imageBoxClean" =====
            else if (currentEventType == "imageboxclean")
            {
                if (imageBoxUpper != null)
                {
                    eventUpperTargetAlpha = 0f;

                    activeEventCoroutine = StartCoroutine(ExecuteVisualEventCoroutine(
                        imageBoxUpper,
                        "fadeout", // Usamos un tipo genérico para el fade-out
                        currentEventTiempo, // Duración del fade-out
                        null, null, // No necesitamos tamaño ni coordenadas para un fade-out
                        true,
                        shouldNewEventBeTrackedAsPersistent,
                        null,
                        0f // Alfa final es 0
                    ));
                    mainVisualCoroutineWasStarted = true;
                }
            }
            // ===== Lógica para el evento "SliderText" =====
            else if (eventTypeBase == "slidertext")
            {
                if (activeSliderSequenceCoroutine != null)
                {
                    Debug.LogWarning("SliderText ya está activo. Ignorando nuevo evento.");
                }
                else
                {
                    // Iniciamos la corrutina principal para el efecto
                    activeSliderSequenceCoroutine = StartCoroutine(AnimateSliderText(
                        currentEventTiempo, // Duración del slide
                        tamanoUpperStr,
                        coordsUpperStr
                    ));
                }
            }
            // ===== Lógica para el evento "SliderTextQuit" =====
            else if (eventTypeBase == "slidertextquit")
            {
                if (activeSliderSequenceCoroutine != null)
                {
                    activeSliderSequenceCoroutine = StartCoroutine(AnimateSliderTextQuit(sliderBoxFadeOutDuration)); // <-- Asignación correcta
                    mainVisualCoroutineWasStarted = true;
                }
                else
                {
                    FinalizeEvent();
                }
            }
            // Lógica para el evento "PaneoTimeIn" (Apertura)
            else if (eventTypeBase == "paneotimein")
            {
                if (paneoImagesUpper == null || paneoImagesUpper.Count == 0 || paneoImagesLower == null || paneoImagesLower.Count == 0)
                {
                    Debug.LogError("Evento 'PaneoTimeIn' llamado, pero las listas de imágenes no están asignadas.");
                    FinalizeEvent();
                    return; 
                }
                // Iniciamos la corrutina de apertura
                activeEventCoroutine = StartCoroutine(AnimatePaneoTimeIn(currentEventTiempo));
                mainVisualCoroutineWasStarted = true;
            }

            // Lógica para el evento "PaneoTimeOut" (Cierre)
            else if (eventTypeBase == "paneotimeout")
            {
                if (paneoImagesUpper == null || paneoImagesUpper.Count == 0 || paneoImagesLower == null || paneoImagesLower.Count == 0)
                {
                    Debug.LogError("Evento 'PaneoTimeOut' llamado, pero las listas de imágenes no están asignadas.");
                    FinalizeEvent();
                    return; 
                }
                // Iniciamos la corrutina de cierre
                activeEventCoroutine = StartCoroutine(AnimatePaneoTimeOut(currentEventTiempo));
                mainVisualCoroutineWasStarted = true;
            }
            else if (eventTypeBase == "sfxloop")
            {
                if (sfxController != null && !string.IsNullOrEmpty(currentEventSFXPath))
                {
                    if (eventData.ContainsKey("Efecto_Volumen") && !float.TryParse(eventData["Efecto_Volumen"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out efectoVolumen))
                    {
                        efectoVolumen = 1.0f;
                    }
                    sfxController.PlayLoopingSFX(currentEventSFXPath, efectoVolumen, currentEventTiempo);
                }
                FinalizeEvent(); // El fadeIn del audio no detiene el flujo del juego
                return;
            }
            else if (eventTypeBase == "sfxout")
            {
                if (sfxController != null)
                {
                    sfxController.StopLoopingSFX(currentEventTiempo);
                }
                FinalizeEvent(); // El fadeOut del audio no detiene el flujo del juego
                return;
            }
            else
            {
                switch (eventTypeBase)
                {
                    case "wait":
                        currentEventAuto = true;
                        currentEventInicial = false;
                        currentEventForzado = false;
                        Debug.Log("Evento 'wait' iniciado. Esperando " + currentEventTiempo + " segundos.");
                        break;
                    case "auto": case "sfx": break;
                    case "fadeinblackbox":
                    case "fadeoutblackbox":
                    case "fadeinwhitebox":
                    case "fadeoutwhitebox":
                        if (imageBoxUpper != null)
                        {
                            determinedTargetAlpha = eventTypeBase.Contains("fadein") ? 1f : 0f;
                            eventUpperTargetAlpha = determinedTargetAlpha;
                            activeEventCoroutine = StartCoroutine(ExecuteVisualEventCoroutine(imageBoxUpper, eventTypeBase, currentEventTiempo, tamanoUpperStr, coordsUpperStr, true, shouldNewEventBeTrackedAsPersistent, currentEventSFXPath, determinedTargetAlpha));
                            mainVisualCoroutineWasStarted = true;
                        }
                        else { Debug.LogWarningFormat("EC: Evento '{0}' necesita imageBoxUpper.", currentEventType); }
                        break;

                    case "allfadeinblackbox":
                    case "allfadeoutblackbox":
                    case "allfadeinwhitebox":
                    case "allfadeoutwhitebox":
                        if (allImageBoxUpper != null)
                        {
                            determinedTargetAlpha = eventTypeBase.Contains("fadein") ? 1f : 0f;
                            eventUpperTargetAlpha = determinedTargetAlpha;
                            activeEventCoroutine = StartCoroutine(ExecuteVisualEventCoroutine(allImageBoxUpper, eventTypeBase, currentEventTiempo, tamanoUpperStr, coordsUpperStr, true, shouldNewEventBeTrackedAsPersistent, currentEventSFXPath, determinedTargetAlpha));
                            mainVisualCoroutineWasStarted = true;
                        }
                        else { Debug.LogWarningFormat("EC: Evento '{0}' necesita allImageBoxUpper.", currentEventType); }
                        break;

                    default:
                        if (IsVisualImageBoxEventType(currentEventType))
                        {
                            if (imageBoxUpper != null)
                            {
                                determinedTargetAlpha = currentEventType.Contains("fadein") ? 1f : (currentEventType.Contains("fadeout") ? 0f : 1f);
                                eventUpperTargetAlpha = determinedTargetAlpha;
                                activeEventCoroutine = StartCoroutine(ExecuteVisualEventCoroutine(imageBoxUpper, currentEventType, currentEventTiempo, tamanoUpperStr, coordsUpperStr, true, shouldNewEventBeTrackedAsPersistent, currentEventSFXPath, determinedTargetAlpha));
                                if (shouldNewEventBeTrackedAsPersistent && activeEventCoroutine != null)
                                {
                                    activePersistentEvents.Add(new PersistentEventInfo(imageBoxUpper, VisualTargetType.ImageBoxUpper, currentEventType, activeEventCoroutine, currentEventSFXPath, currentEventForzado, determinedTargetAlpha));
                                }
                                mainVisualCoroutineWasStarted = true;
                            }
                            else { Debug.LogWarningFormat("EC: Evento visual '{0}' necesita imageBoxUpper.", currentEventType); }
                        }
                        else { Debug.LogWarningFormat("EC: Tipo evento principal desconocido '{0}' ID: {1}", currentEventType, eventNodeID); }
                        break;
                }
            }
        }

        // --- Lógica para el evento Secundario (Lower) ---
        bool lowerVisualCoroutineWasStarted = false;
        if (!string.IsNullOrEmpty(currentEventLowerType))
        {
            string lowerEventTypeBase = currentEventLowerType;

            if (IsVisualImageBoxEventType(lowerEventTypeBase) || lowerEventTypeBase.StartsWith("allfade"))
            {
                if (activeLowerEventCoroutine != null)
                {
                    StopCoroutine(activeLowerEventCoroutine);
                    activeLowerEventCoroutine = null;
                }
                StopPersistentVisualsOnTarget(VisualTargetType.ImageBoxLower, imageBoxLower, false);
                // NOTA: No tenemos persistencia para allImageBoxLower.
            }

            if (lowerEventTypeBase.StartsWith("allfade"))
            {
                if (allImageBoxLower != null)
                {
                    bool isLowerPersistent = shouldNewEventBeTrackedAsPersistent;
                    float determinedLowerTargetAlpha = lowerEventTypeBase.Contains("fadein") ? 1f : 0f;
                    eventLowerTargetAlpha = determinedLowerTargetAlpha;
                    activeLowerEventCoroutine = StartCoroutine(ExecuteVisualEventCoroutine(allImageBoxLower, lowerEventTypeBase, currentEventTiempo, currentEventLowerTamano, currentEventLowerCoords, false, isLowerPersistent, null, determinedLowerTargetAlpha));
                    lowerVisualCoroutineWasStarted = true;
                }
                else { Debug.LogWarningFormat("EC: Evento '{0}' en columna Lower necesita que allImageBoxLower esté asignado.", currentEventLowerType); }
            }
            else if (lowerEventTypeBase == "imageboxclean")
            {
                if (imageBoxLower != null)
                {
                    activeLowerEventCoroutine = StartCoroutine(ExecuteVisualEventCoroutine(imageBoxLower, "fadeout", currentEventTiempo, null, null, false, false, null, 0f));
                    lowerVisualCoroutineWasStarted = true;
                }
            }
            else if (IsVisualImageBoxEventType(lowerEventTypeBase))
            {
                if (imageBoxLower != null)
                {
                    bool isLowerPersistent = shouldNewEventBeTrackedAsPersistent;
                    float determinedLowerTargetAlpha = lowerEventTypeBase.Contains("fadein") ? 1f : (lowerEventTypeBase.Contains("fadeout") ? 0f : 1f);
                    eventLowerTargetAlpha = determinedLowerTargetAlpha;
                    activeLowerEventCoroutine = StartCoroutine(ExecuteVisualEventCoroutine(imageBoxLower, lowerEventTypeBase, currentEventTiempo, currentEventLowerTamano, currentEventLowerCoords, false, isLowerPersistent, null, determinedLowerTargetAlpha));
                    if (isLowerPersistent && activeLowerEventCoroutine != null)
                    {
                        activePersistentEvents.Add(new PersistentEventInfo(imageBoxLower, VisualTargetType.ImageBoxLower, lowerEventTypeBase, activeLowerEventCoroutine, null, currentEventForzado, determinedLowerTargetAlpha));
                    }
                    lowerVisualCoroutineWasStarted = true;
                }
            }
        }

        if (currentEventAuto && currentEventInicial)
        {
            FinalizeEvent();
            return; // Salimos del método; GameController se encargará del resto.
        }
        bool isWaitEvent = currentEventType == "wait";
        bool needsToWait = ((mainVisualCoroutineWasStarted || lowerVisualCoroutineWasStarted) && currentEventTiempo > 0)
                        || waitForSFXCompletionForCurrentEvent
                        || (isWaitEvent && currentEventTiempo > 0);

        if (needsToWait)
        {
            activeOverallEventWaiterCoroutine = StartCoroutine(OverallEventWaiterCoroutine(mainVisualCoroutineWasStarted || lowerVisualCoroutineWasStarted));
        }
        else
        {

            FinalizeEvent();
        }
    }

    private IEnumerator ExecuteVisualEventCoroutine(Image targetImage, string eventActionType, float duration,
                                                    string tamanoStr, string coordsStr, bool isUpperEvent,
                                                    bool isActuallyPersistent, string associatedSfxPath,
                                                    float finalAlphaValueToReach)
    {
        Coroutine thisSpecificCoroutineInstance = isUpperEvent ? activeEventCoroutine : activeLowerEventCoroutine;

        if (targetImage == null)
        {
            if (isUpperEvent) activeEventCoroutine = null; else activeLowerEventCoroutine = null;
            CheckAndFinalizeEventAfterCoroutine();
            yield break;
        }

        if (IsVisualImageBoxEventType(eventActionType) && !eventActionType.StartsWith("heartstop-"))
        {
            targetImage.sprite = null;
        }

        if (eventActionType.Contains("blackbox"))
        {
            targetImage.color = defaultBlackBoxColor;
        }
        else if (eventActionType.Contains("whitebox"))
        {
            targetImage.color = defaultWhiteBoxColor;
        }

        if (eventActionType.Contains("fadein") || eventActionType.StartsWith("heartstop-"))
        {
            SetImageAlpha(targetImage, 0f);
        }

        if (!string.IsNullOrEmpty(tamanoStr))
            targetImage.rectTransform.sizeDelta = ParseTamano(tamanoStr, targetImage.rectTransform.sizeDelta);

        if (!string.IsNullOrEmpty(coordsStr))
            targetImage.rectTransform.anchoredPosition3D = ParseCoordenadas(coordsStr, targetImage.rectTransform.anchoredPosition3D);

        targetImage.gameObject.SetActive(true);

        if (isUpperEvent) eventUpperTargetAlpha = finalAlphaValueToReach; else eventLowerTargetAlpha = finalAlphaValueToReach;

        if (duration > 0)
        {
            float elapsedTime = 0f;
            float actualStartAlphaForLerp = targetImage.color.a;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                SetImageAlpha(targetImage, Mathf.Lerp(actualStartAlphaForLerp, finalAlphaValueToReach, elapsedTime / duration));
                yield return null;
            }
        }

        SetImageAlpha(targetImage, finalAlphaValueToReach);
        if (finalAlphaValueToReach == 0f)
        {
            targetImage.gameObject.SetActive(false);
        }
        else
        {
            targetImage.gameObject.SetActive(true);
        }

        if (isActuallyPersistent && thisSpecificCoroutineInstance != null)
        {
            activePersistentEvents.RemoveAll(pInfo => pInfo.runningCoroutine == thisSpecificCoroutineInstance && pInfo.targetImage == targetImage);
        }

        if (isUpperEvent)
        {
            activeEventCoroutine = null;
        }
        else
        {
            activeLowerEventCoroutine = null;
        }

        CheckAndFinalizeEventAfterCoroutine();
        yield break;
    }

    private IEnumerator OverallEventWaiterCoroutine(bool visualCoroutinesWereActiveForThisEvent = false)
    {
        Debug.LogWarning(string.Format(
            "[[OVR_WAITER]] Evento '{0}': Iniciado. visualCoroutinesWereActive={1}",
            currentEventType, visualCoroutinesWereActiveForThisEvent
        ));

        if (visualCoroutinesWereActiveForThisEvent)
        {
            Debug.LogWarning(string.Format(
                "[[OVR_WAITER]] Evento '{0}': Esperando visuales. activeEventCoroutine is {1}, activeLowerEventCoroutine is {2}",
                currentEventType,
                (activeEventCoroutine == null ? "NULL" : "ACTIVO"),
                (activeLowerEventCoroutine == null ? "NULL" : "ACTIVO")
            ));
            while (activeEventCoroutine != null || activeLowerEventCoroutine != null)
            {
                yield return null;
            }
        }

        // Lógica para esperar currentEventTiempo (considerando la corrección del "doble tiempo")
        if (!visualCoroutinesWereActiveForThisEvent && currentEventTiempo > 0)
        {
            float waitedTime = 0;
            while (waitedTime < currentEventTiempo)
            {
                waitedTime += Time.deltaTime;
                yield return null;
            }
        }

        if (waitForSFXCompletionForCurrentEvent && sfxController != null)
        {

            while (sfxController.IsPlayingAndWaiting)
            {
                yield return null;
            }
        }
        // Capturamos la referencia de esta instancia específica de la corrutina.
        Coroutine thisSpecificInstance = activeOverallEventWaiterCoroutine;
        if (activeOverallEventWaiterCoroutine == thisSpecificInstance)
        {
            activeOverallEventWaiterCoroutine = null;
            FinalizeEvent();
        }
    }

    private void CheckAndFinalizeEventAfterCoroutine()
    {
        // Este log es para ver cuándo se llama esta función y con qué estado
        Debug.LogWarning(string.Format(
            "[[CHK_FINALIZE]] Evento '{0}': Verificando. activeVisUpper={1}, activeVisLower={2}, activeOWC={3}, waitSFX={4}, sfxPlayingAndWaiting={5}, eventTiempo={6}",
            currentEventType,
            (activeEventCoroutine == null ? "NULL" : "ACTIVO"),
            (activeLowerEventCoroutine == null ? "NULL" : "ACTIVO"),
            (activeOverallEventWaiterCoroutine == null ? "NULL" : "ACTIVO"),
            waitForSFXCompletionForCurrentEvent,
            (sfxController != null && sfxController.IsPlayingAndWaiting && waitForSFXCompletionForCurrentEvent), // Esta parte es un poco redundante con waitSFX pero da info
            currentEventTiempo
        ));

        if (activeEventCoroutine == null && activeLowerEventCoroutine == null) // Corrutinas visuales principales terminadas (o aún no iniciadas para el evento actual)
        {
            if (activeOverallEventWaiterCoroutine != null) // Si hay un OverallEventWaiterCoroutine YA ACTIVO para el evento actual, él se encarga.
            {
                Debug.LogWarning(string.Format("[[CHK_FINALIZE]] Evento '{0}': OverallEventWaiter está ACTIVO. Dejamos que él finalice.", currentEventType));
                return;
            }

            bool sfxEstaEsperando = (waitForSFXCompletionForCurrentEvent && sfxController != null && sfxController.IsPlayingAndWaiting);

            // Determinamos si el evento actual tiene una duración visual que justificaría un OverallEventWaiter
            bool tieneDuracionVisualDefinida = false;
            if (currentEventType.StartsWith("heartattack-"))
            {
                tieneDuracionVisualDefinida = true;
            }
            else if (IsVisualImageBoxEventType(currentEventType))
            {
                // Para otros eventos visuales (como fades), su duración es currentEventTiempo.
                if (currentEventTiempo > 0)
                {
                    tieneDuracionVisualDefinida = true;
                }
            }

            if (sfxEstaEsperando)
            {
                Debug.LogWarning(string.Format("[[CHK_FINALIZE]] Evento '{0}': No hay OWC, pero SFX está pendiente. TriggerEvent debería iniciar OWC.", currentEventType));
            }
            else if (tieneDuracionVisualDefinida && activeOverallEventWaiterCoroutine == null)
            {
                Debug.LogWarning(string.Format("[[CHK_FINALIZE]] Evento '{0}': No hay OWC, no se espera SFX, PERO tiene duración visual definida ({1}s / heartAttack). TriggerEvent se encargará.", currentEventType, currentEventTiempo));
            }
            else
            {
                Debug.LogWarning(string.Format("[[CHK_FINALIZE]] Evento '{0}': No hay OWC, no se espera SFX, y no parece tener duración visual pendiente. Llamando a FinalizeEvent.", currentEventType));
                FinalizeEvent();
            }
        }
        else
        {
            Debug.LogWarning(string.Format("[[CHK_FINALIZE]] Evento '{0}': Alguna corrutina visual (Upper o Lower) sigue activa. No se finaliza aún.", currentEventType));
        }
    }

    private void FinalizeEvent()
    {
        EventoAnimacionCompletada = true;
        activeEventCoroutine = null;
        activeLowerEventCoroutine = null;
        activeOverallEventWaiterCoroutine = null;
    }

    public void CompleteCurrentEvent()
    {
        if (currentEventType == "bigboxsay" || currentEventType == "bigboxsayquit")
        {
            if (currentEventForzado && bigDialogueController != null)
            {
                Debug.Log("CompleteCurrentEvent: Saltando animación de BigBoxSay.");
                bigDialogueController.CompleteBoxFade(); // Llamamos a un nuevo método para completar el fade
            }
            if (activeOverallEventWaiterCoroutine != null)
            {
                StopCoroutine(activeOverallEventWaiterCoroutine);
            }
            FinalizeEvent(); // Finalizamos el evento
            return; // Salimos para no ejecutar la lógica de salto general
        }

        if (currentEventType == "flashback" || currentEventType == "flashbackquit")
        {
            Debug.Log("CompleteCurrentEvent: Se intentó saltar un evento 'flashback' o 'flashbackquit', lo cual no es permitido.");
            return;
        }

        if (!currentEventForzado) { return; }
        if (EventoAnimacionCompletada && activeOverallEventWaiterCoroutine == null && activeEventCoroutine == null && activeLowerEventCoroutine == null) { return; }
        if (sfxController != null && !string.IsNullOrEmpty(currentEventSFXPath)) { sfxController.StopCurrentSFX(); }
        if (activeEventCoroutine != null) { StopCoroutine(activeEventCoroutine); }
        if (activeLowerEventCoroutine != null) { StopCoroutine(activeLowerEventCoroutine); }
        if (activeOverallEventWaiterCoroutine != null) { StopCoroutine(activeOverallEventWaiterCoroutine); }
        if (imageBoxUpper != null && IsVisualImageBoxEventType(currentEventType))
        {
            SetImageAlpha(imageBoxUpper, eventUpperTargetAlpha); imageBoxUpper.gameObject.SetActive(eventUpperTargetAlpha > 0f);
        }
        if (imageBoxLower != null && IsVisualImageBoxEventType(currentEventLowerType))
        {
            SetImageAlpha(imageBoxLower, eventLowerTargetAlpha); imageBoxLower.gameObject.SetActive(eventLowerTargetAlpha > 0f);
        }
        FinalizeEvent(); // Esto pondrá EventoAnimacionCompletada=true y anulará las corrutinas miembro
    }

    public void CompleteAllForzablePersistentEvents()
    {
        if (activePersistentEvents.Count == 0) return;
        List<PersistentEventInfo> toProcess = new List<PersistentEventInfo>(activePersistentEvents);
        bool anyPersistentActuallyCompleted = false;
        foreach (PersistentEventInfo pEvent in toProcess)
        {
            if (pEvent.isForzado)
            {
                anyPersistentActuallyCompleted = true;
                if (pEvent.targetType == VisualTargetType.ImageBoxUpper || pEvent.targetType == VisualTargetType.ImageBoxLower)
                {
                    if (pEvent.runningCoroutine != null) StopCoroutine(pEvent.runningCoroutine);
                    if (pEvent.targetImage != null)
                    {
                        SetImageAlpha(pEvent.targetImage, pEvent.targetAlphaOnCompletion);
                        pEvent.targetImage.gameObject.SetActive(pEvent.targetAlphaOnCompletion > 0f);
                    }
                }
                if (sfxController != null && !string.IsNullOrEmpty(pEvent.sfxPathToStop)) { sfxController.StopCurrentSFX(); }
                activePersistentEvents.Remove(pEvent);
            }
        }
        if (anyPersistentActuallyCompleted) CheckAndFinalizeEventAfterCoroutine();
    }

    //Que Gemini se encargue xd:
    public void FinalizarTodosLosEventosPersistentesVisuales()
    {
        List<PersistentEventInfo> toStop = new List<PersistentEventInfo>(activePersistentEvents);
        activePersistentEvents.Clear();
        foreach (PersistentEventInfo pEvent in toStop)
        {
            if (pEvent.targetType == VisualTargetType.ImageBoxUpper || pEvent.targetType == VisualTargetType.ImageBoxLower)
            {
                if (pEvent.runningCoroutine != null) StopCoroutine(pEvent.runningCoroutine);
                if (pEvent.targetImage != null) { SetImageAlpha(pEvent.targetImage, 0f); pEvent.targetImage.gameObject.SetActive(false); }
            }
            if (sfxController != null && !string.IsNullOrEmpty(pEvent.sfxPathToStop)) { sfxController.StopCurrentSFX(); }
        }
        CheckAndFinalizeEventAfterCoroutine();
    }

    public void StopPersistentVisualsOnTarget(VisualTargetType targetToClear, Image specificImage = null, bool alsoStopAssociatedSfx = false)
    {
        if (targetToClear != VisualTargetType.ImageBoxUpper && targetToClear != VisualTargetType.ImageBoxLower) return;
        if (activePersistentEvents.Count == 0) return;
        List<PersistentEventInfo> toRemove = new List<PersistentEventInfo>();
        foreach (PersistentEventInfo pEvent in activePersistentEvents)
        {
            if (pEvent.targetType == targetToClear && pEvent.targetImage == specificImage) { toRemove.Add(pEvent); }
        }
        foreach (PersistentEventInfo pEvent in toRemove)
        {
            if (pEvent.runningCoroutine != null) StopCoroutine(pEvent.runningCoroutine);
            if (pEvent.targetImage != null) { SetImageAlpha(pEvent.targetImage, 0f); pEvent.targetImage.gameObject.SetActive(false); }
            if (alsoStopAssociatedSfx && sfxController != null && !string.IsNullOrEmpty(pEvent.sfxPathToStop)) { sfxController.StopCurrentSFX(); }
            activePersistentEvents.Remove(pEvent);
        }
        if (toRemove.Count > 0) CheckAndFinalizeEventAfterCoroutine();
    }
    private IEnumerator AnimateHeartAttack(Image targetImage, Sprite imageToShow, float targetAlphaIn,
                                           float fadeInDuration, float fadeOutDuration,
                                           string tamanoCsv, string coordsCsv, bool isUpperBox)
    {
        if (targetImage == null || imageToShow == null)
        {
            Debug.LogError("EventsController.AnimateHeartAttack: targetImage o imageToShow es null.");
            if (isUpperBox) activeEventCoroutine = null; else activeLowerEventCoroutine = null;
            CheckAndFinalizeEventAfterCoroutine();
            yield break;
        }

        targetImage.sprite = imageToShow;
        targetImage.rectTransform.sizeDelta = ParseTamano(tamanoCsv, new Vector2(400, 240));
        targetImage.rectTransform.anchoredPosition3D = ParseCoordenadas(coordsCsv, Vector3.zero);
        SetImageAlpha(targetImage, 0f);
        targetImage.gameObject.SetActive(true);

        float elapsedTime = 0f;
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(0f, targetAlphaIn, elapsedTime / fadeInDuration);
            SetImageAlpha(targetImage, newAlpha);
            yield return null;
        }
        SetImageAlpha(targetImage, targetAlphaIn);

        elapsedTime = 0f;
        float startAlphaFadeOut = targetImage.color.a;

        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlphaFadeOut, 0f, elapsedTime / fadeOutDuration);
            SetImageAlpha(targetImage, newAlpha);
            yield return null;
        }
        SetImageAlpha(targetImage, 0f);
        targetImage.gameObject.SetActive(false);

        if (isUpperBox)
        {
            activeEventCoroutine = null;
        }
        else
        {
            activeLowerEventCoroutine = null;
        }

        CheckAndFinalizeEventAfterCoroutine();
    }

    // Seguimos =w=
    private IEnumerator AnimateFlashbackIn(float duration)
    {
        if (flashbackImage1 == null || flashbackImage2 == null)
        {
            Debug.LogError("AnimateFlashbackIn: Las imágenes de flashback no están asignadas en el Inspector.");
            activeFlashbackInCoroutine = null;
            yield break;
        }

        // Preparamos las imágenes activando sus GameObjects y poniendo su alfa a 0
        flashbackImage1.gameObject.SetActive(true);
        flashbackImage2.gameObject.SetActive(true);
        SetImageAlpha(flashbackImage1, 0f);
        SetImageAlpha(flashbackImage2, 0f);

        float elapsedTime = 0f;
        Coroutine flashback2FadeCoroutine = null;
        float safeDuration = Mathf.Max(0.01f, duration); // Evitamos división por cero

        Debug.Log(string.Format("AnimateFlashbackIn: Iniciando fade-in para Flashback1 (duración: {0}s).", safeDuration));

        // Bucle para el fade-in de la primera imagen (Flashback1)
        while (elapsedTime < safeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha1 = Mathf.Lerp(0f, 1f, elapsedTime / safeDuration);
            SetImageAlpha(flashbackImage1, newAlpha1);

            if (newAlpha1 >= 0.2f && flashback2FadeCoroutine == null)
            {
                Debug.Log("AnimateFlashbackIn: Alfa de Flashback1 >= 0.3. Iniciando fade-in para Flashback2.");
                flashback2FadeCoroutine = StartCoroutine(AnimateSingleImageFade(flashbackImage2, 1f, safeDuration));
            }

            yield return null;
        }

        // Aseguramos el estado final de la primera imagen al terminar el bucle
        SetImageAlpha(flashbackImage1, 1f);
        Debug.Log("AnimateFlashbackIn: Fade-in de Flashback1 completado.");

        activeFlashbackInCoroutine = null;
    }
    // ===== CORRUTINA PARA FLASHBACKQUIT =====
    private IEnumerator AnimateFlashbackOut(float duration)
    {
        if (flashbackImage1 == null || flashbackImage2 == null)
        {
            Debug.LogError("AnimateFlashbackOut: Las imágenes de flashback no están asignadas en el Inspector.");
            activeFlashbackOutCoroutine = null;
            yield break;
        }

        float safeDuration = Mathf.Max(0.01f, duration);
        Debug.Log(string.Format("AnimateFlashbackOut: Iniciando fade-out simultáneo para Flashback1 y 2 (duración: {0}s).", safeDuration));

        // Iniciamos ambos fades al mismo tiempo
        Coroutine fade1 = StartCoroutine(AnimateSingleImageFade(flashbackImage1, 0f, safeDuration));
        Coroutine fade2 = StartCoroutine(AnimateSingleImageFade(flashbackImage2, 0f, safeDuration));

        // Esperamos a que ambos terminen
        if (fade1 != null) yield return fade1;
        if (fade2 != null) yield return fade2;

        // Una vez terminados los fades, desactivamos los GameObjects
        flashbackImage1.gameObject.SetActive(false);
        flashbackImage2.gameObject.SetActive(false);

        Debug.Log("AnimateFlashbackOut: Fade-out de imágenes de flashback completado.");
        activeFlashbackOutCoroutine = null;
    }
    private IEnumerator AnimateSingleImageFade(Image targetImage, float targetAlpha, float duration)
    {
        if (targetImage == null) yield break;

        float startAlpha = targetImage.color.a;
        float elapsedTime = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsedTime < safeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / safeDuration);
            SetImageAlpha(targetImage, newAlpha);
            yield return null;
        }

        // Aseguramos el valor final
        SetImageAlpha(targetImage, targetAlpha);
    }
    
    private IEnumerator AnimatePaneoTimeIn(float openDuration)
    {
        Debug.Log("Iniciando PaneoTimeIn con duración de apertura: " + openDuration + "s");

        if (paneoImagesUpper.Count == 0 || paneoImagesLower.Count == 0) yield break;
        
        paneoOriginalPositions.Clear();
        foreach (var img in paneoImagesUpper) { if (img != null) paneoOriginalPositions.Add(img.rectTransform.anchoredPosition3D); }
        foreach (var img in paneoImagesLower) { if (img != null) paneoOriginalPositions.Add(img.rectTransform.anchoredPosition3D); }
        
        int maxImagesInGroup = Mathf.Max(paneoImagesUpper.Count, paneoImagesLower.Count);
        float cascadeDelayTrigger = 0.2f;
        float singleAnimationDuration = openDuration / (1f + (maxImagesInGroup - 1) * cascadeDelayTrigger);
        float delayBetweenImages = singleAnimationDuration * cascadeDelayTrigger;

        // --- FASE DE APERTURA EN PARALELO ---
        for (int i = 0; i < maxImagesInGroup; i++)
        {
            // --- Procesar imagen SUPERIOR (si existe en este índice) ---
            if (i < paneoImagesUpper.Count && paneoImagesUpper[i] != null)
            {
                Image imgUpper = paneoImagesUpper[i];
                RectTransform rtUpper = imgUpper.rectTransform;
                rtUpper.pivot = new Vector2(0, 0.5f);
                rtUpper.sizeDelta = new Vector2(0, rtUpper.sizeDelta.y);
                float widthUpper = 100f;
                
                if(i < paneoOriginalPositions.Count) // Usamos la primera parte de la lista de posiciones
                {
                    float newX = paneoOriginalPositions[i].x - (widthUpper / 2f);
                    rtUpper.anchoredPosition3D = new Vector3(newX, paneoOriginalPositions[i].y, paneoOriginalPositions[i].z);
                }

                imgUpper.gameObject.SetActive(true);
                StartCoroutine(AnimateSinglePaneoImage(imgUpper, 0, widthUpper, singleAnimationDuration));
            }

            // --- Procesar imagen INFERIOR (si existe en este índice) ---
            if (i < paneoImagesLower.Count && paneoImagesLower[i] != null)
            {
                Image imgLower = paneoImagesLower[i];
                RectTransform rtLower = imgLower.rectTransform;
                rtLower.pivot = new Vector2(0, 0.5f);
                rtLower.sizeDelta = new Vector2(0, rtLower.sizeDelta.y);
                float widthLower = 80f;

                // El índice para las posiciones originales de la pantalla inferior es paneoImagesUpper.Count + i
                int posIndex = paneoImagesUpper.Count + i;
                if(posIndex < paneoOriginalPositions.Count)
                {
                    float newX = paneoOriginalPositions[posIndex].x - (widthLower / 2f);
                    rtLower.anchoredPosition3D = new Vector3(newX, paneoOriginalPositions[posIndex].y, paneoOriginalPositions[posIndex].z);
                }

                imgLower.gameObject.SetActive(true);
                StartCoroutine(AnimateSinglePaneoImage(imgLower, 0, widthLower, singleAnimationDuration));
            }

            // --- Esperamos el retraso antes de la siguiente pareja de imágenes ---
            if (i < maxImagesInGroup - 1)
            {
                yield return new WaitForSeconds(delayBetweenImages);
            }
        }
        
        // Esperamos a que la última animación termine
        yield return new WaitForSeconds(singleAnimationDuration);
        
        Debug.Log("PaneoTimeIn: Fase de apertura completada.");
        FinalizeEvent();
    }
    private IEnumerator AnimatePaneoTimeOut(float closeDuration)
    {
        Debug.Log("Iniciando PaneoTimeOut con duración de cierre: " + closeDuration + "s");

        if (paneoImagesUpper.Count == 0 || paneoImagesLower.Count == 0) yield break;

        // --- CÁLCULO DE TIEMPOS ---
        int maxImagesInGroup = Mathf.Max(paneoImagesUpper.Count, paneoImagesLower.Count);
        float cascadeDelayTrigger = 0.2f;
        float singleAnimationDuration = closeDuration / (1f + (maxImagesInGroup - 1) * cascadeDelayTrigger);
        float delayBetweenImages = singleAnimationDuration * cascadeDelayTrigger;

        // --- FASE DE CIERRE EN PARALELO ---
        for (int i = 0; i < maxImagesInGroup; i++)
        {
            // --- Procesar imagen SUPERIOR (si existe) ---
            if (i < paneoImagesUpper.Count && paneoImagesUpper[i] != null && paneoImagesUpper[i].gameObject.activeInHierarchy)
            {
                Image imgUpper = paneoImagesUpper[i];
                RectTransform rtUpper = imgUpper.rectTransform;
                rtUpper.pivot = new Vector2(1, 0.5f);
                float widthUpper = rtUpper.sizeDelta.x;
                
                if (i < paneoOriginalPositions.Count)
                {
                    float newX = paneoOriginalPositions[i].x + (widthUpper / 2f);
                    rtUpper.anchoredPosition3D = new Vector3(newX, paneoOriginalPositions[i].y, paneoOriginalPositions[i].z);
                }
                
                StartCoroutine(AnimateSinglePaneoImage(imgUpper, widthUpper, 0, singleAnimationDuration));
            }

            // --- Procesar imagen INFERIOR (si existe) ---
            if (i < paneoImagesLower.Count && paneoImagesLower[i] != null && paneoImagesLower[i].gameObject.activeInHierarchy)
            {
                Image imgLower = paneoImagesLower[i];
                RectTransform rtLower = imgLower.rectTransform;
                rtLower.pivot = new Vector2(1, 0.5f);
                float widthLower = rtLower.sizeDelta.x;
                
                int posIndex = paneoImagesUpper.Count + i;
                if (posIndex < paneoOriginalPositions.Count)
                {
                    float newX = paneoOriginalPositions[posIndex].x + (widthLower / 2f);
                    rtLower.anchoredPosition3D = new Vector3(newX, paneoOriginalPositions[posIndex].y, paneoOriginalPositions[posIndex].z);
                }
                
                StartCoroutine(AnimateSinglePaneoImage(imgLower, widthLower, 0, singleAnimationDuration));
            }
            
            // --- Esperamos el retraso antes de la siguiente pareja ---
            if (i < maxImagesInGroup - 1)
            {
                yield return new WaitForSeconds(delayBetweenImages);
            }
        }
        
        // Esperamos a que la última animación termine
        yield return new WaitForSeconds(singleAnimationDuration);
        Debug.Log("PaneoTimeOut: Fase de cierre completada.");

        // --- LIMPIEZA ---
        List<Image> allImages = new List<Image>();
        allImages.AddRange(paneoImagesUpper);
        allImages.AddRange(paneoImagesLower);

        for(int i = 0; i < allImages.Count; i++)
        {
            if (allImages[i] != null)
            {
                allImages[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (i < paneoOriginalPositions.Count)
                {
                allImages[i].rectTransform.anchoredPosition3D = paneoOriginalPositions[i];
                }
                allImages[i].gameObject.SetActive(false);
            }
        }

        Debug.Log("Evento PaneoTimeOut finalizado.");
        FinalizeEvent();
    }
    private IEnumerator AnimateSinglePaneoImage(Image image, float startWidth, float endWidth, float duration)
    {
        if (image == null || duration <= 0)
        {
            // Si no hay imagen o la duración es cero, establecemos el estado final y terminamos
            if (image != null)
            {
                image.rectTransform.sizeDelta = new Vector2(endWidth, image.rectTransform.sizeDelta.y);
            }
            yield break;
        }

        float elapsedTime = 0f;
        RectTransform rt = image.rectTransform;

        while (elapsedTime < duration)
        {
            // Calculamos el nuevo ancho usando una interpolación lineal (Lerp)
            float newWidth = Mathf.Lerp(startWidth, endWidth, elapsedTime / duration);
            
            // Aplicamos el nuevo ancho, manteniendo la altura que ya tenía
            rt.sizeDelta = new Vector2(newWidth, rt.sizeDelta.y);
            
            elapsedTime += Time.deltaTime;
            yield return null; // Esperamos al siguiente frame
        }

        // Al finalizar, nos aseguramos de que la imagen tenga exactamente el ancho final
        rt.sizeDelta = new Vector2(endWidth, rt.sizeDelta.y);
    }
    
    // ===== CORRUTINA PRINCIPAL =====
    private IEnumerator AnimateSliderText(float slideDuration, string sizeCsv, string posCsv)
    {
        if (sliderBoxImage == null || spriteListProvider == null || sliderImages == null || sliderImages.Count == 0)
        {
            Debug.LogError("Faltan referencias para SliderText (modo Imágenes) en el Inspector de EventsController.");
            activeSliderSequenceCoroutine = null;
            yield break;
        }

        Vector3 startPos = ParseCoordenadas(posCsv, Vector3.zero);
        Vector3 endPos = new Vector3(-startPos.x, startPos.y, startPos.z);
        sliderBoxImage.rectTransform.sizeDelta = ParseTamano(sizeCsv, sliderBoxImage.rectTransform.sizeDelta);
        sliderBoxImage.rectTransform.anchoredPosition3D = startPos;

        sliderBoxImage.gameObject.SetActive(true);
        SetImageAlpha(sliderBoxImage, 0f);
        yield return StartCoroutine(FadeImage(sliderBoxImage, 1f, sliderBoxFadeInDuration));

        activeWordSpawnerCoroutine = StartCoroutine(ImageSpawnerCoroutine(slideDuration));
        
        // El resto de esta corrutina se encarga del deslizamiento
        float slideTimer = 0f;
        while (slideTimer < slideDuration)
        {
            sliderBoxImage.rectTransform.anchoredPosition3D = Vector3.Lerp(startPos, endPos, slideTimer / slideDuration);
            slideTimer += Time.deltaTime;
            yield return null;
        }
        
        // Aseguramos la posición final
        sliderBoxImage.rectTransform.anchoredPosition3D = endPos;
    }


    // ===== SPAWNER DE IMÁGENES =====
    private IEnumerator ImageSpawnerCoroutine(float duration)
    {
        if (LanguageManager.Instance == null)
        {
            Debug.LogError("ImageSpawnerCoroutine: No se encontró LanguageManager.");
            yield break;
        }

        string langCode = LanguageManager.Instance.GetCurrentLanguage();
        List<Sprite> sprites = spriteListProvider.GetSpriteList(langCode);

        if (sprites == null || sprites.Count == 0)
        {
            Debug.LogWarning("ImageSpawnerCoroutine: No hay sprites en la lista para el idioma " + langCode);
            yield break;
        }

        List<int> availableImageIndices = new List<int>();
        for(int i = 0; i < sliderImages.Count; i++) { availableImageIndices.Add(i); }

        if (availableImageIndices.Count > 0)
        {
            // Escogemos y preparamos la primera imagen
            int randomIndexInList = Random.Range(0, availableImageIndices.Count);
            int imageIndexToShow = availableImageIndices[randomIndexInList];
            availableImageIndices.RemoveAt(randomIndexInList);

            Image imageToShow = sliderImages[imageIndexToShow];
            imageToShow.sprite = sprites[Random.Range(0, sprites.Count)];
            
            Rect boxRect = sliderBoxImage.rectTransform.rect;
            float randomX = Random.Range(boxRect.xMin * 0.7f, boxRect.xMax * 0.7f);
            float randomY = Random.Range(boxRect.yMin * 0.7f, boxRect.yMax * 0.7f);
            imageToShow.rectTransform.anchoredPosition = new Vector2(randomX, randomY);

            SetImageAlpha(imageToShow, 0f);
            StartCoroutine(FadeImage(imageToShow, 1f, imageFadeDuration));
        }

        if (availableImageIndices.Count == 0) yield break;
        
        float remainingDuration = duration;
        float imageAppearanceInterval = remainingDuration / (sliderImages.Count - 1);

        while (availableImageIndices.Count > 0)
        {
            yield return new WaitForSeconds(imageAppearanceInterval);

            int randomIndexInList = Random.Range(0, availableImageIndices.Count);
            int imageIndexToShow = availableImageIndices[randomIndexInList];
            availableImageIndices.RemoveAt(randomIndexInList);

            Image imageToShow = sliderImages[imageIndexToShow];
            imageToShow.sprite = sprites[Random.Range(0, sprites.Count)];

            Rect boxRect = sliderBoxImage.rectTransform.rect;
            float randomX = Random.Range(boxRect.xMin * 0.7f, boxRect.xMax * 0.7f);
            float randomY = Random.Range(boxRect.yMin * 0.7f, boxRect.yMax * 0.7f);
            imageToShow.rectTransform.anchoredPosition = new Vector2(randomX, randomY);

            SetImageAlpha(imageToShow, 0f);
            StartCoroutine(FadeImage(imageToShow, 1f, imageFadeDuration));
        }
    }

    // ===== MÉTODO QUIT =====
    private IEnumerator AnimateSliderTextQuit(float fadeOutDuration)
    {
        Debug.Log("Iniciando secuencia de SliderTextQuit...");
        
        if (activeSliderSequenceCoroutine != null) StopCoroutine(activeSliderSequenceCoroutine);
        if (activeWordSpawnerCoroutine != null) StopCoroutine(activeWordSpawnerCoroutine);
        activeSliderSequenceCoroutine = null;
        activeWordSpawnerCoroutine = null;

        if (sliderBoxImage != null && sliderBoxImage.gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeImage(sliderBoxImage, 0f, fadeOutDuration));
        }

        // Hacemos fade out de las imágenes que estén activas
        if (sliderImages != null)
        {
            foreach (Image img in sliderImages)
            {
                if (img != null && img.gameObject.activeInHierarchy)
                {
                    StartCoroutine(FadeImage(img, 0f, fadeOutDuration));
                }
            }
        }

        if (fadeOutDuration > 0)
        {
            yield return new WaitForSeconds(fadeOutDuration);
        }

        // Limpieza final
        if (sliderBoxImage != null) sliderBoxImage.gameObject.SetActive(false);
        if (sliderImages != null)
        {
            foreach (Image img in sliderImages)
            {
                if (img != null) img.gameObject.SetActive(false);
            }
        }
        
        Debug.Log("SliderTextQuit completado.");
        activeSliderSequenceCoroutine = null;
        FinalizeEvent();
    }




    // --- Helpers ---
    private bool IsVisualImageBoxEventType(string eventType)
    {
        if (string.IsNullOrEmpty(eventType)) return false; return eventType.Contains("box");
    }
    private bool IsEventTypeSFXOnlyOrAuto(string eventType)
    {
        return eventType == "sfx" || eventType == "auto";
    }
    private VisualTargetType GetVisualTargetTypeFromName(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            return VisualTargetType.None;
        }

        // Comprobamos primero todos los eventos especiales que NO usan los ImageBox estándar.
        if (eventName == "bigboxsay" ||
            eventName == "bigboxsayquit" ||
            eventName == "flashback" ||
            eventName == "flashbackquit" ||
            eventName.StartsWith("heartattack-"))
        {
            return VisualTargetType.None;
        }

        // Si no es ninguno de los eventos especiales de arriba, entonces sí comprobamos si es un "box" estándar.
        if (IsVisualImageBoxEventType(eventName))
        {
            return VisualTargetType.ImageBoxUpper;
        }

        return VisualTargetType.None;
    }

    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return; Color c = image.color; c.a = Mathf.Clamp01(alpha); image.color = c;
    }
    private Vector2 ParseTamano(string csv, Vector2 def)
    {
        if (string.IsNullOrEmpty(csv)) return def; string[] p = csv.ToLowerInvariant().Split('x'); try
        {
            if (p.Length == 2)
            {
                NumberFormatInfo n = new NumberFormatInfo
                {
                    NumberDecimalSeparator = ","
                };
                return new Vector2(float.Parse(p[0].Trim(), n), float.Parse(p[1].Trim(), n));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(string.Format("EC: ErrParseTamano '{0}'. E:{1}. U:{2}", csv, e.Message, def));
        }
        return def;
    }
    // ===== Corrutina para animar el texto central =====
    private IEnumerator AnimateCentralText(string fullText, float lineDuration)
    {
        foreach (var t in centralTextUIElements) { t.text = ""; t.gameObject.SetActive(true); }
        foreach (var m in centralTextMasks) { m.sizeDelta = new Vector2(0, m.sizeDelta.y); m.gameObject.SetActive(true); }

        string[] processedLines = GetProcessedLinesForCentralText(fullText);

        for (int i = 0; i < processedLines.Length; i++)
        {
            if (i < centralTextUIElements.Count && i < centralTextMasks.Count)
            {
                centralTextUIElements[i].text = processedLines[i];
                
                // Animamos la máscara de esta línea
                RectTransform maskToAnimate = centralTextMasks[i];
                float elapsedTime = 0f;
                float safeDuration = Mathf.Max(0.01f, lineDuration);

                while (elapsedTime < safeDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float newWidth = Mathf.Lerp(0, centralTextTargetWidth, elapsedTime / safeDuration);
                    maskToAnimate.sizeDelta = new Vector2(newWidth, maskToAnimate.sizeDelta.y);
                    yield return null;
                }
                maskToAnimate.sizeDelta = new Vector2(centralTextTargetWidth, maskToAnimate.sizeDelta.y);
            }
        }

        FinalizeEvent();
    }
    // ===== Método para ocultar la UI del texto central =====
    public void HideCentralText()
    {
        if (centralTextUIElements == null) return;
        foreach (var t in centralTextUIElements) { if (t != null) t.gameObject.SetActive(false); }
        foreach (var m in centralTextMasks) { if (m != null) m.gameObject.SetActive(false); }
    }
    // ===== Helper para procesar el texto (adaptado de DialogueUIManager) =====
    private string[] GetProcessedLinesForCentralText(string originalText)
    {
        if (string.IsNullOrEmpty(originalText) || centralTextUIElements.Count == 0 || centralTextUIElements[0] == null)
        {
            return new string[0];
        }

        TextGenerator textGen = new TextGenerator();
        TextGenerationSettings settings = centralTextUIElements[0].GetGenerationSettings(new Vector2(centralTextTargetWidth, 0));
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;

        List<string> lines = new List<string>();
        string[] words = originalText.Split(' ');
        StringBuilder currentLineBuilder = new StringBuilder();

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (currentLineBuilder.Length > 0)
            {
                string testLine = currentLineBuilder.ToString() + " " + word;
                if (textGen.GetPreferredWidth(testLine, settings) > centralTextTargetWidth)
                {
                    lines.Add(currentLineBuilder.ToString());
                    currentLineBuilder.Length = 0;
                }
            }
            if (currentLineBuilder.Length > 0) currentLineBuilder.Append(" ");
            currentLineBuilder.Append(word);
        }
        if (currentLineBuilder.Length > 0) lines.Add(currentLineBuilder.ToString());
        
        return lines.ToArray();
    }


    private IEnumerator WaitForExternalCoroutine(Coroutine externalCoroutine)
    {
        if (externalCoroutine != null)
        {
            yield return externalCoroutine;
        }
    }

    private IEnumerator FadeImage(Image image, float targetAlpha, float duration)
    {
        image.gameObject.SetActive(true);
        
        float startAlpha = image.color.a;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            SetImageAlpha(image, Mathf.Lerp(startAlpha, targetAlpha, timer / duration));
            yield return null;
        }
        SetImageAlpha(image, targetAlpha);
    }

    private void SetupPaneoImage(Image image, bool forOpening)
    {
        if (image == null) return;

        RectTransform rt = image.rectTransform;

        if (forOpening)
        {
            // Configuración para ensanchar desde la izquierda
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
        }
        else
        {
            // Configuración para achicar hacia la derecha
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
        }
    }

    private Vector3 ParseCoordenadas(string csv, Vector3 def)
    {
        if (string.IsNullOrEmpty(csv)) return def; NumberFormatInfo n = new NumberFormatInfo
        {
            NumberDecimalSeparator = ","
        };
        string[] p = csv.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        try
        {
            float x = def.x, y = def.y, z = def.z;
            if (p.Length >= 1) x = float.Parse(p[0].Trim(), n);
            if (p.Length >= 2) y = float.Parse(p[1].Trim(), n);
            if (p.Length >= 3) z = float.Parse(p[2].Trim(), n);
            return new Vector3(x, y, z);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(string.Format("EC: ErrParseCoords '{0}'. E:{1}. U:{2}", csv, e.Message, def));
        }
        Debug.LogWarning(string.Format("EC: ErrFormatoCoords '{0}'. U:{1}", csv, def)); return def;
    }
    // 70% de este script lo hizo Gemini XD
}