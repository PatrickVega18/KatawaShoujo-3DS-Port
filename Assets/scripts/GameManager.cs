using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class GameManager : MonoBehaviour
{
    public ScriptManager scriptManager;
    public BackgroundManager backgroundManager;
    public CharacterSpriteManager characterSpriteManager;
    public DialogueManager dialogueManager;
    public MusicManager musicManager;
    public EventManager eventManager;
    public SFXManager sfxManager;
    public DecisionManager decisionManager;
    public MenuManager menuManager;
    public Camera upperCamera;
    public Canvas[] upperCanvases;

    public string currentAct = "prueba"; 
    public string currentLanguage = "ES";
    public string startNodeID = "prueba_01";
    [HideInInspector] public bool IsGamePaused = false;
    public string currentSceneDescription = "";
    public string currentActDescription = "";
    public bool persistentSaveLock = false;
    public bool inputLock = false;
    public float sessionPlayTime = 0f;
    private int lastSceneHash = 0;
    [HideInInspector] public bool isShuttingDown = false;

    private EventData currentEvent;
    private bool hasEventActive = false;
    private string loadedAct = "";

    private Dictionary<int, string> actoEscenaActo = new Dictionary<int, string>();
    private Dictionary<int, string> actoEscenaEscena = new Dictionary<int, string>();

    public static GameManager Instance;

    void Awake()
    {
        Instance = this;
    }

    private bool firstRun = true;

    void OnEnable()
    {
        // Si no es la primera vez que se activa, necesitamos reiniciar la lógica de juego
        // ya que Unity no vuelve a ejecutar Start() en un objeto que ya existía.
        if (!firstRun)
        {
            StartCoroutine(InitializeGameRoutine());
        }
        firstRun = false;
    }

    private IEnumerator Start()
    {
        // La primera ejecución la maneja Unity automáticamente
        yield return StartCoroutine(InitializeGameRoutine());
    }

    private IEnumerator InitializeGameRoutine()
    {
        isShuttingDown = false;
        inputLock = true;
        if (menuManager != null) menuManager.ShowOpeningCurtain();
        yield return null;

        if (LocalizationManager.Instance != null)
            currentLanguage = LocalizationManager.Instance.currentLanguage;

        // Cargar miniaturas al entrar al juego (para el menú de pausa)
        if (backgroundManager != null) backgroundManager.LoadThumbnailBundle("fondos_thumbnail");

        SaveBody pendingBody = default(SaveBody);
        bool hasPendingSave = false;

        if (SceneTransferData.shouldLoadSave && SceneTransferData.slotToLoad >= 0)
        {
            pendingBody = SaveSystem.LoadBody(SceneTransferData.slotToLoad);
            sessionPlayTime = SceneTransferData.playTimeToLoad;
            
            if (!string.IsNullOrEmpty(pendingBody.currentActName))
            {
                currentAct = pendingBody.currentActName;
                hasPendingSave = true;
            }
        }

        yield return StartCoroutine(LoadActResourcesStaged(true)); // Forzamos carga fresca

        if (hasPendingSave)
        {
            SceneTransferData.shouldLoadSave = false;
            SceneTransferData.slotToLoad = -1;
            yield return StartCoroutine(RestoreSnapshot(pendingBody));
        }
        else
        {
            GlobalVariablesManager.Instance.ResetState();
            int startHash = HashHelper.GetID(startNodeID);
            ExecuteNode(startHash);
        }

        // Esto evita el tirón justo cuando empieza el fadeOut
        yield return new WaitForSecondsRealtime(0.5f);

        if (menuManager != null) yield return StartCoroutine(menuManager.HideOpeningCurtain());
        inputLock = false;
    }

    private IEnumerator LoadActResourcesStaged(bool forceUnload = false)
    {
        if (currentAct == loadedAct && !forceUnload) yield break;

        // FASE 1: DESCARGA GRANULAR (Antes esto era síncrono o incompleto)
        yield return StartCoroutine(UnloadAllResources());

        loadedAct = currentAct;

        // FASE 2: CARGA (Load paso por paso, sin GC innecesarios)
        scriptManager.Init(
            currentAct + "_guion.dat", 
            currentAct + "_eventos.dat", 
            currentAct + "_decisiones_" + currentLanguage + ".dat"
        );
        dialogueManager.LoadLanguage(currentAct + "_dialogos_" + currentLanguage + ".dat");
        LoadActoEscena(currentAct + "_actoescena_" + currentLanguage + ".dat");
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.LoadActSceneCache(currentAct);
        yield return null; // Pausa para estabilizar RAM

        musicManager.LoadBundle(currentAct + "_musica");
        yield return null;

        sfxManager.LoadBundle(currentAct + "_sfx");
        yield return null;

        backgroundManager.LoadBundle(currentAct + "_fondos");
        backgroundManager.LoadEventosBundle(currentAct + "_eventosfondos");
        yield return null;

        characterSpriteManager.LoadBundle(currentAct + "_sprites");
        yield return null;

        if (eventManager != null) eventManager.LoadBundle(currentAct + "_vfx");
        yield return null;
    }


    public void LoadActResources()
    {
        if (currentAct == loadedAct) return;

        // FASE 1: DESCARGA
        if (musicManager != null) musicManager.UnloadContent();
        if (sfxManager != null) sfxManager.UnloadContent();
        if (backgroundManager != null) backgroundManager.UnloadContent();
        if (characterSpriteManager != null) characterSpriteManager.UnloadContent();
        if (eventManager != null) eventManager.UnloadContent();

        if (scriptManager != null) scriptManager.UnloadContent();
        if (dialogueManager != null) dialogueManager.UnloadContent();
        actoEscenaActo.Clear();
        actoEscenaEscena.Clear();

        loadedAct = currentAct;

        // FASE 2: CARGA
        scriptManager.Init(
            currentAct + "_guion.dat", 
            currentAct + "_eventos.dat", 
            currentAct + "_decisiones_" + currentLanguage + ".dat"
        );
        dialogueManager.LoadLanguage(currentAct + "_dialogos_" + currentLanguage + ".dat");
        LoadActoEscena(currentAct + "_actoescena_" + currentLanguage + ".dat");

        musicManager.LoadBundle(currentAct + "_musica");
        sfxManager.LoadBundle(currentAct + "_sfx");
        backgroundManager.LoadBundle(currentAct + "_fondos");
        backgroundManager.LoadEventosBundle(currentAct + "_eventosfondos");
        characterSpriteManager.LoadBundle(currentAct + "_sprites");
        if (eventManager != null) eventManager.LoadBundle(currentAct + "_vfx");
    }

    private void LoadActoEscena(string fileName)
    {
        actoEscenaActo.Clear();
        actoEscenaEscena.Clear();
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path)) return;

        using (BinaryReader r = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8))
        {
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int hash = r.ReadInt32();
                string acto = r.ReadString();
                string escena = r.ReadString();
                if (!actoEscenaActo.ContainsKey(hash)) actoEscenaActo[hash] = acto;
                if (!actoEscenaEscena.ContainsKey(hash)) actoEscenaEscena[hash] = escena;
            }
        }
    }

    void Update()
    {
        if (isShuttingDown) 
        {
            #if UNITY_N3DS && !UNITY_EDITOR
            // Drenar solo botones críticos durante la transición para evitar sobrecarga del bus
            UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A);
            UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.B);
            UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Start);
            #endif
            return;
        }

        if (!IsGamePaused)
        {
            sessionPlayTime += Time.deltaTime;
        }

        if (inputLock) return;

        bool startTrigger = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.S);
        #if UNITY_N3DS && !UNITY_EDITOR
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Start)) startTrigger = true;
        #endif
        
        if (startTrigger)
        {
            if (menuManager != null) { if (IsGamePaused) menuManager.CloseMenu(); else menuManager.OpenMenu(); }
            return;
        }

        if (IsGamePaused || (decisionManager != null && decisionManager.isDecisionActive)) return;

        bool aTrigger = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Return);
        #if UNITY_N3DS && !UNITY_EDITOR
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A)) aTrigger = true;
        #endif
        
        if (aTrigger) HandleInput();

        if (hasEventActive && !IsSystemBusy())
        {
            int t = currentEvent.eventType;
            if (currentEvent.auto == 1 || t == 6 || currentEvent.lowerType == 6 || t == 9 || t == 8 || t == 10 || t == 11)
            {
                TryAdvance();
            }
        }
    }

    public void HandleTouchMenu()
    {
        if (inputLock) return;
        if (menuManager != null) 
        { 
            if (IsGamePaused) menuManager.CloseMenu(); 
            else menuManager.OpenMenu(); 
        }
    }

    public void HandleTouchText()
    {
        if (inputLock || IsGamePaused || (decisionManager != null && decisionManager.isDecisionActive)) return;
        HandleInput();
    }

    private bool IsSystemBusy() {
        if (backgroundManager.isAnimating || characterSpriteManager.isAnimating) return true;
        if (dialogueManager.isTyping || dialogueManager.isFading) return true;
        if (eventManager.isVisualBusy) return true;
        if (hasEventActive && currentEvent.sfxWait == 1 && sfxManager.isPlaying) return true;
        return false;
    }

    private void HandleInput() { if (dialogueManager.useCenterText) return; if (IsSystemBusy()) ForceFinishAll(); else TryAdvance(); }

    private void ForceFinishAll() {
        dialogueManager.InstantFinish();
        backgroundManager.InstantFinish();
        characterSpriteManager.InstantFinish();
        if (hasEventActive) eventManager.InstantFinish(currentEvent.forzado);
    }

    public IEnumerator UnloadAllResources()
    {
        // FASE 0: FadeOut Audio Coordinado (1s)
        if (musicManager != null) musicManager.ExecuteCommand(new MusicData { actionType = 2, time = 1.0f });
        if (sfxManager != null) sfxManager.ExecuteSFX(0, 3, 0, 1.0f);
        yield return new WaitForSecondsRealtime(1.1f);

        // FASE 1: Nulificación de Clips Audio
        if (musicManager != null) musicManager.ClearReferences();
        if (sfxManager != null) sfxManager.ClearReferences();
        yield return null;

        // FASE 2: Nulificación de Fondos
        if (backgroundManager != null) { backgroundManager.ClearReferences(); yield return null; }

        // FASE 3: Nulificación Escalonada de Sprites (Personajes 1-6)
        if (characterSpriteManager != null)
        {
            for (int i = 0; i < 6; i++) {
                characterSpriteManager.ClearSlotReferences(i);
                yield return null;
            }
        }

        // FASE 4: Nulificación de Texto (Diálogo y BigBox)
        if (dialogueManager != null) { dialogueManager.ClearReferences(); yield return null; }

        // FASE 5: Nulificación de Decisiones
        if (decisionManager != null) { decisionManager.ClearReferences(); yield return null; }

        // FASE 6: Nulificación de VFX
        if (eventManager != null) { eventManager.ClearVFXReferences(); yield return null; }

        // FASE 7: Limpieza de Cachés Estáticas
        if (LocalizationManager.Instance != null) { LocalizationManager.Instance.ClearCaches(); yield return null; }

        // FASE 8: Descarga de AssetBundles (Unload Content escalonado)
        if (backgroundManager != null) 
        { 
            backgroundManager.UnloadContent(); 
            yield return null; 
        }
        if (characterSpriteManager != null) { characterSpriteManager.UnloadContent(); yield return null; }
        if (musicManager != null) { musicManager.UnloadContent(); yield return null; }
        if (sfxManager != null) { sfxManager.UnloadContent(); yield return null; }
        if (eventManager != null) { eventManager.UnloadContent(); yield return null; }
        
        if (scriptManager != null) { scriptManager.UnloadContent(); yield return null; }
        if (dialogueManager != null) { dialogueManager.UnloadContent(); yield return null; }
        actoEscenaActo.Clear();
        actoEscenaEscena.Clear();

        // FASE Final: Limpieza PROFUNDA y ÚNICA
        System.GC.Collect();
        yield return Resources.UnloadUnusedAssets();
        
        // Pequeño respiro para que el hardware de Old 3DS se asiente
        yield return new WaitForSeconds(0.3f);
    }

    private void TryAdvance() { if (scriptManager.nodeActual.nextIdHash != 0) ExecuteNode(scriptManager.nodeActual.nextIdHash); }

    public void OnDecisionMade(int nextHash) { StartCoroutine(InputCooldown()); if (nextHash != 0) ExecuteNode(nextHash); else if (scriptManager.nodeActual.nextIdHash != 0) ExecuteNode(scriptManager.nodeActual.nextIdHash); }

    private IEnumerator InputCooldown() { inputLock = true; yield return new WaitForSeconds(0.2f); inputLock = false; }

    void ExecuteNode(int hash) {
        scriptManager.UpdateCache(hash);
        if (scriptManager.nodeActual.idHash == 0) return;

        string ae;
        if (actoEscenaEscena.TryGetValue(hash, out ae) && !string.IsNullOrEmpty(ae)) {
            currentSceneDescription = ae;
            lastSceneHash = hash;
        }
        string aa;
        if (actoEscenaActo.TryGetValue(hash, out aa) && !string.IsNullOrEmpty(aa)) currentActDescription = aa;

        if (scriptManager.nodeActual.nodeType == 3) {
            bool result = GlobalVariablesManager.Instance.CheckCondition(scriptManager.nodeActual.checkVarName, scriptManager.nodeActual.checkCond);
            Debug.Log("[GameManager] Check Node evaluated: " + scriptManager.nodeActual.checkVarName + " " + scriptManager.nodeActual.checkCond + " = " + result);
            ExecuteNode(result ? scriptManager.nodeActual.nextTrueHash : scriptManager.nodeActual.nextFalseHash);
            return;
        }

        if (scriptManager.nodeActual.nodeType == 2) {
            if (decisionManager != null) { decisionManager.StartDecision(scriptManager.GetDecisionData(scriptManager.nodeActual.idHash)); ProcessVisualsAndAudio(); }
            return; 
        }
        
        if (scriptManager.nodeActual.nodeType == 4) {
            StartCoroutine(ChangeActSequence(scriptManager.nodeActual.checkVarName));
            return;
        }

        if (scriptManager.nodeActual.nodeType == 5) {
            StartCoroutine(VideoSequence(scriptManager.nodeActual.checkVarName));
            return;
        }

        if (scriptManager.nodeActual.nodeType == 8) {
            StartCoroutine(GoToMenuDirectCoroutine());
            return;
        }

        ProcessVisualsAndAudio();

        if (hasEventActive && !IsSystemBusy())
        {
            int t = currentEvent.eventType;
            if (currentEvent.auto == 1 || t == 6 || currentEvent.lowerType == 6 || t == 9 || t == 8 || t == 10 || t == 11)
            {
                TryAdvance();
            }
        }
    }

    private void ProcessVisualsAndAudio() {
        hasEventActive = (scriptManager.nodeActual.isEvent == 1);
        if (hasEventActive) { currentEvent = scriptManager.GetEventData(scriptManager.nodeActual.idHash); eventManager.ExecuteEvent(currentEvent); }
        backgroundManager.ExecuteCommand(scriptManager.nodeActual.principal, scriptManager.nodeActual.secundario);
        characterSpriteManager.ExecuteAllCharacters(scriptManager.nodeActual);
        musicManager.ExecuteCommand(scriptManager.nodeActual.music);
        
        if (scriptManager.nodeActual.music.actionType == 3) {
            sfxManager.FadeLoopVolume(scriptManager.nodeActual.music.targetVolume, scriptManager.nodeActual.music.time);
        }

        dialogueManager.ExecuteDialogue(scriptManager.nodeActual.idHash, scriptManager.nodeActual.nodeType);
    }

    public bool IsSaveLocked() 
    {
        if (hasEventActive && currentEvent.eventType == 5)
            return persistentSaveLock;

        return hasEventActive || persistentSaveLock; 
    }

    public string GetCurrentSceneName() { return currentSceneDescription; }
    public string GetCurrentActDescription() { return currentActDescription; }
    public int GetCurrentBGHash() { return backgroundManager.GetActiveVisibleHash(); }
    public int GetLastSceneHash() { return lastSceneHash; }

    public SaveBody GetSaveSnapshot()
    {
        ForceFinishAll();
        SaveBody body = new SaveBody();
        body.currentNodeHash = scriptManager.nodeActual.idHash;
        body.currentActName = currentAct;
        body.currentActDescription = currentActDescription;
        body.currentSceneDescription = currentSceneDescription;
        body.lastSceneHash = lastSceneHash;

        body.bgMainHash = backgroundManager.GetMainHash();
        body.bgSubHash = backgroundManager.GetSubHash();
        body.bgMainAlpha = backgroundManager.GetMainAlpha();
        body.bgSubAlpha = backgroundManager.GetSubAlpha();
        body.bgMainActive = backgroundManager.GetMainActive();
        body.bgSubActive = backgroundManager.GetSubActive();
        body.bgIsSwapped = backgroundManager.GetIsSwapped();

        Vector2 ms = backgroundManager.GetMainSize();
        Vector2 ss = backgroundManager.GetSubSize();
        body.bgMainSizeX = ms.x;
        body.bgMainSizeY = ms.y;
        body.bgSubSizeX = ss.x;
        body.bgSubSizeY = ss.y;

        Vector2 mp = backgroundManager.GetMainPos();
        Vector2 sp = backgroundManager.GetSubPos();
        body.bgMainPosX = mp.x;
        body.bgMainPosY = mp.y;
        body.bgSubPosX = sp.x;
        body.bgSubPosY = sp.y;

        body.musicHash = musicManager.GetCurrentHash();
        body.musicVolume = musicManager.GetCurrentVolume();

        int sfxH; float sfxV;
        sfxManager.GetLoopState(out sfxH, out sfxV);
        body.sfxLoopHash = sfxH;
        body.sfxLoopVolume = sfxV;

        body.counterKeys = GlobalVariablesManager.Instance.GetCounterKeys();
        body.counterValues = GlobalVariablesManager.Instance.GetCounterValues();
        body.flagKeys = GlobalVariablesManager.Instance.GetFlagKeys();
        body.flagValues = GlobalVariablesManager.Instance.GetFlagValues();

        body.spriteStates = characterSpriteManager.GetCurrentStates();

        body.vfxActive = eventManager.currentVfxActive;
        body.vfxSpriteHash = eventManager.currentVfxSpriteHash;
        body.vfxAlpha = eventManager.currentVfxAlpha;
        body.vfxSizeX = eventManager.currentVfxSize.x;
        body.vfxSizeY = eventManager.currentVfxSize.y;
        body.vfxPosX = eventManager.currentVfxPos.x;
        body.vfxPosY = eventManager.currentVfxPos.y;

        body.snowActive = (eventManager.snowCanvas != null) ? eventManager.snowCanvas.activeSelf : false;

        return body;
    }

    public IEnumerator RestoreSnapshot(SaveBody body)
    {
        ForceFinishAll();
        // Eliminamos musicManager.StopImmediate() y sfxManager.StopAll() 
        // para permitir que UnloadAllResources haga el fade-out de 1s suave.
        eventManager.StopAll();

        currentAct = body.currentActName;
        GlobalVariablesManager.Instance.ResetState();
        for (int i = 0; i < body.counterKeys.Count; i++) GlobalVariablesManager.Instance.ModifyCounter(body.counterKeys[i], body.counterValues[i]);
        for (int i = 0; i < body.flagKeys.Count; i++) GlobalVariablesManager.Instance.SetFlag(body.flagKeys[i], body.flagValues[i]);

        // Forzamos descarga granular para asegurar el fade-out de audio y limpieza de RAM
        yield return StartCoroutine(LoadActResourcesStaged(true));

        backgroundManager.RestoreInstant(body.bgMainHash, body.bgSubHash, body.bgMainAlpha, body.bgSubAlpha, body.bgMainActive, body.bgSubActive, body.bgIsSwapped, new Vector2(body.bgMainSizeX, body.bgMainSizeY), new Vector2(body.bgSubSizeX, body.bgSubSizeY), new Vector2(body.bgMainPosX, body.bgMainPosY), new Vector2(body.bgSubPosX, body.bgSubPosY));
        yield return StartCoroutine(characterSpriteManager.RestoreStatesCoroutine(body.spriteStates));

        scriptManager.UpdateCache(body.currentNodeHash);
        
        if (!string.IsNullOrEmpty(body.currentActDescription)) currentActDescription = body.currentActDescription;
        if (!string.IsNullOrEmpty(body.currentSceneDescription)) currentSceneDescription = body.currentSceneDescription;

        lastSceneHash = body.lastSceneHash;

        if (LocalizationManager.Instance != null && lastSceneHash != 0)
        {
            // Forzar actualización localizada del nombre de la escena/acto usando el hash restaurado
            currentSceneDescription = LocalizationManager.Instance.GetSceneName(currentAct, lastSceneHash, currentSceneDescription);
        }

        if (scriptManager.nodeActual.idHash != 0)
        {
            string ae;
            if (actoEscenaEscena.TryGetValue(body.currentNodeHash, out ae) && !string.IsNullOrEmpty(ae))
                currentSceneDescription = ae;
            string aa;
            if (actoEscenaActo.TryGetValue(body.currentNodeHash, out aa) && !string.IsNullOrEmpty(aa))
                currentActDescription = aa;
        }
        
        lastSceneHash = body.lastSceneHash;
        
        // Restauración de Diálogo y Decisiones (ahora se esperan para evitar tirones)
        yield return StartCoroutine(dialogueManager.RestoreInstantCoroutine(body.currentNodeHash, scriptManager.nodeActual.nodeType));

        if (decisionManager != null)
        {
            if (scriptManager.nodeActual.nodeType == 2)
            {
                yield return StartCoroutine(decisionManager.RestoreDecisionCoroutine(scriptManager.GetDecisionData(scriptManager.nodeActual.idHash)));
                ProcessVisualsAndAudio();
            }
            else
            {
                decisionManager.HideDecision();
            }
        }
        
        // Pequeño respiro extra para que las corrutinas de UI se sincronicen tras la carga pesada
        yield return null;
        yield return null;

        musicManager.RestoreInstant(body.musicHash, body.musicVolume);
        sfxManager.RestoreLoop(body.sfxLoopHash, body.sfxLoopVolume);
        eventManager.RestoreVfx(body.vfxActive, body.vfxSpriteHash, body.vfxAlpha, new Vector2(body.vfxSizeX, body.vfxSizeY), new Vector2(body.vfxPosX, body.vfxPosY));

        if (eventManager.snowCanvas != null) eventManager.snowCanvas.SetActive(body.snowActive);

        hasEventActive = false;

        yield return new WaitForSecondsRealtime(0.5f);
    }

    private IEnumerator ChangeActSequence(string newAct)
    {
        inputLock = true;

        // Detener coroutines pendientes del EventManager para evitar crashes
        ForceFinishAll();
        eventManager.StopAllCoroutines();
        eventManager.isVisualBusy = false;

        currentAct = newAct;
        
        // Carga controlada: No destruye lo que hay en pantalla (BackgroundManager mantiene los sprites actuales)
        // pero descarga los AssetBundles antiguos para liberar RAM antes de cargar los nuevos.
        yield return StartCoroutine(LoadActResourcesStaged());
        
        // Saltamos al primer nodo del nuevo guion
        ExecuteNode(scriptManager.firstNodeHash);
        
        // Nota: inputLock permanece activo hasta un comando 'restoreInputs' futuro
    }

    private IEnumerator VideoSequence(string videoName)
    {
        inputLock = true;

        // Detener coroutines pendientes del EventManager
        ForceFinishAll();
        eventManager.StopAllCoroutines();
        eventManager.isVisualBusy = false;

        string videoPath = Path.Combine(Application.streamingAssetsPath, videoName + ".moflex");
        
        if (File.Exists(videoPath))
        {
            // Desactivar Canvas de la pantalla superior para que el video se vea
            for (int i = 0; i < upperCanvases.Length; i++)
                if (upperCanvases[i] != null) upperCanvases[i].enabled = false;

            UnityEngine.N3DS.Video.Play(videoPath, N3dsScreen.Top);
            
            yield return new WaitForSeconds(0.5f);

            #if UNITY_N3DS && !UNITY_EDITOR
            UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A);
            UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Start);
            #endif

            while (UnityEngine.N3DS.Video.IsPlaying)
            {
                bool skip = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.S);
                #if UNITY_N3DS && !UNITY_EDITOR
                if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A) || UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Start)) skip = true;
                #endif

                if (skip)
                {
                    UnityEngine.N3DS.Video.Stop();
                    break;
                }
                yield return null;
            }

            // Restaurar Canvas
            for (int i = 0; i < upperCanvases.Length; i++)
                if (upperCanvases[i] != null) upperCanvases[i].enabled = true;
        }

        inputLock = false;
        TryAdvance();
    }

    private IEnumerator GoToMenuDirectCoroutine()
    {
        isShuttingDown = true;
        inputLock = true;

        // Detenemos ruidos visuales inmediatos, pero dejamos el audio para el fade-out de UnloadAllResources
        ForceFinishAll();
        eventManager.StopAllCoroutines();

        // Limpieza profunda de memoria antes de salir de la escena de juego
        yield return StartCoroutine(UnloadAllResources());

        SceneOrchestrator.Instance.GoToMenu();
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}