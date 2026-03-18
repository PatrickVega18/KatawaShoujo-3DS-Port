using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

public class MenuManager : MonoBehaviour
{
    public GameManager gameManager;
    public BackgroundManager backgroundManager;
    public Canvas menuCanvas;
    public RectTransform effectUpper, effectLower;
    public Image effectUpperImg, effectLowerImg;
    public CanvasGroup menuBase, menuGuardar, guardadoConfirmado, confirmarBorrado, confirmarSobreescribir, confirmarCarga, confirmarMenuPrincipal, lowerInfoCG;
    public Text txtTiempoJuego, txtEscenaActual, txtMusicaActual;

    public Text[] mainMenuTexts; 
    public SlotUI[] uiSlots;
    public CanvasGroup btnNuevoCG, btnVolverCG;
    public Text btnNuevoText, btnVolverText, titleGuardarCargar; 
    public Text txtContinuarExito;

    [Header("Textos Sí/No por Popup")]
    public Text[] sobreescribirTexts;   // [0]=Sí, [1]=No
    public Text[] borradoTexts;         // [0]=Sí, [1]=No
    public Text[] cargaTexts;           // [0]=Sí, [1]=No
    public Text[] menuPrincipalTexts;   // [0]=Sí, [1]=No

    [Header("Textos Preguntas (Localizables)")]
    public Text txtPreguntaGuardar;
    public Text txtPreguntaCargar;
    public Text txtPreguntaBorrar;
    public Text txtPreguntaSobreescribir;
    public Text txtPreguntaMenuPrincipal;
    public Text btnMenuTouchText;

    [Header("Tiempos")]
    public float menuFadeDuration = 0.3f;
    public float crossfadeDuration = 0.2f;
    public float effectMoveDuration = 0.5f;

    private enum MenuState { Closed, Animating, MainMenu, FileSelect, PopupConfirm, PopupSuccess }
    private MenuState currentState = MenuState.Closed;
    private bool isSaveMode = false;
    private int mainIndex = -1;
    private List<SaveHeader> headers;
    private int slotIndex = 0;
    private int scrollOffset = 0;
    private bool focusOnButtons = false;
    private int buttonIndex = 0; 
    private int targetSlotID = -1;
    private Action onConfirmAction;

    private bool isUnloadingNative = false;

    private int popupIndex = -1; 
    private CanvasGroup currentPopupGroup;
    private Text[] currentPopupTexts;

    void Start()
    {
        HideAllGroups();
        RefreshLocalizedTexts();
        
        for (int i = 0; i < uiSlots.Length; i++) uiSlots[i].gameObject.SetActive(false);
        menuCanvas.gameObject.SetActive(false);
        effectUpper.anchoredPosition = new Vector2(0, 285);
        effectLower.anchoredPosition = new Vector2(0, 285);

        if (backgroundManager != null)
        {
            backgroundManager.LoadThumbnailBundle("fondos_thumbnail");
        }
    }

    private void RefreshLocalizedTexts()
    {
        if (LocalizationManager.Instance == null) return;
        var loc = LocalizationManager.Instance;

        if (mainMenuTexts != null && mainMenuTexts.Length >= 4)
        {
            mainMenuTexts[0].text = loc.Get("continuar");
            mainMenuTexts[1].text = loc.Get("guardar");
            mainMenuTexts[2].text = loc.Get("cargar");
            mainMenuTexts[3].text = loc.Get("menu_principal");
        }

        if (titleGuardarCargar != null) titleGuardarCargar.text = isSaveMode ? loc.Get("guardar") : loc.Get("cargar");
        if (btnNuevoText != null) btnNuevoText.text = loc.Get("crear_nuevo");
        if (btnVolverText != null) btnVolverText.text = loc.Get("volver");

        if (txtPreguntaGuardar != null) txtPreguntaGuardar.text = loc.Get("guardado_ok");
        if (txtPreguntaCargar != null) txtPreguntaCargar.text = loc.Get("confirmar_carga");
        if (txtPreguntaBorrar != null) txtPreguntaBorrar.text = loc.Get("confirmar_borrado");
        if (txtPreguntaSobreescribir != null) txtPreguntaSobreescribir.text = loc.Get("confirmar_sobreescribir");
        if (txtPreguntaMenuPrincipal != null) txtPreguntaMenuPrincipal.text = loc.Get("confirmar_menu");
        if (btnMenuTouchText != null) btnMenuTouchText.text = loc.Get("menu");
        if (txtContinuarExito != null) txtContinuarExito.text = loc.Get("guardado_continuar");

        SetYesNo(sobreescribirTexts, loc);
        SetYesNo(borradoTexts, loc);
        SetYesNo(cargaTexts, loc);
        SetYesNo(menuPrincipalTexts, loc);
    }

    private void SetYesNo(Text[] texts, LocalizationManager loc)
    {
        if (texts != null && texts.Length >= 2)
        {
            texts[0].text = loc.Get("si");
            texts[1].text = loc.Get("no");
        }
    }

    void Update()
    {
        if (currentState == MenuState.Animating && !isUnloadingNative)
        {
            // OLD 3DS FIX: Drenar la entrada nativa durante animaciones donde el input se ignora (fadeouts largos).
            // Si el jugador masha 'A' en estas pausas, el OS buffer se llena y crashea.
            #if UNITY_N3DS && !UNITY_EDITOR
            UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Start);
            UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A);
            #endif
        }

        if (currentState == MenuState.Closed || currentState == MenuState.Animating) return;
        
        bool startTrigger = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.S);
        #if UNITY_N3DS && !UNITY_EDITOR
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Start)) startTrigger = true;
        #endif

        if (startTrigger) { CloseMenu(); return; }

        if (currentState == MenuState.MainMenu) UpdateMainMenu();
        else if (currentState == MenuState.FileSelect) UpdateFileSelect();
        else if (currentState == MenuState.PopupConfirm) UpdatePopupConfirm();
        else if (currentState == MenuState.PopupSuccess) UpdatePopupSuccess();
    }

    public void OpenMenu()
    {
        gameManager.IsGamePaused = true;
        RefreshLocalizedTexts();
        currentState = MenuState.Animating;
        menuCanvas.gameObject.SetActive(true);
        StartCoroutine(AnimateOpen());
    }

    public void CloseMenu()
    {
        if (currentState == MenuState.Closed || currentState == MenuState.Animating) return;
        currentState = MenuState.Animating;
        StartCoroutine(AnimateClose());
    }

    private IEnumerator AnimateOpen()
    {
        currentState = MenuState.Animating;
        SetEffectImageAlpha(150f/255f);
        if (effectUpper != null) effectUpper.gameObject.SetActive(true);
        if (effectLower != null) effectLower.gameObject.SetActive(true);

        yield return StartCoroutine(UIAnimationHelper.Move(effectUpper, new Vector3(0, 285, 0), new Vector3(0, -40, 0), effectMoveDuration));
        yield return StartCoroutine(UIAnimationHelper.Move(effectLower, new Vector3(0, 285, 0), new Vector3(0, -40, 0), effectMoveDuration));

        menuBase.alpha = 0;
        menuBase.gameObject.SetActive(true);
        if (lowerInfoCG != null) { lowerInfoCG.alpha = 0; lowerInfoCG.gameObject.SetActive(true); }
        UpdateGameInfo();

        StartCoroutine(UIAnimationHelper.Fade(menuBase, 0, 1, menuFadeDuration));
        if (lowerInfoCG != null) yield return StartCoroutine(UIAnimationHelper.Fade(lowerInfoCG, 0, 1, menuFadeDuration));
        else yield return new WaitForSeconds(menuFadeDuration);

        mainIndex = -1;
        RefreshMainVisuals();
        currentState = MenuState.MainMenu;
    }

    [Header("Loading Screen")]
    public Image cuadroInicialUpper;
    public Image cuadroInicialLower;

    public void ShowOpeningCurtain()
    {
        if (cuadroInicialUpper != null) { cuadroInicialUpper.gameObject.SetActive(true); SetImageAlpha(cuadroInicialUpper, 1f); }
        if (cuadroInicialLower != null) { cuadroInicialLower.gameObject.SetActive(true); SetImageAlpha(cuadroInicialLower, 1f); }
    }

    public IEnumerator HideOpeningCurtain()
    {
        float duration = 1.0f;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, elapsed / duration);
            SetImageAlpha(cuadroInicialUpper, a);
            SetImageAlpha(cuadroInicialLower, a);
            yield return null;
        }

        SetImageAlpha(cuadroInicialUpper, 0f);
        SetImageAlpha(cuadroInicialLower, 0f);
        
        if (cuadroInicialUpper != null) cuadroInicialUpper.gameObject.SetActive(false);
        if (cuadroInicialLower != null) cuadroInicialLower.gameObject.SetActive(false);
    }

    private void SetImageAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    private void UpdateGameInfo()
    {
        if (LocalizationManager.Instance == null) return;
        var loc = LocalizationManager.Instance;

        // 1. Tiempo de Juego (hh:mm)
        if (txtTiempoJuego != null)
        {
            int totalSeconds = Mathf.FloorToInt(gameManager.sessionPlayTime);
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            txtTiempoJuego.text = string.Format("{0} {1:00}:{2:00}", loc.Get("tiempo_jugado"), hours, minutes);
        }

        // 2. Escena Actual
        if (txtEscenaActual != null)
        {
            string sceneName = gameManager.GetCurrentSceneName();
            if (loc != null) sceneName = loc.GetSceneName(gameManager.currentAct, gameManager.GetLastSceneHash(), sceneName);
            txtEscenaActual.text = string.Format("{0} {1}", loc.Get("escena_actual"), sceneName);
        }

        // 3. Música Actual (Ocultar si no hay)
        if (txtMusicaActual != null)
        {
            string track = gameManager.musicManager.GetCurrentTrackName();
            if (string.IsNullOrEmpty(track))
            {
                txtMusicaActual.gameObject.SetActive(false);
            }
            else
            {
                txtMusicaActual.gameObject.SetActive(true);
                txtMusicaActual.text = string.Format("{0} {1}", loc.Get("musica_actual"), track);
            }
        }
    }

    private IEnumerator AnimateClose()
    {
        CanvasGroup active = GetActiveGroup();
        if (lowerInfoCG != null) StartCoroutine(UIAnimationHelper.Fade(lowerInfoCG, lowerInfoCG.alpha, 0, menuFadeDuration));

        if (active != null)
        {
            yield return StartCoroutine(UIAnimationHelper.Fade(active, active.alpha, 0, menuFadeDuration));
        }
        else if (lowerInfoCG != null)
        {
            yield return new WaitForSeconds(menuFadeDuration);
        }

        HideAllGroups();

        yield return StartCoroutine(UIAnimationHelper.Move(effectLower, new Vector3(0, -40, 0), new Vector3(0, 285, 0), effectMoveDuration));
        yield return StartCoroutine(UIAnimationHelper.Move(effectUpper, new Vector3(0, -40, 0), new Vector3(0, 285, 0), effectMoveDuration));
        if (lowerInfoCG != null) lowerInfoCG.gameObject.SetActive(false);
        menuCanvas.gameObject.SetActive(false);
        currentState = MenuState.Closed;
        gameManager.IsGamePaused = false;
    }

    private void UpdateMainMenu()
    {
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool a = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Return);
        #if UNITY_N3DS && !UNITY_EDITOR
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Down) || UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Right)) down = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Up) || UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Left)) up = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A)) a = true;
        #endif

        if (down)
        {
            if (mainIndex == -1) mainIndex = 0;
            else { 
                mainIndex++; 
                if (mainIndex == 1 && gameManager.IsSaveLocked()) mainIndex++;
                if (mainIndex > 3) mainIndex = 0; 
            }
            RefreshMainVisuals();
        }
        else if (up)
        {
            if (mainIndex == -1) mainIndex = 0;
            else { 
                mainIndex--; 
                if (mainIndex == 1 && gameManager.IsSaveLocked()) mainIndex--;
                if (mainIndex < 0) mainIndex = 3; 
            }
            RefreshMainVisuals();
        }

        if (mainIndex != -1 && a)
        {
            if (mainIndex == 0) CloseMenu();
            else if (mainIndex == 1 && !gameManager.IsSaveLocked()) SetupFileSelect(true);
            else if (mainIndex == 2) SetupFileSelect(false);
            else if (mainIndex == 3) SetupPopupFromMain(confirmarMenuPrincipal, ActionExit);
        }
    }

    private void RefreshMainVisuals()
    {
        for (int i = 0; i < mainMenuTexts.Length; i++) 
        {
            if (i == 1 && gameManager.IsSaveLocked())
            {
                if (mainMenuTexts[i] != null) 
                {
                    Color c = mainMenuTexts[i].color;
                    c.a = 0.2f;
                    mainMenuTexts[i].color = c;
                }
            }
            else 
            {
                SetTextAlpha(mainMenuTexts[i], i == mainIndex);
            }
        }
    }

    private void SetupFileSelect(bool save)
    {
        isSaveMode = save;
        if (LocalizationManager.Instance != null)
            titleGuardarCargar.text = save ? LocalizationManager.Instance.Get("guardar") : LocalizationManager.Instance.Get("cargar");
        else
            titleGuardarCargar.text = save ? "Guardar" : "Cargar";
        btnNuevoCG.gameObject.SetActive(save);
        headers = SaveSystem.LoadHeaders();

        if (headers.Count == 0)
        {
            focusOnButtons = true;
            buttonIndex = save ? 0 : 1;
        }
        else
        {
            focusOnButtons = false;
            slotIndex = 0;
            scrollOffset = 0;
        }

        currentState = MenuState.Animating;
        StartCoroutine(CrossfadeAndSetState(menuBase, menuGuardar, MenuState.FileSelect));
    }

    private void UpdateFileSelect()
    {
        bool b = Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Backspace);
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
        bool left = Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow);
        bool a = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Return);
        bool x = Input.GetKeyDown(KeyCode.X);

        #if UNITY_N3DS && !UNITY_EDITOR
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.B)) b = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Up)) up = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Down)) down = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Left)) left = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Right)) right = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A)) a = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.X)) x = true;
        #endif

        if (b)
        {
            currentState = MenuState.Animating;
            StartCoroutine(CrossfadeAndSetState(menuGuardar, menuBase, MenuState.MainMenu));
            return;
        }

        if (focusOnButtons)
        {
            if (up)
            {
                if (headers.Count > 0) { focusOnButtons = false; scrollOffset = Mathf.Max(0, slotIndex - 2); RefreshFileVisuals(); }
            }
            else if (down)
            {
                if (headers.Count > 0) { focusOnButtons = false; scrollOffset = Mathf.Max(0, slotIndex - 2); RefreshFileVisuals(); }
            }
            else if (left || right)
            {
                if (isSaveMode)
                {
                    buttonIndex = (buttonIndex == 0) ? 1 : 0;
                }
                RefreshFileVisuals();
            }
            else if (a)
            {
                if (buttonIndex == 1)
                {
                    currentState = MenuState.Animating;
                    StartCoroutine(CrossfadeAndSetState(menuGuardar, menuBase, MenuState.MainMenu));
                }
                else if (isSaveMode && buttonIndex == 0)
                {
                    int nid = 0;
                    foreach (var h in headers) if (h.slotIndex >= nid) nid = h.slotIndex + 1;
                    targetSlotID = nid;
                    ActionSave();
                }
            }
        }
        else
        {
            if (down)
            {
                slotIndex++;
                if (slotIndex >= headers.Count)
                {
                    slotIndex = headers.Count - 1;
                    focusOnButtons = true;
                    buttonIndex = isSaveMode ? 0 : 1;
                }
                else if (slotIndex >= scrollOffset + 3) scrollOffset++;
                RefreshFileVisuals();
            }
            else if (up)
            {
                slotIndex--;
                if (slotIndex < 0)
                {
                    slotIndex = 0;
                    focusOnButtons = true;
                    buttonIndex = isSaveMode ? 0 : 1;
                }
                else if (slotIndex < scrollOffset) scrollOffset--;
                RefreshFileVisuals();
            }
            else if (left)
            {
                focusOnButtons = true;
                buttonIndex = isSaveMode ? 0 : 1; // Izquierda -> Crear nuevo estado (o Volver si carga)
                RefreshFileVisuals();
            }
            else if (right)
            {
                focusOnButtons = true;
                buttonIndex = 1; // Derecha -> Volver
                RefreshFileVisuals();
            }
            else if (a)
            {
                targetSlotID = headers[slotIndex].slotIndex;
                if (isSaveMode) SetupPopup(confirmarSobreescribir, ActionSave);
                else SetupPopup(confirmarCarga, ActionLoad);
            }
            else if (x)
            {
                targetSlotID = headers[slotIndex].slotIndex;
                SetupPopup(confirmarBorrado, ActionDelete);
            }
        }
    }

    private void RefreshFileVisuals()
    {
        for (int i = 0; i < uiSlots.Length; i++)
        {
            int dIdx = scrollOffset + i;
            if (dIdx < headers.Count)
            {
                uiSlots[i].gameObject.SetActive(true);
                Sprite bgSprite = (backgroundManager != null) ? backgroundManager.GetThumbnailSpriteByHash(headers[dIdx].backgroundHash) : null;
                uiSlots[i].SetData(headers[dIdx], bgSprite);
                uiSlots[i].SetHighlight(!focusOnButtons && slotIndex == dIdx);
            }
            else
            {
                uiSlots[i].gameObject.SetActive(false);
            }
        }
        
        btnNuevoCG.alpha = (focusOnButtons && buttonIndex == 0) ? 1f : 0.5f;
        btnVolverCG.alpha = (focusOnButtons && buttonIndex == 1) ? 1f : 0.5f;
        SetTextAlpha(btnNuevoText, focusOnButtons && buttonIndex == 0);
        SetTextAlpha(btnVolverText, focusOnButtons && buttonIndex == 1);
    }

    // ===== POPUPS (SÍ/NO) =====

    private void SetupPopup(CanvasGroup pg, Action a)
    {
        onConfirmAction = a;
        popupIndex = 1;
        currentPopupGroup = pg;
        currentPopupTexts = GetPopupTexts(pg);
        RefreshPopupVisuals();
        currentState = MenuState.Animating;
        StartCoroutine(CrossfadeAndSetState(menuGuardar, pg, MenuState.PopupConfirm));
    }

    private void SetupPopupFromMain(CanvasGroup pg, Action a)
    {
        onConfirmAction = a;
        popupIndex = 1;
        currentPopupGroup = pg;
        currentPopupTexts = GetPopupTexts(pg);
        RefreshPopupVisuals();
        currentState = MenuState.Animating;
        StartCoroutine(CrossfadeAndSetState(menuBase, pg, MenuState.PopupConfirm));
    }

    private Text[] GetPopupTexts(CanvasGroup pg)
    {
        if (pg == confirmarSobreescribir) return sobreescribirTexts;
        if (pg == confirmarBorrado) return borradoTexts;
        if (pg == confirmarCarga) return cargaTexts;
        if (pg == confirmarMenuPrincipal) return menuPrincipalTexts;
        return null;
    }

    private void UpdatePopupConfirm()
    {
        bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow);
        bool a = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Return);

        #if UNITY_N3DS && !UNITY_EDITOR
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Left) || UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Up)) left = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Right) || UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Down)) right = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A)) a = true;
        #endif

        if (left)
        {
            if (popupIndex == -1) popupIndex = 0;
            else popupIndex = (popupIndex == 0) ? 1 : 0; // Wrap
            RefreshPopupVisuals();
        }
        else if (right)
        {
            if (popupIndex == -1) popupIndex = 1;
            else popupIndex = (popupIndex == 1) ? 0 : 1; // Wrap
            RefreshPopupVisuals();
        }

        if (popupIndex != -1 && a)
        {
            if (popupIndex == 0) onConfirmAction.Invoke();
            else ReturnFromPopup();
        }
    }

    private void RefreshPopupVisuals()
    {
        if (currentPopupTexts == null || currentPopupTexts.Length < 2) return;
        SetTextAlpha(currentPopupTexts[0], popupIndex == 0);
        SetTextAlpha(currentPopupTexts[1], popupIndex == 1);
    }

    // ===== POPUP ÉXITO =====

    private void SetupSuccess()
    {
        currentState = MenuState.Animating;
        CanvasGroup from = currentPopupGroup != null ? currentPopupGroup : menuGuardar;
        SetTextAlpha(txtContinuarExito, false);
        StartCoroutine(SequentialFadeAndSetState(from, guardadoConfirmado, MenuState.PopupSuccess));
    }

    private void UpdatePopupSuccess()
    {
        bool anyDir = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                      Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow);
        bool a = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Return);

        #if UNITY_N3DS && !UNITY_EDITOR
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Left) || UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Right) ||
            UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Up) || UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Down)) anyDir = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A)) a = true;
        #endif

        if (anyDir) SetTextAlpha(txtContinuarExito, true);
        if (txtContinuarExito != null && txtContinuarExito.color.a > 0.9f && a)
        {
            currentState = MenuState.Animating;
            StartCoroutine(CrossfadeAndSetState(guardadoConfirmado, menuBase, MenuState.MainMenu));
            mainIndex = -1;
            RefreshMainVisuals();
        }
    }

    // ===== ACCIONES =====

    private void ActionSave()
    {
        SaveHeader h = new SaveHeader();
        h.slotIndex = targetSlotID;

        if (LocalizationManager.Instance != null)
        {
            h.date = LocalizationManager.Instance.GetLocalizedDate();
            h.language = LocalizationManager.Instance.currentLanguage;
        }
        else
        {
            h.date = DateTime.Now.ToString("MMM dd, HH:mm");
            h.language = "ES";
        }

        h.actName = gameManager.GetCurrentActDescription();
        h.sceneName = gameManager.GetCurrentSceneName();
        h.backgroundHash = gameManager.GetCurrentBGHash();
        h.playTime = gameManager.sessionPlayTime;
        h.lastSceneHash = gameManager.GetLastSceneHash();
        SaveBody b = gameManager.GetSaveSnapshot();
        SaveSystem.SaveGame(targetSlotID, h, b);
        SetupSuccess();
    }

    private void ActionLoad()
    {
        SaveBody b = SaveSystem.LoadBody(targetSlotID);
        int hIdx = headers.FindIndex(hd => hd.slotIndex == targetSlotID);
        float pTime = (hIdx >= 0) ? headers[hIdx].playTime : 0f;
        StartCoroutine(LoadSequence(b, pTime));
    }

    private void ActionDelete()
    {
        SaveSystem.DeleteSlot(targetSlotID);
        ReturnToFileSelect();
    }

    private void ActionExit()
    {
        StartCoroutine(ExitSequence());
    }

    private IEnumerator ExitSequence()
    {
        if (gameManager != null) gameManager.isShuttingDown = true;
        currentState = MenuState.Animating;

        // 1. FadeOut del popup actual (confirmarMenuPrincipal) e información inferior
        if (lowerInfoCG != null) StartCoroutine(UIAnimationHelper.Fade(lowerInfoCG, lowerInfoCG.alpha, 0, crossfadeDuration));
        yield return StartCoroutine(UIAnimationHelper.Fade(confirmarMenuPrincipal, 1, 0, crossfadeDuration));

        // 2. Cortinas a opaco (Alpha 255 / 1.0)
        yield return StartCoroutine(FadeEffectImages(effectUpperImg.color.a, 1f, effectMoveDuration));

        if (menuBase != null) menuBase.gameObject.SetActive(false);
        if (menuGuardar != null) menuGuardar.gameObject.SetActive(false);
        if (lowerInfoCG != null) lowerInfoCG.gameObject.SetActive(false);

        // 3. Descarga de recursos y espera (Peligro crítico: No llamar APIs de N3DS aquí)
        isUnloadingNative = true;
        
        if (gameManager != null) yield return StartCoroutine(gameManager.UnloadAllResources());

        SceneOrchestrator.Instance.GoToMenu();
    }

    // ===== SECUENCIA DE CARGA =====

    private IEnumerator LoadSequence(SaveBody data, float playTime)
    {
        currentState = MenuState.Animating;

        // 1. FadeOut del popup confirmarCarga e información inferior
        CanvasGroup from = currentPopupGroup != null ? currentPopupGroup : confirmarCarga;
        if (lowerInfoCG != null) StartCoroutine(UIAnimationHelper.Fade(lowerInfoCG, lowerInfoCG.alpha, 0, crossfadeDuration));
        yield return StartCoroutine(UIAnimationHelper.Fade(from, from.alpha, 0, crossfadeDuration));
        HideAllGroups();

        // 2. Cortina opaca (fade a alpha 1.0)
        yield return StartCoroutine(FadeEffectImages(effectUpperImg.color.a, 1f, crossfadeDuration));
        yield return null;

        if (menuBase != null) menuBase.gameObject.SetActive(false);
        if (menuGuardar != null) menuGuardar.gameObject.SetActive(false);
        if (lowerInfoCG != null) lowerInfoCG.gameObject.SetActive(false);

        // 3. Restaurar todo detrás de la cortina
        yield return StartCoroutine(gameManager.RestoreSnapshot(data));
        yield return null;

        // 4. Cortina semi-transparente (fade a 0.5)
        yield return StartCoroutine(FadeEffectImages(1f, 0.5f, crossfadeDuration));

        // 5. Animación de salida (secuencial: Lower primero, luego Upper)
        yield return StartCoroutine(UIAnimationHelper.Move(effectLower, new Vector3(0, -40, 0), new Vector3(0, 285, 0), effectMoveDuration));
        yield return StartCoroutine(UIAnimationHelper.Move(effectUpper, new Vector3(0, -40, 0), new Vector3(0, 285, 0), effectMoveDuration));

        menuCanvas.gameObject.SetActive(false);
        currentState = MenuState.Closed;
        gameManager.sessionPlayTime = playTime;
        gameManager.IsGamePaused = false;
    }

    private IEnumerator FadeEffectImages(float from, float to, float duration)
    {
        float elapsed = 0;
        if (duration <= 0)
        {
            SetEffectImageAlpha(to);
            yield break;
        }
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(from, to, elapsed / duration);
            SetEffectImageAlpha(a);
            yield return null;
        }
        SetEffectImageAlpha(to);
    }
    


    private void SetEffectImageAlpha(float a)
    {
        bool isActive = a > 0.01f;
        if (effectUpperImg != null) { 
            if (effectUpperImg.gameObject.activeSelf != isActive) effectUpperImg.gameObject.SetActive(isActive);
            if (isActive) { Color c = effectUpperImg.color; c.a = a; effectUpperImg.color = c; }
        }
        if (effectLowerImg != null) { 
            if (effectLowerImg.gameObject.activeSelf != isActive) effectLowerImg.gameObject.SetActive(isActive);
            if (isActive) { Color c = effectLowerImg.color; c.a = a; effectLowerImg.color = c; }
        }
    }

    // ===== RETORNO A FILE SELECT =====

    private void ReturnFromPopup()
    {
        if (currentPopupGroup == confirmarMenuPrincipal)
        {
            // Volver a menuBase
            currentState = MenuState.Animating;
            StartCoroutine(CrossfadeAndSetState(currentPopupGroup, menuBase, MenuState.MainMenu));
            mainIndex = -1;
            RefreshMainVisuals();
        }
        else
        {
            ReturnToFileSelect();
        }
    }

    private void ReturnToFileSelect()
    {
        CanvasGroup from = currentPopupGroup != null ? currentPopupGroup : menuGuardar;
        currentState = MenuState.Animating;
        headers = SaveSystem.LoadHeaders();
        if (headers.Count == 0)
        {
            focusOnButtons = true;
            buttonIndex = isSaveMode ? 0 : 1;
        }
        else if (slotIndex >= headers.Count) slotIndex = headers.Count - 1;

        StartCoroutine(CrossfadeAndSetState(from, menuGuardar, MenuState.FileSelect));
    }

    // ===== UTILIDADES =====

    private IEnumerator CrossfadeAndSetState(CanvasGroup from, CanvasGroup to, MenuState nextState)
    {
        to.alpha = 0;
        to.gameObject.SetActive(true);
        StartCoroutine(UIAnimationHelper.Fade(from, from.alpha, 0, crossfadeDuration));
        yield return StartCoroutine(UIAnimationHelper.Fade(to, 0, 1, crossfadeDuration));
        from.gameObject.SetActive(false);

        currentState = nextState;

        if (nextState == MenuState.MainMenu) RefreshMainVisuals();
        else if (nextState == MenuState.FileSelect) RefreshFileVisuals();
    }

    private IEnumerator SequentialFadeAndSetState(CanvasGroup from, CanvasGroup to, MenuState nextState)
    {
        // Primero fadeOut del actual
        yield return StartCoroutine(UIAnimationHelper.Fade(from, from.alpha, 0, crossfadeDuration));
        HideAllGroups();

        // Luego fadeIn del siguiente
        to.alpha = 0;
        to.gameObject.SetActive(true);
        yield return StartCoroutine(UIAnimationHelper.Fade(to, 0, 1, crossfadeDuration));

        currentState = nextState;

        if (nextState == MenuState.MainMenu) RefreshMainVisuals();
        else if (nextState == MenuState.FileSelect) RefreshFileVisuals();
    }

    private CanvasGroup GetActiveGroup()
    {
        if (menuBase.gameObject.activeSelf) return menuBase;
        if (menuGuardar.gameObject.activeSelf) return menuGuardar;
        if (guardadoConfirmado.gameObject.activeSelf) return guardadoConfirmado;
        if (confirmarBorrado.gameObject.activeSelf) return confirmarBorrado;
        if (confirmarSobreescribir.gameObject.activeSelf) return confirmarSobreescribir;
        if (confirmarCarga.gameObject.activeSelf) return confirmarCarga;
        if (confirmarMenuPrincipal.gameObject.activeSelf) return confirmarMenuPrincipal;
        return null;
    }

    private void HideAllGroups()
    {
        menuBase.gameObject.SetActive(false);
        menuGuardar.gameObject.SetActive(false);
        guardadoConfirmado.gameObject.SetActive(false);
        confirmarBorrado.gameObject.SetActive(false);
        confirmarSobreescribir.gameObject.SetActive(false);
        confirmarCarga.gameObject.SetActive(false);
        confirmarMenuPrincipal.gameObject.SetActive(false);
    }

    private void SetTextAlpha(Text t, bool active)
    {
        if (t == null) return;
        Color c = t.color;
        c.a = active ? 1f : 0.5f;
        t.color = c;
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}