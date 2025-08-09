using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

// Más magia :D
public enum DialogueEventType
{
    Wait,
    LineRun,
    SpriteCommand
}

public class DialogueEventTag
{
    public DialogueEventType Type;
    public int CharacterIndex;

    public float WaitDuration;
    public float EventTriggerWidth;

    public int TargetLine;
    public float TargetWidth;
    
    public int SpriteSlotIndex;
    public Dictionary<int, string> SpriteCommands;
}

public class DialogueUIManager : MonoBehaviour
{
    [Header("UI Elementos - Nombre Personaje")]
    public Text uiTextNombrePrincipal;
    public Text uiTextNombreFuerza1;
    public Text uiTextNombreFuerza2;
    public Image uiBoxNameImage;


    [Header("UI Typewriter - Máscaras y Textos por Línea")]
    public List<RectTransform> lineMasks;
    public List<Text> lineTextUIElements;
    public Image uiBoxSayImage;

    [Header("Configuración de Fades y Typewriter")]
    public float boxFadeDuration = 0.25f;
    public float delayBetweenLines = 0.1f;
    public float targetMaskWidth = 300f;

    private Coroutine activeMainCoroutine = null;
    private Coroutine activeTypewriterCoroutine = null;

    private Coroutine activeBoxNameFadeCoroutine = null;
    private Coroutine activeBoxSayFadeCoroutine = null;

    private string currentFullDialogueTextForCompletion;
    private float currentLineRevealDuration;
    private string nameToShowFromShowDialogue;

    private List<DialogueEventTag> _activeDialogueEvents = new List<DialogueEventTag>();

    public bool TextoCompletado { get; private set; }
    public bool IsDisplayingSomething { get { return activeMainCoroutine != null; } }
    public bool IsTypewriterActive { get { return activeTypewriterCoroutine != null; } }

    void Start()
    {
        if (uiTextNombrePrincipal == null || uiTextNombreFuerza1 == null || uiTextNombreFuerza2 == null)
        {
            Debug.LogError("DialogueUIManager: Faltan referencias a Text UI para nombres.", gameObject);
            this.enabled = false;
            return;
        }

        if (uiBoxNameImage == null)
        {
            Debug.LogError("DialogueUIManager: 'uiBoxNameImage' no asignado.", gameObject);
            this.enabled = false;
            return;
        }
        if (uiBoxSayImage == null)
        {
            Debug.LogError("DialogueUIManager: 'uiBoxSayImage' no asignado.", gameObject);
            this.enabled = false; return;
        }

        if (lineMasks == null || lineMasks.Count == 0 || lineTextUIElements == null || lineTextUIElements.Count == 0 || lineMasks.Count != lineTextUIElements.Count)
        {
            Debug.LogError("DialogueUIManager: Listas 'lineMasks' o 'lineTextUIElements' no configuradas o no coinciden.", gameObject);
            this.enabled = false;
            return;
        }
        for (int i = 0; i < lineMasks.Count; i++)
        {
            if (lineMasks[i] == null || lineTextUIElements[i] == null)
            {
                Debug.LogError("DialogueUIManager: Elemento nulo en 'lineMasks' o 'lineTextUIElements' en el índice " + i, gameObject);
                this.enabled = false;
                return;
            }
        }

        if (uiTextNombrePrincipal != null) uiTextNombrePrincipal.text = "";
        if (uiTextNombreFuerza1 != null) uiTextNombreFuerza1.text = "";
        if (uiTextNombreFuerza2 != null) uiTextNombreFuerza2.text = "";

        // Inicializar alfas de contenido de nombre a 0 (siempre empiezan ocultos dentro de la caja)
        SetTextAlpha(uiTextNombrePrincipal, 0f);
        SetTextAlpha(uiTextNombreFuerza1, 0f);
        SetTextAlpha(uiTextNombreFuerza2, 0f);
        foreach (Text lineText in lineTextUIElements) SetTextAlpha(lineText, 0f);


        PrepareLineMasksAndTexts(true, true);

        if (uiBoxNameImage != null)
        {
            SetImageAlpha(uiBoxNameImage, 0f);
            uiBoxNameImage.gameObject.SetActive(false);
        }
        if (uiBoxSayImage != null)
        {
            SetImageAlpha(uiBoxSayImage, 0f);
            uiBoxSayImage.gameObject.SetActive(false);
        }

        TextoCompletado = true;
    }

    //==== INICIA LA VISUALIZACIÓN DE UN NUEVO DIÁLOGO (LLAMADO DESDE GAMECONTROLLER) ====
    public void ShowDialogue(string characterName, string fullDialogueText, float textSpeed, bool isEventInText)
    {
        // Detenemos cualquier diálogo o animación que estuviera en curso.
        if (activeMainCoroutine != null) StopCoroutine(activeMainCoroutine);
        if (activeTypewriterCoroutine != null) StopCoroutine(activeTypewriterCoroutine);

        // Limpiamos la lista de eventos del diálogo anterior.
        _activeDialogueEvents.Clear();
        string textToDisplay = fullDialogueText;

        // Si este nodo es de tipo EventInText, procesamos el texto para encontrar las etiquetas.
        if (isEventInText && !string.IsNullOrEmpty(textToDisplay))
        {
            textToDisplay = ParseAndCleanTextForEvents(fullDialogueText);
        }

        // OJO: Guardamos la versión LIMPIA del texto.
        currentFullDialogueTextForCompletion = textToDisplay;
        nameToShowFromShowDialogue = characterName;
        currentLineRevealDuration = (textSpeed > 0) ? textSpeed : 0.5f;

        // Marcamos el texto como "no completado" e iniciamos la corrutina principal de visualización.
        TextoCompletado = false;
        activeMainCoroutine = StartCoroutine(OrchestrateDialogueDisplay(characterName, textToDisplay, currentLineRevealDuration));
    }


    private IEnumerator OrchestrateDialogueDisplay(string characterName, string fullDialogueText, float lineDuration)
    {
        bool hasNameContent = !string.IsNullOrEmpty(characterName);
        bool hasDialogueContent = !string.IsNullOrEmpty(fullDialogueText);
        bool shouldShowUI = hasNameContent || hasDialogueContent;

        StartCoroutine(AnimateImageAlpha(uiBoxNameImage, shouldShowUI ? 1f : 0f, boxFadeDuration));

        // Procesamos el nombre para obtener el texto final (con o sin Rich Text).
        string processedName = ProcessComplexName(characterName);

        // Aplicamos el nombre procesado a todos los textos de nombre.
        uiTextNombrePrincipal.text = processedName;
        uiTextNombreFuerza1.text = processedName;
        uiTextNombreFuerza2.text = processedName;

        // Si el nombre no usó Rich Text, nos aseguramos de que tenga el color correcto.
        if (!processedName.Contains("<color"))
        {
            UpdateNameColors(characterName);
        }
        SetTextAlpha(uiTextNombrePrincipal, hasNameContent ? 1f : 0f);
        SetTextAlpha(uiTextNombreFuerza1, hasNameContent ? 1f : 0f);
        SetTextAlpha(uiTextNombreFuerza2, hasNameContent ? 1f : 0f);

        // ===== La animación de la caja de diálogo y el texto empiezan a la vez =====
        StartCoroutine(AnimateImageAlpha(uiBoxSayImage, hasDialogueContent ? 1f : 0f, boxFadeDuration));

        // Inmediatamente después, preparamos y empezamos a animar el texto del diálogo
        if (hasDialogueContent)
        {
            string[] processedLines = GetProcessedLinesArray(fullDialogueText);
            PrepareLineMasksAndTexts(true, true);
            for (int i = 0; i < processedLines.Length; i++)
            {
                if (i < lineTextUIElements.Count)
                {
                    lineTextUIElements[i].text = "\n" + processedLines[i];
                    SetTextAlpha(lineTextUIElements[i], 1f);
                }
            }

            // La corrutina ahora espera a que el typewriter termine, no a que la caja aparezca.
            activeTypewriterCoroutine = StartCoroutine(AnimateMaskedTypewriter(fullDialogueText, lineDuration));
            yield return activeTypewriterCoroutine;
            activeTypewriterCoroutine = null;
        }
        else
        {
            // Si no hay diálogo, aún debemos esperar a que terminen los fades de las cajas
            yield return new WaitForSeconds(boxFadeDuration);
        }

        TextoCompletado = true;
        activeMainCoroutine = null;
    }


    // --- Sub-Corrutinas de Fade ---
    private IEnumerator FadeOutContent(float duration, bool clearTextAfter = false)
    {
        // Esta corrutina ahora solo se encarga del fade out del contenido, no de las cajas
        List<Coroutine> fades = new List<Coroutine>();
        if (uiTextNombrePrincipal != null && uiTextNombrePrincipal.color.a > 0.01f)
            fades.Add(StartCoroutine(AnimateTextAlpha(uiTextNombrePrincipal, 0f, duration)));
        if (uiTextNombreFuerza1 != null && uiTextNombreFuerza1.color.a > 0.01f)
            fades.Add(StartCoroutine(AnimateTextAlpha(uiTextNombreFuerza1, 0f, duration)));
        if (uiTextNombreFuerza2 != null && uiTextNombreFuerza2.color.a > 0.01f)
            fades.Add(StartCoroutine(AnimateTextAlpha(uiTextNombreFuerza2, 0f, duration)));

        // Para el texto de las líneas, en lugar de fade, las cerramos con las máscaras y limpiamos.
        // O podríamos hacer un fade de cada Text si quisiéramos.
        // Por ahora, las máscaras se cerrarán y el texto se limpiará.
        // El problema son los fps... pero se puede cambiar esto... que pereza rehacer eso x,D
        PrepareLineMasksAndTexts(clearTextAfter, true);

        foreach (Coroutine fade in fades) { if (fade != null) yield return fade; }

        if (clearTextAfter)
        {
            if (uiTextNombrePrincipal != null) uiTextNombrePrincipal.text = "";
        }
    }

    private IEnumerator FadeInContent_NameOnly(float duration)
    {
        List<Coroutine> fades = new List<Coroutine>();
        if (!string.IsNullOrEmpty(uiTextNombrePrincipal.text))
        {
            if (uiTextNombrePrincipal != null && uiTextNombrePrincipal.color.a < 1f) fades.Add(StartCoroutine(AnimateTextAlpha(uiTextNombrePrincipal, 1f, duration)));
            if (uiTextNombreFuerza1 != null && uiTextNombreFuerza1.color.a < 1f) fades.Add(StartCoroutine(AnimateTextAlpha(uiTextNombreFuerza1, 1f, duration)));
            if (uiTextNombreFuerza2 != null && uiTextNombreFuerza2.color.a < 1f) fades.Add(StartCoroutine(AnimateTextAlpha(uiTextNombreFuerza2, 1f, duration)));
        }
        else
        {
            if (uiTextNombrePrincipal != null) SetTextAlpha(uiTextNombrePrincipal, 0f);
            if (uiTextNombreFuerza1 != null) SetTextAlpha(uiTextNombreFuerza1, 0f);
            if (uiTextNombreFuerza2 != null) SetTextAlpha(uiTextNombreFuerza2, 0f);
        }
        foreach (Coroutine fade in fades)
        {
            if (fade != null) yield return fade;
        }
    }

    private IEnumerator FadeOutBoxes(float duration)
    {
        Coroutine nameBoxFade = null;
        Coroutine sayBoxFade = null;
        if (uiBoxNameImage != null && uiBoxNameImage.color.a > 0) nameBoxFade = StartCoroutine(AnimateImageAlpha(uiBoxNameImage, 0f, duration));
        if (uiBoxSayImage != null && uiBoxSayImage.color.a > 0) sayBoxFade = StartCoroutine(AnimateImageAlpha(uiBoxSayImage, 0f, duration));
        if (nameBoxFade != null) yield return nameBoxFade;
        if (sayBoxFade != null) yield return sayBoxFade;
    }

    private IEnumerator FadeInBoxes(float duration)
    {
        Coroutine nameBoxFade = null;
        Coroutine sayBoxFade = null;
        if (uiBoxNameImage != null && uiBoxNameImage.color.a < 1f) nameBoxFade = StartCoroutine(AnimateImageAlpha(uiBoxNameImage, 1f, duration));
        if (uiBoxSayImage != null && uiBoxSayImage.color.a < 1f) sayBoxFade = StartCoroutine(AnimateImageAlpha(uiBoxSayImage, 1f, duration));
        if (nameBoxFade != null) yield return nameBoxFade;
        if (sayBoxFade != null) yield return sayBoxFade;
    }

    // ===== CORRUTINA PARA ANIMAR EL ALFA DE UN COMPONENTE IMAGE =====
    private IEnumerator AnimateImageAlpha(Image imageToFade, float targetAlpha, float duration)
    {
        if (imageToFade == null)
        {
            yield break;
        }

        if (targetAlpha > 0 && !imageToFade.gameObject.activeSelf)
        {
            imageToFade.gameObject.SetActive(true);
        }

        float startAlpha = imageToFade.color.a;
        float elapsedTime = 0f;

        if (duration <= 0f)
        {
            SetImageAlpha(imageToFade, targetAlpha);
            if (targetAlpha == 0f && imageToFade.gameObject.activeSelf)
            {
                imageToFade.gameObject.SetActive(false);
            }
            else if (targetAlpha > 0f && !imageToFade.gameObject.activeSelf) // Asegurar que esté activo si el alfa final es >0
            {
                imageToFade.gameObject.SetActive(true);
            }
            Debug.Log(string.Format("AnimateImageAlpha (DialogueUI) para {0} TERMINADO (duración cero). FinalAlpha={1}, FinalActive={2}",
                                    imageToFade.gameObject.name, imageToFade.color.a, imageToFade.gameObject.activeSelf));
            yield break;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            SetImageAlpha(imageToFade, newAlpha);
            yield return null;
        }
        SetImageAlpha(imageToFade, targetAlpha);
        if (targetAlpha == 0f)
        {
            if (imageToFade.gameObject.activeSelf)
            {
                imageToFade.gameObject.SetActive(false);
            }
        }
        else
        {
            if (!imageToFade.gameObject.activeSelf)
            {
                imageToFade.gameObject.SetActive(true);
            }
        }
    }
    // ===== CORRUTINA PARA ANIMAR EL ALFA DE UN COMPONENTE TEXT =====
    private IEnumerator AnimateTextAlpha(Text textToFade, float targetAlpha, float duration)
    {
        if (textToFade == null) yield break;

        float startAlpha = textToFade.color.a;
        float elapsedTime = 0f;

        if (duration <= 0f)
        {
            SetTextAlpha(textToFade, targetAlpha);
            yield break;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            SetTextAlpha(textToFade, newAlpha);
            yield return null;
        }
        SetTextAlpha(textToFade, targetAlpha);
    }

    // Helper para establecer alfa de una Image
    private void SetTextAlpha(Text textElement, float alpha)
    {
        if (textElement == null) return;
        Color currentColor = textElement.color;
        currentColor.a = Mathf.Clamp01(alpha);
        textElement.color = currentColor;
    }

    private void SetImageAlpha(Image image, float alpha)
    {
        if (image != null)
        {
            Color c = image.color;
            c.a = Mathf.Clamp01(alpha);
            image.color = c;
        }
    }

    //==== COMPLETA INSTANTÁNEAMENTE TODO EL PROCESO DE VISUALIZACIÓN ====
    public void CompleteTypewriter()
    {
        // Detener todas las corrutinas de animación de este manager
        if (activeMainCoroutine != null)
        {
            StopCoroutine(activeMainCoroutine);
            activeMainCoroutine = null;
        }
        if (activeTypewriterCoroutine != null)
        {
            StopCoroutine(activeTypewriterCoroutine);
            activeTypewriterCoroutine = null;
        }
        if (activeBoxNameFadeCoroutine != null)
        {
            StopCoroutine(activeBoxNameFadeCoroutine);
            activeBoxNameFadeCoroutine = null;
        }
        if (activeBoxSayFadeCoroutine != null)
        {
            StopCoroutine(activeBoxSayFadeCoroutine);
            activeBoxSayFadeCoroutine = null;
        }

        // Determinar si las cajas deben estar visibles
        bool hasTargetName = !string.IsNullOrEmpty(nameToShowFromShowDialogue);
        bool hasTargetDialogue = !string.IsNullOrEmpty(currentFullDialogueTextForCompletion);
        bool shouldShowUI = hasTargetName || hasTargetDialogue;

        if (shouldShowUI)
        {
            // --- SALTO DURANTE APARICIÓN O MIENTRAS SE MUESTRA ---
            if (uiBoxNameImage != null)
            {
                SetImageAlpha(uiBoxNameImage, 1f);
                uiBoxNameImage.gameObject.SetActive(true);
            }
            if (uiBoxSayImage != null && hasTargetDialogue) // Solo mostrar caja de decir si hay diálogo objetivo
            {
                SetImageAlpha(uiBoxSayImage, 1f);
                uiBoxSayImage.gameObject.SetActive(true);
            }
            else if (uiBoxSayImage != null)
            {
                // No hay diálogo objetivo, ocultar caja de decir
                SetImageAlpha(uiBoxSayImage, 0f);
                uiBoxSayImage.gameObject.SetActive(false);
            }

            // Contenido del nombre instantáneamente visible
            if (uiTextNombrePrincipal != null)
            {
                string processedName = ProcessComplexName(nameToShowFromShowDialogue);

                uiTextNombrePrincipal.text = processedName;
                uiTextNombreFuerza1.text = processedName;
                uiTextNombreFuerza2.text = processedName;

                // Nos aseguramos de que los textos sean visibles si corresponde.
                SetTextAlpha(uiTextNombrePrincipal, hasTargetName ? 1f : 0f);
                SetTextAlpha(uiTextNombreFuerza1, hasTargetName ? 1f : 0f);
                SetTextAlpha(uiTextNombreFuerza2, hasTargetName ? 1f : 0f);

                if (hasTargetName && !processedName.Contains("<color"))
                {
                    UpdateNameColors(nameToShowFromShowDialogue);
                }
            }

            if (hasTargetDialogue && lineTextUIElements != null && lineMasks != null)
            {
                string[] processedLines = GetProcessedLinesArray(currentFullDialogueTextForCompletion);
                int actualLines = processedLines.Length; // Usamos la longitud de las líneas procesadas

                for (int i = 0; i < lineTextUIElements.Count; i++)
                {
                    if (lineTextUIElements[i] != null)
                    {
                        if (i < actualLines)
                        {
                            // Aseguramos que el texto correcto esté y sea visible
                            lineTextUIElements[i].text = "\n" + processedLines[i]; // Re-asignamos por si acaso, aunque Orchestrate ya lo hizo
                            SetTextAlpha(lineTextUIElements[i], 1f);
                            if (lineMasks.Count > i && lineMasks[i] != null)
                            {
                                lineMasks[i].sizeDelta = new Vector2(targetMaskWidth, lineMasks[i].sizeDelta.y);
                            }
                        }
                        else
                        {
                            lineTextUIElements[i].text = "";
                            SetTextAlpha(lineTextUIElements[i], 0f);
                            if (lineMasks.Count > i && lineMasks[i] != null)
                            {
                                lineMasks[i].sizeDelta = new Vector2(0, lineMasks[i].sizeDelta.y);
                            }
                        }
                    }
                }
            }
            else if (lineTextUIElements != null)
            {
                foreach (Text lineTextComponent in lineTextUIElements)
                {
                    if (lineTextComponent != null) { lineTextComponent.text = ""; SetTextAlpha(lineTextComponent, 0f); }
                }
                foreach (RectTransform mask in lineMasks)
                {
                    if (mask != null) mask.sizeDelta = new Vector2(0, mask.sizeDelta.y);
                }
            }
        }
        else
        {
            // --- SALTO DURANTE DESAPARICIÓN O SI NO HABÍA NADA QUE MOSTRAR ---
            if (uiTextNombrePrincipal != null) { uiTextNombrePrincipal.text = ""; SetTextAlpha(uiTextNombrePrincipal, 0f); }
            if (uiTextNombreFuerza1 != null) { uiTextNombreFuerza1.text = ""; SetTextAlpha(uiTextNombreFuerza1, 0f); }
            if (uiTextNombreFuerza2 != null) { uiTextNombreFuerza2.text = ""; SetTextAlpha(uiTextNombreFuerza2, 0f); }

            if (lineTextUIElements != null)
            {
                foreach (Text lineTextComponent in lineTextUIElements)
                {
                    if (lineTextComponent != null) { lineTextComponent.text = ""; SetTextAlpha(lineTextComponent, 0f); }
                }
            }

            // Cajas instantáneamente invisibles e inactivas
            if (uiBoxNameImage != null) { SetImageAlpha(uiBoxNameImage, 0f); uiBoxNameImage.gameObject.SetActive(false); }
            if (uiBoxSayImage != null) { SetImageAlpha(uiBoxSayImage, 0f); uiBoxSayImage.gameObject.SetActive(false); }

            // Máscaras cerradas
            if (lineMasks != null)
            {
                foreach (RectTransform mask in lineMasks)
                {
                    if (mask != null) mask.sizeDelta = new Vector2(0, mask.sizeDelta.y);
                }
            }
        }

        TextoCompletado = true;
    }

    private IEnumerator AnimateMaskedTypewriter(string textForLineCount, float lineDuration)
    {
        string[] processedLines = GetProcessedLinesArray(textForLineCount);
        int linesToRevealCount = Mathf.Min(lineMasks.Count, processedLines.Length);
        float safeLineDuration = Mathf.Max(0.01f, lineDuration);

        TextGenerator textGen = new TextGenerator();
        TextGenerationSettings settings = lineTextUIElements[0].GetGenerationSettings(new Vector2(targetMaskWidth, 0));
        string fullCleanText = textForLineCount;
        textGen.Populate(fullCleanText, settings);
        IList<UILineInfo> lineInfos = textGen.lines;

        DialogueEventTag lineRunEvent = null;
        foreach (var evt in _activeDialogueEvents)
        {
            if (evt.Type == DialogueEventType.LineRun)
            {
                if (lineRunEvent == null || evt.TargetLine > lineRunEvent.TargetLine)
                {
                    lineRunEvent = evt;
                }
            }
        }

        int startingLine = 0;
        if (lineRunEvent != null)
        {
            for (int i = 0; i < lineRunEvent.TargetLine - 1; i++)
            {
                if (i < lineMasks.Count)
                    lineMasks[i].sizeDelta = new Vector2(targetMaskWidth, lineMasks[i].sizeDelta.y);
            }
            if (lineRunEvent.TargetLine - 1 < lineMasks.Count)
            {
                int targetLineIndex = lineRunEvent.TargetLine - 1;
                float initialWidth = Mathf.Clamp(lineRunEvent.TargetWidth, 0, targetMaskWidth);
                lineMasks[targetLineIndex].sizeDelta = new Vector2(initialWidth, lineMasks[targetLineIndex].sizeDelta.y);
            }
            startingLine = lineRunEvent.TargetLine - 1;
        }

        for (int i = startingLine; i < linesToRevealCount; i++)
        {
            RectTransform currentMaskRect = lineMasks[i];
            if (currentMaskRect == null) continue;

            // Obtenemos los límites de la línea actual del TextGenerator
            if (i >= lineInfos.Count)
            {
                Debug.LogError("Error de sincronización entre processedLines y lineInfos. Abortando typewriter.");
                yield break;
            }
            var currentLineInfo = lineInfos[i];
            int lineStartCharIndex = currentLineInfo.startCharIdx;
            int lineEndCharIndex = (i < lineInfos.Count - 1) ? lineInfos[i + 1].startCharIdx : fullCleanText.Length;


            List<DialogueEventTag> eventsOnThisLine = new List<DialogueEventTag>();
            foreach (var evt in _activeDialogueEvents)
            {
                if ((evt.Type == DialogueEventType.Wait || evt.Type == DialogueEventType.SpriteCommand) &&
                    evt.CharacterIndex >= lineStartCharIndex &&
                    evt.CharacterIndex <= lineEndCharIndex)
                {
                    eventsOnThisLine.Add(evt);
                }
            }
            eventsOnThisLine.Sort((a, b) => a.CharacterIndex.CompareTo(b.CharacterIndex));

            float currentWidth = currentMaskRect.sizeDelta.x;

            foreach (var currentEvent in eventsOnThisLine)
            {
                float targetWidth = currentEvent.EventTriggerWidth;
                float durationMultiplier = (targetWidth - currentWidth) / targetMaskWidth;
                float segmentDuration = safeLineDuration * durationMultiplier;

                if (segmentDuration > 0)
                {
                    float elapsedTime = 0f;
                    while (elapsedTime < segmentDuration)
                    {
                        elapsedTime += Time.deltaTime;
                        float newWidth = Mathf.Lerp(currentWidth, targetWidth, elapsedTime / segmentDuration);
                        currentMaskRect.sizeDelta = new Vector2(newWidth, currentMaskRect.sizeDelta.y);
                        yield return null;
                    }
                }

                currentMaskRect.sizeDelta = new Vector2(targetWidth, currentMaskRect.sizeDelta.y);

                if (currentEvent.Type == DialogueEventType.SpriteCommand)
                {
                    ExecuteSpriteEvent(currentEvent);
                }

                yield return new WaitForSeconds(currentEvent.WaitDuration);
                currentWidth = targetWidth;
            }

            float finalSegmentDuration = safeLineDuration * ((targetMaskWidth - currentWidth) / targetMaskWidth);
            if (finalSegmentDuration > 0)
            {
                float elapsedTime = 0f;
                while (elapsedTime < finalSegmentDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float newWidth = Mathf.Lerp(currentWidth, targetMaskWidth, elapsedTime / finalSegmentDuration);
                    currentMaskRect.sizeDelta = new Vector2(newWidth, currentMaskRect.sizeDelta.y);
                    yield return null;
                }
            }

            currentMaskRect.sizeDelta = new Vector2(targetMaskWidth, currentMaskRect.sizeDelta.y);

            if (i < linesToRevealCount - 1)
            {
                yield return new WaitForSeconds(delayBetweenLines);
            }
        }
        activeTypewriterCoroutine = null;
    }

    // ===== CALCULA EL NÚMERO DE LÍNEAS VISUALES QUE OCUPARÁ UN TEXTO DADO =====
    private int GetActualLineCount(string textToAnalyze)
    {
        if (IsStringNullOrAllWhiteSpace(textToAnalyze) || lineTextUIElements == null || lineTextUIElements.Count == 0 || lineTextUIElements[0] == null)
        {
            return 0;
        }

        TextGenerator textGen = new TextGenerator();
        TextGenerationSettings settings = lineTextUIElements[0].GetGenerationSettings(new Vector2(targetMaskWidth, lineTextUIElements[0].rectTransform.rect.height));
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.scaleFactor = 1f;
        settings.resizeTextForBestFit = false;

        textGen.Populate(textToAnalyze, settings);
        int lineCount = textGen.lineCount;

        return lineCount;
    }

    // ===== PREPARA (LIMPIA/RESETEA) LOS TEXTOS DE LÍNEA Y SUS MÁSCARAS =====
    private void PrepareLineMasksAndTexts(bool clearExistingText, bool resetAllMasksToZeroWidth)
    {
        if (lineTextUIElements == null || lineMasks == null || lineTextUIElements.Count != lineMasks.Count)
        {
            return;
        }
        for (int i = 0; i < lineTextUIElements.Count; i++)
        {
            if (lineTextUIElements[i] != null && clearExistingText)
            {
                lineTextUIElements[i].text = "";
            }
            if (lineMasks[i] != null)
            {
                if (resetAllMasksToZeroWidth)
                {
                    lineMasks[i].sizeDelta = new Vector2(0f, lineMasks[i].sizeDelta.y);
                }
                lineMasks[i].gameObject.SetActive(true);
            }
        }
    }

    // ===== ACTUALIZA LOS COLORES DE LOS TEXTOS DE NOMBRE USANDO NAMECOLORCHANGER =====
    private void UpdateNameColors(string characterName)
    {
        if (uiTextNombrePrincipal != null)
        {
            NameColorChanger ncc = uiTextNombrePrincipal.GetComponent<NameColorChanger>();
            if (ncc != null) ncc.UpdateCharacterColor(characterName);
        }
        if (uiTextNombreFuerza1 != null)
        {
            NameColorChanger ncc = uiTextNombreFuerza1.GetComponent<NameColorChanger>();
            if (ncc != null) ncc.UpdateCharacterColor(characterName);
        }
        if (uiTextNombreFuerza2 != null)
        {
            NameColorChanger ncc = uiTextNombreFuerza2.GetComponent<NameColorChanger>();
            if (ncc != null) ncc.UpdateCharacterColor(characterName);
        }
    }

    // ===== VERIFICA SI UN STRING ES NULO, VACÍO O SOLO CONTIENE ESPACIOS EN BLANCO =====
    private bool IsStringNullOrAllWhiteSpace(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                return false;
            }
        }
        return true;
    }

    // ===== MÉTODO PARA PROCESAR EL TEXTO COMPLETO EN LÍNEAS INDIVIDUALES AJUSTADAS AL ANCHO =====
    private string[] GetProcessedLinesArray(string originalText)
    {
        if (string.IsNullOrEmpty(originalText) || lineTextUIElements == null || lineTextUIElements.Count == 0 || lineTextUIElements[0] == null || targetMaskWidth <= 0)
        {
            if (string.IsNullOrEmpty(originalText)) return new string[0]; // Devuelve array vacío si no hay texto
            return new string[] { originalText }; // Devuelve el texto original en una sola línea si hay error de configuración
        }

        TextGenerator textGen = new TextGenerator();
        TextGenerationSettings settings = lineTextUIElements[0].GetGenerationSettings(new Vector2(targetMaskWidth, lineTextUIElements[0].rectTransform.rect.height));

        settings.resizeTextForBestFit = false;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.updateBounds = true;

        List<string> lines = new List<string>();
        string[] words = originalText.Split(' ');

        if (words.Length == 0)
        {
            return new string[0];
        }

        StringBuilder currentLineBuilder = new StringBuilder();

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];

            if (currentLineBuilder.Length == 0)
            {
                currentLineBuilder.Append(word);
            }
            else
            {
                string testLine = currentLineBuilder.ToString() + " " + word;
                textGen.Populate(testLine, settings);

                if (textGen.lineCount > 1)
                {
                    lines.Add(currentLineBuilder.ToString()); // Añadimos la línea completada a la lista
                    currentLineBuilder.Length = 0;
                    currentLineBuilder.Append(word);
                }
                else
                {
                    currentLineBuilder.Append(" ").Append(word);
                }
            }
        }

        if (currentLineBuilder.Length > 0)
        {
            lines.Add(currentLineBuilder.ToString());
        }
        return lines.ToArray();
    }

    private string ProcessComplexName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "";

        // Obtenemos el componente que sabe los colores de cada personaje.
        NameColorChanger ncc = uiTextNombrePrincipal.GetComponent<NameColorChanger>();
        if (ncc == null) return rawName; // Si no hay NameColorChanger, devolvemos el nombre tal cual.

        string colorSourceName = null;
        string displayName = rawName;

        // Revisamos si hay un override de color (C_...)
        if (rawName.StartsWith("C_") && rawName.Contains("-"))
        {
            string[] parts = rawName.Split(new char[] { '-' }, 2);
            colorSourceName = parts[0].Substring(2); // Obtenemos el nombre fuente del color (ej. "Misha")
            displayName = parts[1];
        }

        // Revisamos si es un nombre compuesto, separado por _SPACE_
        string[] nameParts = displayName.Split(new string[] { "_SPACE_" }, System.StringSplitOptions.None);

        // Si solo hay una parte y no hay un color personalizado, devolvemos el nombre normal.
        if (nameParts.Length == 1 && colorSourceName == null)
        {
            return displayName;
        }

        // Si hay formato especial, construimos el string con Rich Text.
        StringBuilder richTextBuilder = new StringBuilder();
        for (int i = 0; i < nameParts.Length; i++)
        {
            string currentPart = nameParts[i];
            string sourceForColor = currentPart; // Por defecto, cada parte usa su propio color.

            // Si es la primera parte y tenemos un override de color, lo usamos.
            if (i == 0 && !string.IsNullOrEmpty(colorSourceName))
            {
                sourceForColor = colorSourceName;
            }

            // Obtenemos el color y lo convertimos a formato hexadecimal #RRGGBB.
            Color partColor = ncc.GetColorForCharacter(sourceForColor);
            string hexColor = ColorUtility.ToHtmlStringRGB(partColor);

            richTextBuilder.AppendFormat("<color=#{0}>{1}</color>", hexColor, currentPart);

            // Si no es la última parte, añadimos un espacio.
            if (i < nameParts.Length - 1)
            {
                richTextBuilder.Append(" ");
            }
        }

        return richTextBuilder.ToString();
    }

    private string ParseAndCleanTextForEvents(string originalText)
    {
        _activeDialogueEvents.Clear();
        Regex tagRegex = new Regex(@"<([^>]+)>");
        string cleanText = tagRegex.Replace(originalText, "");

        TextGenerator textGen = new TextGenerator();
        TextGenerationSettings settings = lineTextUIElements[0].GetGenerationSettings(new Vector2(targetMaskWidth, 0));

        MatchCollection matches = tagRegex.Matches(originalText);
        int removedCharsCount = 0;

        // --- FASE 1: Extraer todos los eventos y su posición absoluta en el texto limpio ---
        foreach (Match match in matches)
        {
            DialogueEventTag eventTag = new DialogueEventTag();
            string tagContent = match.Groups[1].Value;
            eventTag.CharacterIndex = match.Index - removedCharsCount;

            if (tagContent.StartsWith("wait-"))
            {
                eventTag.Type = DialogueEventType.Wait;
                string durationStr = tagContent.Substring(5);
                float.TryParse(durationStr, NumberStyles.Any, CultureInfo.InvariantCulture, out eventTag.WaitDuration);
            }
            else if (tagContent.StartsWith("line_") && tagContent.Contains("-run_"))
            {
                eventTag.Type = DialogueEventType.LineRun;
                string[] parts = tagContent.Split(new string[] { "-run_" }, System.StringSplitOptions.None);
                string linePart = parts[0].Substring(5);
                string widthPart = parts[1];
                int.TryParse(linePart, out eventTag.TargetLine);
                float.TryParse(widthPart, NumberStyles.Any, CultureInfo.InvariantCulture, out eventTag.TargetWidth);
            }

            else if (tagContent.StartsWith("Sprite"))
            {
                eventTag.Type = DialogueEventType.SpriteCommand;
                eventTag.SpriteCommands = new Dictionary<int, string>();
                eventTag.WaitDuration = 0f;
                string[] mainParts = tagContent.Split(new string[] { "--" }, System.StringSplitOptions.None);
                if (mainParts.Length > 0)
                {
                    string slotPart = mainParts[0];
                    int slotNumber;
                    if (int.TryParse(slotPart.Replace("Sprite", ""), out slotNumber))
                    {
                        eventTag.SpriteSlotIndex = slotNumber - 1;
                    }
                    for (int i = 1; i < mainParts.Length; i++)
                    {
                        string commandPart = mainParts[i];
                        string[] commandPieces = commandPart.Split(new char[] { '_' }, 2);
                        if (commandPieces.Length == 2)
                        {
                            int headerNumber;
                            if (int.TryParse(commandPieces[0], out headerNumber))
                            {
                                string value = commandPieces[1];
                                eventTag.SpriteCommands[headerNumber] = value;
                                if (headerNumber == 6 || headerNumber == 12 || headerNumber == 18 || headerNumber == 24 || headerNumber == 30)
                                {
                                    float waitTime;
                                    if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out waitTime))
                                    {
                                        eventTag.WaitDuration = waitTime;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                removedCharsCount += match.Length;
                continue;
            }

            _activeDialogueEvents.Add(eventTag);
            removedCharsCount += match.Length;
        }
        // --- FASE 2: Calcular el ancho relativo ---
        //HORAS PARA HACER SOLO UN + \U200B POR LA PTMR XDDDDD AAAAAAH
        textGen.Populate(cleanText + "\u200B", settings); // \u200B es un Zero-Width Space

        IList<UILineInfo> lines = textGen.lines;

        for (int i = 0; i < _activeDialogueEvents.Count; i++)
        {
            var evt = _activeDialogueEvents[i];
            if (evt.Type == DialogueEventType.LineRun) continue;

            for (int j = 0; j < lines.Count; j++)
            {
                var line = lines[j];
                int lineEndCharIndex = (j < lines.Count - 1) ? lines[j + 1].startCharIdx : cleanText.Length;

                if (evt.CharacterIndex >= line.startCharIdx && evt.CharacterIndex <= lineEndCharIndex)
                {
                    if (evt.CharacterIndex == lineEndCharIndex && j < lines.Count - 1 && cleanText.Length > lineEndCharIndex) continue;

                    int relativeCharIndex = evt.CharacterIndex - line.startCharIdx;
                    string textUntilTagInLine = cleanText.Substring(line.startCharIdx, relativeCharIndex);
                    evt.EventTriggerWidth = textGen.GetPreferredWidth(textUntilTagInLine, settings);
                    break;
                }
            }
        }

        return cleanText;
    }

    private void ExecuteSpriteEvent(DialogueEventTag spriteEvent)
    {
        if (spriteEvent.SpriteSlotIndex < 0 || spriteEvent.SpriteSlotIndex > 2) return;

        // 1. Diccionarios para almacenar los parámetros de cada parte del cuerpo
        var bodyParams = new Dictionary<int, string>();
        var faceParams = new Dictionary<int, string>();
        var headParams = new Dictionary<int, string>();
        var leftArmParams = new Dictionary<int, string>();
        var rightArmParams = new Dictionary<int, string>();

        // 2. Clasificamos cada comando en su diccionario correspondiente
        foreach (var cmd in spriteEvent.SpriteCommands)
        {
            int headerNum = cmd.Key;
            string value = cmd.Value;

            if (headerNum >= 1 && headerNum <= 6) bodyParams[headerNum] = value;
            else if (headerNum >= 7 && headerNum <= 12) faceParams[headerNum] = value;
            else if (headerNum >= 13 && headerNum <= 18) headParams[headerNum] = value;
            else if (headerNum >= 19 && headerNum <= 24) leftArmParams[headerNum] = value;
            else if (headerNum >= 25 && headerNum <= 30) rightArmParams[headerNum] = value;
        }

        // 3. Obtenemos el GameController para acceder a los controladores de sprites
        GameController gc = FindObjectOfType<GameController>();
        if (gc == null)
        {
            Debug.LogError("DialogueUIManager: No se pudo encontrar GameController para ejecutar evento de sprite.");
            return;
        }


        if (bodyParams.Count > 0 && gc.spritesController != null)
        {
            gc.spritesController.ProcessSpriteCommand(spriteEvent.SpriteSlotIndex,
                GetParam(bodyParams, 1),    // Sprite1
                GetParam(bodyParams, 3),    // Sprite1_Accion
                GetParam(bodyParams, 4),    // Sprite1_Switch
                GetParam(bodyParams, 5),    // Sprite1_Coordenadas
                GetTime(bodyParams, 6),     // Sprite1_Tiempo
                GetParam(bodyParams, 2));   // Sprite1_Tamano
        }

        // Cara (Headers 7-12)
        if (faceParams.Count > 0 && gc.facesController != null)
        {
            gc.facesController.ProcessSpriteCommand(spriteEvent.SpriteSlotIndex,
                GetParam(faceParams, 7),    // Sprite1Cara
                GetParam(faceParams, 9),    // Sprite1Cara_Accion
                GetParam(faceParams, 10),   // Sprite1Cara_Switch
                GetParam(faceParams, 11),   // Sprite1Cara_Coordenadas
                GetTime(faceParams, 12),    // Sprite1Cara_Tiempo
                GetParam(faceParams, 8));   // Sprite1Cara_Tamano
        }

        // Cabeza (Headers 13-18)
        if (headParams.Count > 0 && gc.headsController != null)
        {
            gc.headsController.ProcessSpriteCommand(spriteEvent.SpriteSlotIndex,
                GetParam(headParams, 13),   // Sprite1Cabeza
                GetParam(headParams, 15),   // Sprite1Cabeza_Accion
                GetParam(headParams, 16),   // Sprite1Cabeza_Switch
                GetParam(headParams, 17),   // Sprite1Cabeza_Coordenadas
                GetTime(headParams, 18),    // Sprite1Cabeza_Tiempo
                GetParam(headParams, 14));  // Sprite1Cabeza_Tamano
        }

        // Brazo Izquierdo (Headers 19-24)
        if (leftArmParams.Count > 0 && gc.leftArmController != null)
        {
            gc.leftArmController.ProcessSpriteCommand(spriteEvent.SpriteSlotIndex,
                GetParam(leftArmParams, 19),  // Sprite1BrazoIzq
                GetParam(leftArmParams, 21),  // Sprite1BrazoIzq_Accion
                GetParam(leftArmParams, 22),  // Sprite1BrazoIzq_Switch
                GetParam(leftArmParams, 23),  // Sprite1BrazoIzq_Coordenadas
                GetTime(leftArmParams, 24),   // Sprite1BrazoIzq_Tiempo
                GetParam(leftArmParams, 20)); // Sprite1BrazoIzq_Tamano
        }

        // Brazo Derecho (Headers 25-30)
        if (rightArmParams.Count > 0 && gc.rightArmController != null)
        {
            gc.rightArmController.ProcessSpriteCommand(spriteEvent.SpriteSlotIndex,
                GetParam(rightArmParams, 25),  // Sprite1BrazoDer
                GetParam(rightArmParams, 27),  // Sprite1BrazoDer_Accion
                GetParam(rightArmParams, 28),  // Sprite1BrazoDer_Switch
                GetParam(rightArmParams, 29),  // Sprite1BrazoDer_Coordenadas
                GetTime(rightArmParams, 30),   // Sprite1BrazoDer_Tiempo
                GetParam(rightArmParams, 26)); // Sprite1BrazoDer_Tamano
        }
    }
    public void ClearDialogueContent()
    {
        // Limpiar el nombre
        if (uiTextNombrePrincipal != null) uiTextNombrePrincipal.text = "";
        if (uiTextNombreFuerza1 != null) uiTextNombreFuerza1.text = "";
        if (uiTextNombreFuerza2 != null) uiTextNombreFuerza2.text = "";

        // Limpiar las líneas y resetear las máscaras
        PrepareLineMasksAndTexts(true, true);

        // Nos aseguramos de que no haya una corrutina de escritura activa
        if (activeTypewriterCoroutine != null)
        {
            StopCoroutine(activeTypewriterCoroutine);
            activeTypewriterCoroutine = null;
        }
        TextoCompletado = true; // No hay nada escribiéndose
    }
    private string GetParam(Dictionary<int, string> dict, int key)
    {
        // Si el diccionario contiene la clave, devuelve el valor. Si no, devuelve null.
        return dict.ContainsKey(key) ? dict[key] : null;
    }
    private float GetTime(Dictionary<int, string> dict, int key)
    {
        float time = 0.5f; // Valor por defecto si no se especifica o falla el parseo.
        if (dict.ContainsKey(key))
        {
            // Intenta convertir el valor a float. Si no lo consigue, se queda con el valor por defecto.
            float.TryParse(dict[key], NumberStyles.Any, CultureInfo.InvariantCulture, out time);
        }
        return time;
    }
    //Tqm gemini x2
}