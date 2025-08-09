using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class BigBoxConfig
{
    public string boxName;
    public List<RectTransform> lineMasks;
    public List<Text> lineTextUIElements;
}

public class BigDialogueController : MonoBehaviour
{
    [Header("Componente UI Principal")]
    public Image uiBoxSayImage;

    [Header("Configuraciones de Big Box")]
    public List<BigBoxConfig> bigBoxConfigurations;

    [Header("Configuración de Animación")]
    public float boxFadeDuration = 0.5f;
    public float delayBetweenLines = 0.1f;
    public float targetMaskWidth = 380f;

    public bool IsBigBoxModeActive { get; private set; }
    public bool IsAnimating { get; private set; }
    public bool IsVisible { get; private set; }

    public bool IsFadingBox { get { return activeBoxFadeCoroutine != null; } }


    private Coroutine activeDialogueProcessCoroutine = null;
    private Coroutine activeBoxFadeCoroutine = null;

    private BigBoxConfig currentConfig = null;
    private string activeConfigName = "";
    private List<int> linesToAnimateForSkip;

    void Start()
    {
        if (uiBoxSayImage == null) { Debug.LogError("BigDialogueController: 'uiBoxSayImage' no asignado.", gameObject); enabled = false; return; }
        if (bigBoxConfigurations == null || bigBoxConfigurations.Count == 0) { Debug.LogError("BigDialogueController: No se ha definido ninguna 'BigBoxConfiguration'.", gameObject); enabled = false; return; }

        IsBigBoxModeActive = false;
        IsAnimating = false;
        IsVisible = false;

        SetImageAlpha(uiBoxSayImage, 0f);
        uiBoxSayImage.gameObject.SetActive(false);
        foreach (var config in bigBoxConfigurations)
        {
            PrepareLineMasksAndTexts(config, true, true);
        }
    }

    // ===== El método ya no necesita el parámetro 'duration' =====
    public Coroutine ShowBox()
    {
        if (activeBoxFadeCoroutine != null) StopCoroutine(activeBoxFadeCoroutine);
        IsBigBoxModeActive = true;
        // Usamos la variable de la clase 'boxFadeDuration' en lugar de un parámetro.
        activeBoxFadeCoroutine = StartCoroutine(AnimateBoxAlpha(1f, boxFadeDuration));
        return activeBoxFadeCoroutine;
    }

    // ===== El método ya no necesita el parámetro 'duration' =====
    public Coroutine HideBox()
    {
        if (activeDialogueProcessCoroutine != null) { StopCoroutine(activeDialogueProcessCoroutine); activeDialogueProcessCoroutine = null; }
        if (activeBoxFadeCoroutine != null) StopCoroutine(activeBoxFadeCoroutine);

        IsBigBoxModeActive = false;
        IsAnimating = false;
        activeConfigName = ""; // Reseteamos el nombre de la config activa
        currentConfig = null;

        // Limpiamos y cerramos todas las máscaras de todas las configuraciones
        foreach (var config in bigBoxConfigurations)
        {
            PrepareLineMasksAndTexts(config, true, true);
        }

        // Usamos la variable de la clase 'boxFadeDuration' en lugar de un parámetro.
        activeBoxFadeCoroutine = StartCoroutine(AnimateBoxAlpha(0f, boxFadeDuration));
        return activeBoxFadeCoroutine;
    }

    public void DisplayDialogue(string command, string text, float lineDuration)
    {
        if (!IsBigBoxModeActive || !IsVisible) { return; }
        if (activeDialogueProcessCoroutine != null) StopCoroutine(activeDialogueProcessCoroutine);
        activeDialogueProcessCoroutine = StartCoroutine(DisplayDialogueProcess(command, text, lineDuration));
    }

    public void CompleteAnimation()
    {
        if (!IsAnimating || activeDialogueProcessCoroutine == null) return;

        StopCoroutine(activeDialogueProcessCoroutine);
        activeDialogueProcessCoroutine = null;

        if (currentConfig != null && linesToAnimateForSkip != null)
        {
            foreach (int lineIndex in linesToAnimateForSkip)
            {
                if (lineIndex < currentConfig.lineMasks.Count && currentConfig.lineMasks[lineIndex] != null)
                {
                    currentConfig.lineMasks[lineIndex].sizeDelta = new Vector2(targetMaskWidth, currentConfig.lineMasks[lineIndex].sizeDelta.y);
                }
            }
        }

        IsAnimating = false;
    }

    private IEnumerator DisplayDialogueProcess(string command, string text, float lineDuration)
    {
        IsAnimating = true;

        string[] commandParts = command.Split('-');
        if (commandParts.Length < 2)
        {
            Debug.LogError("BigDialogueController: Comando de diálogo inválido: " + command);
            IsAnimating = false;
            yield break;
        }

        string boxNameToFind = commandParts[0];
        List<int> lineIndicesToShow = new List<int>();
        for (int i = 1; i < commandParts.Length; i++)
        {
            string linePart = commandParts[i].ToLower().Replace("line", "");
            int lineIndex;
            if (int.TryParse(linePart, out lineIndex)) { lineIndicesToShow.Add(lineIndex - 1); }
        }
        linesToAnimateForSkip = new List<int>(lineIndicesToShow);

        BigBoxConfig targetConfig = FindConfigByName(boxNameToFind);
        if (targetConfig == null)
        {
            Debug.LogError("BigDialogueController: No se encontró la configuración: " + boxNameToFind);
            IsAnimating = false;
            yield break;
        }

        if (currentConfig != null && currentConfig != targetConfig)
        {
            Debug.Log(string.Format("Cambiando de BigBox '{0}' a '{1}'. Limpiando texto de ambas.", activeConfigName, boxNameToFind));
            PrepareLineMasksAndTexts(currentConfig, true, true); // Limpiar la vieja
            PrepareLineMasksAndTexts(targetConfig, true, true);  // Limpiar la nueva
        }
        activeConfigName = boxNameToFind;
        currentConfig = targetConfig;

        string[] processedLines = GetProcessedLinesArray(text, currentConfig, targetMaskWidth);
        for (int i = 0; i < lineIndicesToShow.Count; i++)
        {
            int lineIndexToUse = lineIndicesToShow[i];
            if (lineIndexToUse < currentConfig.lineTextUIElements.Count && currentConfig.lineTextUIElements[lineIndexToUse] != null)
            {
                if (i < processedLines.Length)
                {
                    currentConfig.lineTextUIElements[lineIndexToUse].text = processedLines[i];
                }
                else
                {
                    currentConfig.lineTextUIElements[lineIndexToUse].text = "";
                }
            }
        }

        float safeLineDuration = Mathf.Max(0.01f, lineDuration);
        foreach (int lineIndex in lineIndicesToShow)
        {
            if (lineIndex < currentConfig.lineMasks.Count && currentConfig.lineMasks[lineIndex] != null)
            {
                RectTransform maskToAnimate = currentConfig.lineMasks[lineIndex];

                // Las otras máscaras no se tocan. NO SE TOCAN!
                maskToAnimate.sizeDelta = new Vector2(0f, maskToAnimate.sizeDelta.y);

                float elapsedTime = 0f;
                while (elapsedTime < safeLineDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float newWidth = Mathf.Lerp(0, targetMaskWidth, elapsedTime / safeLineDuration);
                    maskToAnimate.sizeDelta = new Vector2(newWidth, maskToAnimate.sizeDelta.y);
                    yield return null;
                }
                maskToAnimate.sizeDelta = new Vector2(targetMaskWidth, maskToAnimate.sizeDelta.y);

                if (lineIndicesToShow.Count > 1 && lineIndicesToShow[lineIndicesToShow.Count - 1] != lineIndex)
                {
                    yield return new WaitForSeconds(delayBetweenLines);
                }
            }
        }

        IsAnimating = false;
        activeDialogueProcessCoroutine = null;
    }

    private IEnumerator AnimateBoxAlpha(float targetAlpha, float duration)
    {
        if (uiBoxSayImage == null) yield break;

        IsAnimating = true;

        if (targetAlpha > 0 && !uiBoxSayImage.gameObject.activeSelf) { uiBoxSayImage.gameObject.SetActive(true); }

        float startAlpha = uiBoxSayImage.color.a;
        float elapsedTime = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsedTime < safeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / safeDuration);
            SetImageAlpha(uiBoxSayImage, newAlpha);
            yield return null;
        }

        SetImageAlpha(uiBoxSayImage, targetAlpha);

        if (targetAlpha == 0f) { uiBoxSayImage.gameObject.SetActive(false); IsVisible = false; }
        else { IsVisible = true; }

        activeBoxFadeCoroutine = null;

        IsAnimating = false;
    }

    // --- PrepareLineMasksAndTexts MODIFICADO ---
    private void PrepareLineMasksAndTexts(BigBoxConfig config, bool clearExistingText, bool resetAllMasksToZeroWidth)
    {
        if (config == null || config.lineTextUIElements == null || config.lineMasks == null) return;
        if (config.lineTextUIElements.Count != config.lineMasks.Count) { Debug.LogError("BigDialogueController: Configuración '" + config.boxName + "' con número desigual de máscaras y textos."); return; }
        for (int i = 0; i < config.lineMasks.Count; i++)
        {
            if (config.lineMasks[i] != null)
            {
                if (resetAllMasksToZeroWidth)
                {
                    config.lineMasks[i].sizeDelta = new Vector2(0f, config.lineMasks[i].sizeDelta.y);
                }
                config.lineMasks[i].gameObject.SetActive(true);
            }
            if (i < config.lineTextUIElements.Count && config.lineTextUIElements[i] != null)
            {
                if (clearExistingText)
                {
                    config.lineTextUIElements[i].text = "";
                }
            }
        }
    }
    public void CompleteBoxFade()
    {
        // Si hay una animación de fade en curso, la detenemos.
        if (activeBoxFadeCoroutine != null)
        {
            StopCoroutine(activeBoxFadeCoroutine);
            activeBoxFadeCoroutine = null;
        }

        // Forzamos el estado final basándonos en si el modo BigBox está activo o no.
        if (IsBigBoxModeActive)
        {
            // El objetivo era aparecer, así que la ponemos visible y opaca.
            SetImageAlpha(uiBoxSayImage, 1f);
            uiBoxSayImage.gameObject.SetActive(true);
            IsVisible = true;
        }
        else
        {
            // El objetivo era desaparecer, la ocultamos.
            SetImageAlpha(uiBoxSayImage, 0f);
            uiBoxSayImage.gameObject.SetActive(false);
            IsVisible = false;
        }

        // =====  Nos aseguramos de marcar la animación como finalizada =====
        IsAnimating = false;
        Debug.Log("BigDialogueController: Fade de caja completado por solicitud.");
    }
    private BigBoxConfig FindConfigByName(string name)
    {
        foreach (var config in bigBoxConfigurations)
        {
            if (config.boxName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return config;
            }
        }
        return null;
    }
    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color currentColor = image.color;
        currentColor.a = Mathf.Clamp01(alpha);
        image.color = currentColor;
    }

    //NO SÉ QUE ES ESTO, TENGO MIEDO :,c
    private string[] GetProcessedLinesArray(string originalText, BigBoxConfig config, float availableWidth)
    {
        if (string.IsNullOrEmpty(originalText) || config == null || config.lineTextUIElements.Count == 0 || config.lineTextUIElements[0] == null || availableWidth <= 0)
        {
            return string.IsNullOrEmpty(originalText) ? new string[0] : new string[] { originalText };
        }
        TextGenerator textGen = new TextGenerator();
        TextGenerationSettings settings = config.lineTextUIElements[0].GetGenerationSettings(new Vector2(availableWidth, 0)); // Altura 0 para que no limite
        settings.resizeTextForBestFit = false;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.updateBounds = true;

        List<string> lines = new List<string>();
        string[] words = originalText.Split(' ');
        if (words.Length == 0) return new string[0];

        StringBuilder currentLineBuilder = new StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (currentLineBuilder.Length == 0) { currentLineBuilder.Append(word); }
            else
            {
                string testLine = currentLineBuilder.ToString() + " " + word;
                textGen.Populate(testLine, settings);
                if (textGen.lineCount > 1)
                {
                    lines.Add(currentLineBuilder.ToString());
                    currentLineBuilder.Length = 0;
                    currentLineBuilder.Append(word);
                }
                else { currentLineBuilder.Append(" ").Append(word); }
            }
        }
        if (currentLineBuilder.Length > 0) { lines.Add(currentLineBuilder.ToString()); }
        return lines.ToArray();
    }
    //TQM Gemini
}
