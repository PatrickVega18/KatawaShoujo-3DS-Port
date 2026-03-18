using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class DialogueManager : MonoBehaviour
{
    public Image boxBG;
    public Text nameText, nameF1, nameF2;
    
    [Header("New Optimized System (Typewriter)")]
    public Text[] typewriterLines;

    [Header("BigBoxSay System")]
    public Image bigBoxBG;
    // [Header("BigBoxSay System - Masks Removed")]
    // public RectTransform[] bigBoxMasks;
    // public float bigBoxMaskFullWidth = 380f;
    public Text[] bigBoxLines;
    public GameObject bigBoxTextContainer;
    public float bigBoxFadeDuration = 0.2f;

    [Header("CenterText System")]
    public Text centerTextComponent;
    public bool useCenterText = false;

    public float boxFadeDuration = 0.2f;

    private byte[] memBuffer;
    private MemoryStream ms;
    private BinaryReader mbr;
    private Dictionary<int, long> offsets = new Dictionary<int, long>();
    private Coroutine mainRoutine;
    private Coroutine bbsFadeRoutine;
    private string currentFullText = "";
    private float targetAlpha = 0;
    private string currentNameText = "";
    private string activeBBSBlock = "";
    private string[] currentBBSFragments;
    private List<string> lineStrings = new List<string>();

    public bool isTyping = false;
    public bool isFading = false;
    private List<TextCommand> activeInlineCommands = new List<TextCommand>();

    void Awake()
    {
        SetUIAlpha(0);
        if (boxBG != null) boxBG.gameObject.SetActive(true);
        if (bigBoxBG != null)
        {
            Color c = bigBoxBG.color;
            c.a = 0;
            bigBoxBG.color = c;
            bigBoxBG.gameObject.SetActive(false);
        }
        ClearUI();
    }

    public void LoadLanguage(string fileName)
    {
        if (mbr != null) mbr.Close();
        if (ms != null) ms.Close();

        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path)) return;

        FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        memBuffer = new byte[fs.Length];
        fs.Read(memBuffer, 0, (int)fs.Length);
        fs.Close();

        ms = new MemoryStream(memBuffer);
        mbr = new BinaryReader(ms, Encoding.UTF8);

        int count = mbr.ReadInt32();
        offsets.Clear();
        for (int i = 0; i < count; i++)
        {
            int h = mbr.ReadInt32();
            long o = mbr.ReadInt64();
            offsets.Add(h, o);
        }
    }

    public void UnloadContent()
    {
        StopAllCoroutines();
        if (mbr != null) { mbr.Close(); mbr = null; }
        if (ms != null) { ms.Close(); ms = null; }
        memBuffer = null;
        offsets.Clear();
    }


    public void ExecuteDialogue(int hash, int nodeType = 0)
    {
        if (mainRoutine != null) StopCoroutine(mainRoutine);

        // Limpiar centerText del nodo anterior
        if (centerTextComponent != null && centerTextComponent.gameObject.activeSelf)
        {
            centerTextComponent.text = "";
            centerTextComponent.gameObject.SetActive(false);
        }

        if (hash == 0 || !offsets.ContainsKey(hash))
        {
            if (string.IsNullOrEmpty(activeBBSBlock))
            {
                if (!isFading) StartFade(0);
            }
            return;
        }

        DialogueEntry entry = ReadEntry(hash);
        if (string.IsNullOrEmpty(entry.text))
        {
            if (string.IsNullOrEmpty(activeBBSBlock))
            {
                if (!isFading) StartFade(0);
            }
            return;
        }

        currentFullText = entry.text;
        currentNameText = entry.name;

        if (entry.name.StartsWith("bbs"))
        {
            if (boxBG != null && boxBG.color.a > 0.01f) SetUIAlpha(0);
            ClearTypewriterLines();
            
            ExecuteBigBoxSequence(entry);
            return;
        }

        if (useCenterText)
        {
            mainRoutine = StartCoroutine(CenterTextSequence(entry));
            return;
        }

        mainRoutine = StartCoroutine(DialogueSequence(entry, nodeType));
    }

    private IEnumerator CenterTextSequence(DialogueEntry entry)
    {
        if (centerTextComponent == null) yield break;

        // Ocultar caja de dialogo normal
        if (boxBG != null && boxBG.color.a > 0.01f) SetUIAlpha(0);

        centerTextComponent.text = "";
        centerTextComponent.gameObject.SetActive(true);

        // Esperar a que terminen las animaciones visuales (fondos y sprites)
        if (GameManager.Instance != null)
        {
            while ((GameManager.Instance.backgroundManager != null && GameManager.Instance.backgroundManager.isAnimating) || 
                   (GameManager.Instance.characterSpriteManager != null && GameManager.Instance.characterSpriteManager.isAnimating))
            {
                yield return null;
            }
        }

        isTyping = true;
        float stepRate = entry.speed > 0 ? entry.speed : 0f;

        if (stepRate <= 0)
        {
            centerTextComponent.text = entry.text;
        }
        else
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < entry.text.Length; i++)
            {
                sb.Append(entry.text[i]);
                centerTextComponent.text = sb.ToString();
                yield return new WaitForSeconds(stepRate);
            }
            centerTextComponent.text = entry.text;
        }

        isTyping = false;
        useCenterText = false;
    }

    private void ParseInlineTags(string rawText, out string cleanText, out List<TextCommand> commands)
    {
        cleanText = "";
        commands = new List<TextCommand>();
        int iRaw = 0;
        int cleanIndex = 0;
        while (iRaw < rawText.Length)
        {
            if (rawText[iRaw] == '<')
            {
                int endTag = rawText.IndexOf('>', iRaw);
                if (endTag != -1)
                {
                    string tag = rawText.Substring(iRaw + 1, endTag - iRaw - 1);
                    if (tag.StartsWith("wait-"))
                    {
                        float wTime = 0;
                        float.TryParse(tag.Substring(5), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out wTime);
                        commands.Add(new TextCommand { charIndex = cleanIndex, type = "wait", floatValue = wTime });
                        iRaw = endTag + 1;
                        continue;
                    }
                    else if (tag == "run")
                    {
                        commands.Add(new TextCommand { charIndex = cleanIndex, type = "run" });
                        iRaw = endTag + 1;
                        continue;
                    }
                    else if (tag.StartsWith("Sprite")) 
                    {
                        commands.Add(new TextCommand { charIndex = cleanIndex, type = "sprite", stringValue = tag });
                        iRaw = endTag + 1;
                        continue;
                    }
                }
            }
            cleanText += rawText[iRaw];
            cleanIndex++;
            iRaw++;
        }
    }

    private void ExecuteSpriteTag(string tag)
    {
        if (GameManager.Instance == null || GameManager.Instance.characterSpriteManager == null) return;

        string[] parts = tag.Split('|');
        if (parts.Length < 2) return;

        string spriteIdStr = parts[0].Replace("Sprite", "");
        int slotIdx = 0;
        int.TryParse(spriteIdStr, out slotIdx);
        slotIdx--; // Convert 1-6 to 0-5

        Dictionary<int, string> parameters = new Dictionary<int, string>();
        for (int i = 1; i < parts.Length; i++)
        {
            string p = parts[i];
            int sep = p.IndexOf(':');
            if (sep == -1) sep = p.IndexOf('_'); // Soporte para ambos delimitadores
            
            if (sep != -1)
            {
                int id = 0;
                if (int.TryParse(p.Substring(0, sep), out id))
                {
                    parameters[id] = p.Substring(sep + 1);
                }
            }
        }

        if (parameters.Count > 0)
        {
            GameManager.Instance.characterSpriteManager.ExecuteInlineUpdate(slotIdx, parameters);
        }
    }

    private IEnumerator DialogueSequence(DialogueEntry entry, int nodeType)
    {
        if (!string.IsNullOrEmpty(activeBBSBlock))
        {
            FadeOutBBS();
        }

        string cleanText = entry.text;
        activeInlineCommands.Clear();
        
        // 6 = EventoTexto (Permite Guardar), 7 = EventoTextoEspecial (Bloquea Guardar y lanza Eventos visuales)
        if (nodeType == 6 || nodeType == 7) {
            ParseInlineTags(entry.text, out cleanText, out activeInlineCommands);
            currentFullText = cleanText; // Fix para no imprimir basura en el InstantFinish
        }

        // --- Optimized Line-by-Line Typewriter Layout Calculation ---
        // Debemos calcular esto ANTES de cualquier yield (como el FadeRoutine)
        lineStrings.Clear();
        if (typewriterLines != null && typewriterLines.Length > 0)
        {
            Text tComp = typewriterLines[0];
            if (tComp != null && tComp.cachedTextGenerator != null)
            {
                TextGenerationSettings settings = tComp.GetGenerationSettings(tComp.rectTransform.rect.size);
                tComp.cachedTextGenerator.Populate(cleanText, settings);
                
                IList<UILineInfo> linesInfo = tComp.cachedTextGenerator.lines;
                for (int i = 0; i < linesInfo.Count; i++)
                {
                    int start = linesInfo[i].startCharIdx;
                    int end = (i < linesInfo.Count - 1) ? linesInfo[i + 1].startCharIdx : cleanText.Length;
                    if (start < cleanText.Length)
                    {
                        lineStrings.Add(cleanText.Substring(start, end - start));
                    }
                }
            }
        }

        if (boxBG != null && boxBG.color.a < 0.1f)
        {
            ClearUI();
            isFading = true;
            targetAlpha = 1;
            yield return StartCoroutine(FadeRoutine(1));
        }
        else
        {
            ClearTypewriterLines();
            UpdateNameUI(""); // Asegurar que el nombre anterior se limpie mientras esperamos
        }

        // Esperar a que terminen las animaciones visuales (fondos y sprites)
        if (GameManager.Instance != null)
        {
            while ((GameManager.Instance.backgroundManager != null && GameManager.Instance.backgroundManager.isAnimating) || 
                   (GameManager.Instance.characterSpriteManager != null && GameManager.Instance.characterSpriteManager.isAnimating))
            {
                yield return null;
            }
        }

        UpdateNameUI(entry.name);

        float revealTime = entry.speed > 0 ? entry.speed : 0f;
        isTyping = true;

        ClearTypewriterLines();

        if (revealTime <= 0)
        {
            for (int i = 0; i < lineStrings.Count && i < typewriterLines.Length; i++)
            {
                typewriterLines[i].text = lineStrings[i];
            }
        }
        else
        {
            float stepRate = entry.speed; 
            int globalCharIndex = 0;
            int cmdIndex = 0;

            int nextRunIndex = -1;
            TextCommand nextRunCmd = activeInlineCommands.Find(c => c.type == "run");
            if (nextRunCmd != null) nextRunIndex = nextRunCmd.charIndex;

            for (int i = 0; i < lineStrings.Count && i < typewriterLines.Length; i++)
            {
                if (typewriterLines[i] == null) continue;
                
                string lineText = lineStrings[i];
                StringBuilder sb = new StringBuilder();
                for (int j = 0; j < lineText.Length; j++)
                {
                    while (cmdIndex < activeInlineCommands.Count && activeInlineCommands[cmdIndex].charIndex == globalCharIndex)
                    {
                        TextCommand cmd = activeInlineCommands[cmdIndex];
                        if (cmd.type == "wait") {
                            if (globalCharIndex >= nextRunIndex) {
                                yield return new WaitForSeconds(cmd.floatValue);
                            }
                        } else if (cmd.type == "run") {
                            int currentCmdIdx = cmdIndex;
                            TextCommand nextR = activeInlineCommands.Find(c => c.type == "run" && c.charIndex > activeInlineCommands[currentCmdIdx].charIndex);
                            nextRunIndex = nextR != null ? nextR.charIndex : -1;
                        } else if (cmd.type == "sprite") {
                            // Barrera de seguridad: si ya hay una animación corriendo, esperamos.
                            if (GameManager.Instance != null && GameManager.Instance.characterSpriteManager != null) {
                                while (GameManager.Instance.characterSpriteManager.isAnimating) yield return null;
                            }
                            ExecuteSpriteTag(cmd.stringValue);
                        }
                        cmdIndex++;
                    }

                    sb.Append(lineText[j]);
                    typewriterLines[i].text = sb.ToString();

                    bool isRunning = (globalCharIndex < nextRunIndex);
                    if (!isRunning && stepRate > 0f) {
                        yield return new WaitForSeconds(stepRate);
                    }

                    globalCharIndex++;
                }
                typewriterLines[i].text = lineText; // Aseguramos el final de linea
            }

            while (cmdIndex < activeInlineCommands.Count && activeInlineCommands[cmdIndex].charIndex == globalCharIndex)
            {
                 TextCommand cmd = activeInlineCommands[cmdIndex];
                 if (cmd.type == "wait") {
                     yield return new WaitForSeconds(cmd.floatValue);
                 } else if (cmd.type == "sprite") {
                     if (GameManager.Instance != null && GameManager.Instance.characterSpriteManager != null) {
                         while (GameManager.Instance.characterSpriteManager.isAnimating) yield return null;
                     }
                     ExecuteSpriteTag(cmd.stringValue);
                 }
                 cmdIndex++;
            }

            // Fin de línea: esperar a que el último sprite termine antes de liberar el diálogo.
            if (GameManager.Instance != null && GameManager.Instance.characterSpriteManager != null) {
                while (GameManager.Instance.characterSpriteManager.isAnimating) yield return null;
            }
        }

        // Old Mask Logic (Commented)
        /*
        int lineCount = dialogoLines.Length > 0 ? dialogoLines[0].cachedTextGenerator.lineCount : 0;
        if (lineCount > maskLines.Length) lineCount = maskLines.Length;

        for (int line = 0; line < lineCount; line++)
        {
            if (revealTime <= 0)
            {
                SetMaskWidth(line, maskFullWidth);
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < revealTime)
                {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
                    float w = Mathf.Lerp(0, maskFullWidth, elapsed / revealTime);
                    SetMaskWidth(line, w);
                    yield return null;
                }
                SetMaskWidth(line, maskFullWidth);
            }
        }

        ToggleMasks(false);
        */
        
        isTyping = false;
    }

    private void UpdateNameUI(string n)
    {
        bool hasN = !string.IsNullOrEmpty(n);
        string displayName = n;
        string colorName = n;

        if (hasN)
        {
            if (n.StartsWith("C_"))
            {
                int dashIndex = n.IndexOf('-');
                if (dashIndex > 2)
                {
                    colorName = n.Substring(2, dashIndex - 2);
                    displayName = n.Substring(dashIndex + 1);
                }
            }
            displayName = displayName.Replace("_SPACE_", " ");
        }

        nameText.text = hasN ? displayName : "";
        nameF1.text = nameText.text;
        nameF2.text = nameText.text;

        if (hasN)
        {
            Color c = CharacterColors.GetColor(colorName);
            nameText.color = c; nameF1.color = c; nameF2.color = c;
        }
    }

    private void StartFade(float t)
    {
        targetAlpha = t;
        mainRoutine = StartCoroutine(FadeRoutine(t));
    }

    private IEnumerator FadeRoutine(float target)
    {
        isFading = true;
        float startValue = (boxBG != null) ? boxBG.color.a : 0f;
        float elapsed = 0f;

        while (elapsed < boxFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float newAlpha = Mathf.Lerp(startValue, target, elapsed / boxFadeDuration);
            SetUIAlpha(newAlpha);
            yield return null;
        }

        SetUIAlpha(target);
        if (target <= 0) ClearUI();
        isFading = false;
    }

    private void SetUIAlpha(float alpha)
    {
        if (boxBG != null) { Color c = boxBG.color; c.a = alpha; boxBG.color = c; }

        if (nameText != null) { Color c = nameText.color; c.a = alpha; nameText.color = c; }
        if (nameF1 != null) { Color c = nameF1.color; c.a = alpha; nameF1.color = c; }
        if (nameF2 != null) { Color c = nameF2.color; c.a = alpha; nameF2.color = c; }

        if (typewriterLines != null)
        {
            for (int i = 0; i < typewriterLines.Length; i++)
            {
                if (typewriterLines[i] != null)
                {
                    Color c = typewriterLines[i].color;
                    c.a = alpha;
                    typewriterLines[i].color = c;
                }
            }
        }
    }

    private void ClearUI()
    {
        nameText.text = ""; nameF1.text = ""; nameF2.text = "";
        ClearTypewriterLines();
        if (bigBoxTextContainer != null) bigBoxTextContainer.SetActive(false);
    }

    private void ClearTypewriterLines()
    {
        if (typewriterLines == null) return;
        for (int i = 0; i < typewriterLines.Length; i++)
        {
            if (typewriterLines[i] != null)
                typewriterLines[i].text = "";
        }
    }

    public void InstantFinish()
    {
        if (mainRoutine != null) { StopCoroutine(mainRoutine); mainRoutine = null; }

        if (bbsFadeRoutine != null)
        {
            StopCoroutine(bbsFadeRoutine);
            bbsFadeRoutine = null;
            activeBBSBlock = "";
            isFading = false;
            ClearTypewriterLines();
            return;
        }

        if (!string.IsNullOrEmpty(activeBBSBlock))
        {
            if (bigBoxBG != null && bigBoxBG.gameObject.activeSelf && bigBoxBG.color.a < 0.99f)
            {
                Color c = bigBoxBG.color; c.a = 1f; bigBoxBG.color = c;
            }

            if (currentBBSFragments != null)
            {
                List<int> linesToAnimate = ParseBigBoxSay(currentNameText);
                for (int i = 0; i < currentBBSFragments.Length && i < linesToAnimate.Count; i++)
                {
                    int lineIndex = linesToAnimate[i];
                    if (lineIndex >= 0 && lineIndex < bigBoxLines.Length && bigBoxLines[lineIndex] != null)
                    {
                        EnableBigBoxLine(lineIndex, true);
                        bigBoxLines[lineIndex].text = currentBBSFragments[i];
                    }
                }
            }
            isTyping = false;
            isFading = false;
            ClearTypewriterLines();
        }
        else
        {
            // Ejecutar todos los comandos de sprite restantes instantáneamente
            if (activeInlineCommands != null)
            {
                foreach (var cmd in activeInlineCommands)
                {
                    if (cmd.type == "sprite") ExecuteSpriteTag(cmd.stringValue);
                }
                activeInlineCommands.Clear(); // Evitar ejecuciones duplicadas en frames siguientes
            }

            if (isFading) SetUIAlpha(targetAlpha);
            if (targetAlpha <= 0) ClearUI();
            else
            {
                SetUIAlpha(1);
                UpdateNameUI(currentNameText);

                // Optimized Instant Finish
                for (int i = 0; i < lineStrings.Count && i < typewriterLines.Length; i++)
                {
                    typewriterLines[i].text = lineStrings[i];
                }

                // Old Mask Logic (Commented)
                // SetTextInAllLines(currentFullText);
                // ShowAllMasks();
                // ToggleMasks(false);
            }
        }
        isTyping = false;
        isFading = false;
    }

    private DialogueEntry ReadEntry(int h)
    {
        ms.Seek(offsets[h], SeekOrigin.Begin);
        DialogueEntry e = new DialogueEntry();
        e.name = mbr.ReadString();
        e.text = mbr.ReadString();
        e.speed = mbr.ReadSingle();
        return e;
    }

    public void RestoreInstant(int hash, int nodeType = 0)
    {
        StopAllCoroutines();
        StartCoroutine(RestoreInstantCoroutine(hash, nodeType));
    }

    public IEnumerator RestoreInstantCoroutine(int hash, int nodeType = 0)
    {
        isTyping = false;
        isFading = false;
        bbsFadeRoutine = null;

        // Limpiar centerText instantaneamente
        if (centerTextComponent != null)
        {
            centerTextComponent.text = "";
            centerTextComponent.gameObject.SetActive(false);
        }
        useCenterText = false;

        // Limpiar cualquier BBS activo instantáneamente
        if (bigBoxBG != null) 
        { 
            Color c = bigBoxBG.color; c.a = 0; bigBoxBG.color = c; 
            bigBoxBG.canvasRenderer.SetAlpha(1f);
            bigBoxBG.gameObject.SetActive(false); 
        }
        for (int i = 0; i < bigBoxLines.Length; i++)
        {
            if (bigBoxLines[i] != null)
            {
                Color c = bigBoxLines[i].color; c.a = 0; bigBoxLines[i].color = c;
                bigBoxLines[i].canvasRenderer.SetAlpha(1f);
                bigBoxLines[i].text = "";
            }
        }
        activeBBSBlock = "";

        if (hash == 0 || !offsets.ContainsKey(hash))
        {
            SetUIAlpha(0);
            ClearUI();
            yield break;
        }

        DialogueEntry entry = ReadEntry(hash);
        if (string.IsNullOrEmpty(entry.text))
        {
            SetUIAlpha(0);
            ClearUI();
            yield break;
        }

        // Empezamos invisibles para la restauración suave
        SetUIAlpha(0);
        targetAlpha = 1;

        string cleanText = entry.text;
        activeInlineCommands.Clear();     
        if (nodeType == 6 || nodeType == 7) {
            ParseInlineTags(entry.text, out cleanText, out activeInlineCommands);
        }

        currentFullText = cleanText;
        currentNameText = entry.name;
        UpdateNameUI(entry.name);
        
        // Optimized Restore Instant
        if (typewriterLines != null && typewriterLines.Length > 0)
        {
            lineStrings.Clear();
            Text tComp = typewriterLines[0];
            if (tComp != null && tComp.cachedTextGenerator != null)
            {
                TextGenerationSettings settings = tComp.GetGenerationSettings(tComp.rectTransform.rect.size);
                tComp.cachedTextGenerator.Populate(cleanText, settings);
                
                IList<UILineInfo> linesInfo = tComp.cachedTextGenerator.lines;
                string full = cleanText;
                for (int i = 0; i < linesInfo.Count; i++)
                {
                    int start = linesInfo[i].startCharIdx;
                    int end = (i < linesInfo.Count - 1) ? linesInfo[i + 1].startCharIdx : full.Length;
                    if (start < full.Length) lineStrings.Add(full.Substring(start, end - start));
                }
            }

            // Al cargar partida, procesamos todos los tags para posicionar a los personajes
            foreach (var cmd in activeInlineCommands)
            {
                if (cmd.type == "sprite") ExecuteSpriteTag(cmd.stringValue);
            }

            ClearTypewriterLines();
            for (int i = 0; i < lineStrings.Count && i < typewriterLines.Length; i++)
            {
                typewriterLines[i].text = lineStrings[i];
            }
        }

        if (entry.name.StartsWith("bbs"))
        {
            RestoreBigBoxInstant(entry);
            // El BBS suele necesitar un fundido propio o ya lo maneja su método.
            // Pero esperaremos igual para el respiro.
            yield return null;
        }
        else
        {
            // Esperamos respiro de frames tras RestoreSnapshot
            yield return null;
            yield return null;
            
            // Aparecer suavemente (Usando el FadeRoutine interno que ya maneja boxBG y textos)
            yield return StartCoroutine(FadeRoutine(1.0f));
        }
    }

    public void FadeOutBBS()
    {
        if (bbsFadeRoutine != null) StopCoroutine(bbsFadeRoutine);
        bbsFadeRoutine = StartCoroutine(FadeOutBBSRoutine(bigBoxFadeDuration));
    }

    private IEnumerator FadeOutBBSRoutine(float duration)
    {
        isFading = true;
        ClearTypewriterLines();

        if (bigBoxBG != null && bigBoxBG.gameObject.activeSelf)
        {
            bigBoxBG.canvasRenderer.SetAlpha(bigBoxBG.color.a);
            bigBoxBG.CrossFadeAlpha(0f, duration, true);
        }

        for (int i = 0; i < bigBoxLines.Length; i++)
        {
            if (bigBoxLines[i] != null && bigBoxLines[i].gameObject.activeSelf)
            {
                bigBoxLines[i].canvasRenderer.SetAlpha(bigBoxLines[i].color.a);
                bigBoxLines[i].CrossFadeAlpha(0f, duration, true);
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (bigBoxBG != null) 
        { 
            Color c = bigBoxBG.color; c.a = 0; bigBoxBG.color = c; 
            bigBoxBG.canvasRenderer.SetAlpha(1f);
            bigBoxBG.gameObject.SetActive(false); 
        }
        
        for (int i = 0; i < bigBoxLines.Length; i++)
        {
            if (bigBoxLines[i] != null)
            {
                Color c = bigBoxLines[i].color; c.a = 0; bigBoxLines[i].color = c;
                bigBoxLines[i].canvasRenderer.SetAlpha(1f);
                bigBoxLines[i].text = "";
            }
        }
        
        activeBBSBlock = "";
        bbsFadeRoutine = null;
        isFading = false;
    }

    public void ClearReferences()
    {
        ClearTypewriterLines();
        if (bigBoxLines != null)
        {
            for (int i = 0; i < bigBoxLines.Length; i++)
                if (bigBoxLines[i] != null) bigBoxLines[i].text = "";
        }
        currentFullText = "";
        currentNameText = "";
        activeBBSBlock = "";
    }

    void OnDestroy()
    {
        UnloadContent();
    }

    // --- BIG BOX SAY (NVL MODE) LOGIC ---
    public void ExecuteBigBoxSequence(DialogueEntry entry)
    {
        if (mainRoutine != null) StopCoroutine(mainRoutine);
        
        currentFullText = entry.text;
        mainRoutine = StartCoroutine(BigBoxSequence(entry));
    }
    private IEnumerator BigBoxSequence(DialogueEntry entry)
    {
        // Preparar datos ANTES del fadeIn para que InstantFinish pueda revelarlos si se interrumpe
        List<int> linesToAnimate = ParseBigBoxSay(entry.name);
        currentBBSFragments = entry.text.Replace("\\n", "\n").Split('\n');
        currentNameText = entry.name;
        float revealTime = entry.speed > 0 ? entry.speed : 0f;

        if (bigBoxBG != null && bigBoxBG.color.a < 0.1f)
        {
            isFading = true;
            bigBoxBG.color = new Color(bigBoxBG.color.r, bigBoxBG.color.g, bigBoxBG.color.b, 0f);
            bigBoxBG.gameObject.SetActive(true);
            yield return StartCoroutine(UIAnimationHelper.FadeImage(bigBoxBG, 0f, 1f, bigBoxFadeDuration));
            isFading = false;
        }

        if (bigBoxTextContainer != null) bigBoxTextContainer.SetActive(true);

        // Esperar a que terminen las animaciones visuales (fondos y sprites)
        if (GameManager.Instance != null)
        {
            while ((GameManager.Instance.backgroundManager != null && GameManager.Instance.backgroundManager.isAnimating) || 
                   (GameManager.Instance.characterSpriteManager != null && GameManager.Instance.characterSpriteManager.isAnimating))
            {
                yield return null;
            }
        }

        isTyping = true;

        if (linesToAnimate.Count > 0)
        {
            for (int i = 0; i < currentBBSFragments.Length && i < linesToAnimate.Count; i++)
            {
                int lineIndex = linesToAnimate[i];
                string fragment = currentBBSFragments[i];
                
                EnableBigBoxLine(lineIndex, true);
                
                if (revealTime <= 0)
                {
                    bigBoxLines[lineIndex].text = fragment;
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    for (int j = 0; j < fragment.Length; j++)
                    {
                        sb.Append(fragment[j]);
                        bigBoxLines[lineIndex].text = sb.ToString();
                        yield return new WaitForSeconds(revealTime);
                    }
                    bigBoxLines[lineIndex].text = fragment;
                }
            }
        }

        isTyping = false;
    }

    private void EnableBigBoxLine(int index, bool active)
    {
        if (bigBoxLines != null && index >= 0 && index < bigBoxLines.Length && bigBoxLines[index] != null)
        {
            bigBoxLines[index].gameObject.SetActive(active);
            bigBoxLines[index].enabled = active;
        }
    }

    private void RestoreBigBoxInstant(DialogueEntry entry)
    {
        if (bigBoxTextContainer != null) bigBoxTextContainer.SetActive(true);
        List<int> linesToAnimate = ParseBigBoxSay(entry.name);
        
        if (linesToAnimate.Count > 0)
        {
            currentBBSFragments = entry.text.Replace("\\n", "\n").Split('\n');

            for (int i = 0; i < currentBBSFragments.Length && i < linesToAnimate.Count; i++)
            {
                int lineIndex = linesToAnimate[i];
                bigBoxLines[lineIndex].text = currentBBSFragments[i];
                EnableBigBoxLine(lineIndex, true);
            }
        }
    }

    private List<int> ParseBigBoxSay(string command)
    {
        List<int> activatedLines = new List<int>();

        if (string.IsNullOrEmpty(command) || bigBoxLines == null) return activatedLines;

        string safeCommand = command.Replace("-line", "|line");
        string[] parts = safeCommand.Split('|');
        if (parts.Length == 0) return activatedLines;

        string blockID = parts[0];

        if (blockID != activeBBSBlock)
        {
            activeBBSBlock = blockID;
            if (bigBoxLines != null)
            {
                for (int i = 0; i < bigBoxLines.Length; i++)
                {
                    if (bigBoxLines[i] != null)
                    {
                        Color c = bigBoxLines[i].color;
                        c.a = 0;
                        bigBoxLines[i].color = c;
                        bigBoxLines[i].text = "";
                    }
                    EnableBigBoxLine(i, false);
                }
            }
        }

        for (int i = 1; i < parts.Length; i++)
        {
            string[] sub = parts[i].Split('_');
            if (sub.Length == 2)
            {
                string lineNumStr = sub[0].Replace("line", "");
                int lineIndex = -1;
                int.TryParse(lineNumStr, out lineIndex);
                lineIndex -= 1;

                float posY = 0f;
                float.TryParse(sub[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out posY);

                if (lineIndex >= 0 && lineIndex < bigBoxLines.Length && bigBoxLines[lineIndex] != null)
                {
                    Vector2 anchoredPos = bigBoxLines[lineIndex].rectTransform.anchoredPosition;
                    anchoredPos.y = posY;
                    bigBoxLines[lineIndex].rectTransform.anchoredPosition = anchoredPos;
                    
                    Color c = bigBoxLines[lineIndex].color;
                    c.a = 1;
                    bigBoxLines[lineIndex].color = c;

                    activatedLines.Add(lineIndex);
                }
            }
        }

        return activatedLines;
    }
}

public class TextCommand
{
    public int charIndex;
    public string type;
    public float floatValue;
    public string stringValue;
}