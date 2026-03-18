using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;

public class MainMenuManager : MonoBehaviour
{
    [Header("Main Menu Options")]
    public Text[] mainTexts; // 0=Inicio, 1=Cargar, 2=Opciones, 3=Creditos

    [Header("Effects")]
    public RectTransform effectUpper, effectLower;
    public Image effectUpperImg, effectLowerImg;
    public float effectMoveDuration = 0.5f;

    [Header("Intro")]
    public Image cuadroInicial1;
    public Image cuadroInicial2;
    public float transitionFadeDuration = 0.5f;
    public float introFadeDuration = 1.0f;

    [Header("Música")]
    public AudioSource bgmSource;

    [Header("Dependencies")]
    public BackgroundManager backgroundManager;

    [Header("SubMenus (CanvasGroups)")]
    public CanvasGroup menuCargar;
    public CanvasGroup menuOpciones;
    public CanvasGroup menuCreditos;
    public CanvasGroup confirmarCarga;
    public CanvasGroup confirmarBorrado;

    [Header("Cargar - Slots")]
    public SlotUI[] uiSlots;
    public CanvasGroup btnVolverCG;
    public Text btnVolverText;
    public Text titleCargar;

    [Header("Opciones")]
    public Text txtEspanol;
    public Text txtIngles;
    public CanvasGroup btnVolverOpcionesCG;
    public Text txtVolverOpciones;

    [Header("Creditos")]
    public CanvasGroup btnVolverCreditosCG;
    public Text txtVolverCreditos;
    public Text txtCreditosTexto;

    [Header("Popup Texts")]
    public Text txtPreguntaCargar;
    public Text[] cargaPopupTexts;   // [0]=Sí, [1]=No
    public Text txtPreguntaBorrar;
    public Text[] borradoPopupTexts; // [0]=Sí, [1]=No

    [Header("Tiempos")]
    public float fadeDuration = 0.3f;

    // ------- State Machine -------
    private enum MenuState { MainMenu, Animating, SubCargar, SubOpciones, SubCreditos, PopupConfirm }
    private MenuState currentState = MenuState.MainMenu;

    // Main menu
    private int mainIndex = -1;
    private const int MAIN_COUNT = 4;

    // Cargar
    private List<SaveHeader> headers;
    private int slotIndex = 0;
    private int scrollOffset = 0;
    private bool focusOnButtons = false;
    private int targetSlotID = -1;

    // Opciones
    private int opcionesIndex = 0;
    private const int OPCIONES_COUNT = 3;

    // Popup
    private int popupIndex = -1;
    private CanvasGroup currentPopupGroup;
    private Text[] currentPopupTexts;
    private Action onConfirmAction;

    private bool isUnloadingNative = false;

    void Start()
    {
        if (backgroundManager == null) backgroundManager = FindObjectOfType<BackgroundManager>();
        
        if (backgroundManager != null)
        {
            backgroundManager.LoadThumbnailBundle("fondos_thumbnail");
        }

        RefreshAllTexts();
        
        // Asegurar que las miniaturas se carguen al inicio (en el primer OnEnable ocurrirá si no)
        if (backgroundManager != null) backgroundManager.LoadThumbnailBundle("fondos_thumbnail");
        
        StartCoroutine(FadeOutIntro());
        // El OnEnable se encargará del resto del setup y del FadeOutIntro (redundante pero seguro)
    }

    void OnEnable()
    {
        // Reiniciar estado cada vez que se activa el Root del menú (Old 3DS Single Scene Fix)
        isUnloadingNative = false;
        currentState = MenuState.MainMenu;
        mainIndex = -1;
        
        // Cargar miniaturas al entrar al menú principal
        if (backgroundManager != null) backgroundManager.LoadThumbnailBundle("fondos_thumbnail");

        HideAllSubs();
        
        if (effectUpper != null) effectUpper.anchoredPosition = new Vector2(0, 285);
        if (effectLower != null) effectLower.anchoredPosition = new Vector2(0, 285);
        SetEffectScale(-1);
        SetEffectImageAlpha(0);

        RefreshMainVisuals();

        // Reiniciar y ejecutar el fundido inicial (Cuadro Inicial) cada vez que volvemos al menú
        StopCoroutine("FadeOutIntro");
        StartCoroutine(FadeOutIntro());

        if (bgmSource != null && bgmSource.clip != null)
        {
            bgmSource.volume = 1f; // Restaurar volumen por si venimos de un fadeout
            if (!bgmSource.isPlaying)
            {
                bgmSource.loop = true;
                bgmSource.Play();
            }
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

        switch (currentState)
        {
            case MenuState.MainMenu: UpdateMainMenu(); break;
            case MenuState.SubCargar: UpdateSubCargar(); break;
            case MenuState.SubOpciones: UpdateSubOpciones(); break;
            case MenuState.SubCreditos: UpdateSubCreditos(); break;
            case MenuState.PopupConfirm: UpdatePopupConfirm(); break;
        }
    }

    private void ReadInput(out bool up, out bool down, out bool left, out bool right, out bool a, out bool b, out bool start, out bool x)
    {
        up = Input.GetKeyDown(KeyCode.UpArrow);
        down = Input.GetKeyDown(KeyCode.DownArrow);
        left = Input.GetKeyDown(KeyCode.LeftArrow);
        right = Input.GetKeyDown(KeyCode.RightArrow);
        a = Input.GetKeyDown(KeyCode.A);
        b = Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Return);
        start = Input.GetKeyDown(KeyCode.Joystick1Button9);
        x = Input.GetKeyDown(KeyCode.X);

        #if UNITY_N3DS && !UNITY_EDITOR
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Up)) up = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Down)) down = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Left)) left = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Right)) right = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A)) a = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.B)) b = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Start)) start = true;
        if (UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.X)) x = true;
        #endif
    }
    private void UpdateMainMenu()
    {
        bool up, down, left, right, a, b, start, x;
        ReadInput(out up, out down, out left, out right, out a, out b, out start, out x);

        if (up || down)
        {
            if (mainIndex == -1)
            {
                mainIndex = 0;
            }
            else
            {
                mainIndex += up ? -1 : 1;
                if (mainIndex < 0) mainIndex = MAIN_COUNT - 1;
                if (mainIndex >= MAIN_COUNT) mainIndex = 0;
            }
            RefreshMainVisuals();
        }

        if (a && mainIndex >= 0)
        {
            switch (mainIndex)
            {
                case 0: StartCoroutine(AnimateInicio()); break;
                case 1: StartCoroutine(OpenSubMenu(menuCargar, MenuState.SubCargar, true)); break;
                case 2: StartCoroutine(OpenSubMenu(menuOpciones, MenuState.SubOpciones, false)); break;
                case 3: StartCoroutine(OpenSubMenu(menuCreditos, MenuState.SubCreditos, false)); break;
            }
        }
    }

    private void RefreshMainVisuals()
    {
        for (int i = 0; i < mainTexts.Length; i++)
        {
            Color c = mainTexts[i].color;
            c.a = (i == mainIndex) ? 1f : 0.49f;
            mainTexts[i].color = c;
        }
    }

    private void SetEffectScale(float yScale)
    {
        effectUpper.localScale = new Vector3(1, yScale, 1);
        effectLower.localScale = new Vector3(1, yScale, 1);
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

    private IEnumerator AnimateInicio()
    {
        currentState = MenuState.Animating;
        SetEffectScale(1);
        SetEffectImageAlpha(1f);
        effectLower.anchoredPosition = new Vector2(0, -285);
        effectUpper.anchoredPosition = new Vector2(0, -285);

        if (bgmSource != null) StartCoroutine(FadeBGMVolume(0f, effectMoveDuration * 2f));

        yield return StartCoroutine(UIAnimationHelper.Move(effectLower, new Vector3(0, -285, 0), new Vector3(0, 40, 0), effectMoveDuration));
        yield return StartCoroutine(UIAnimationHelper.Move(effectUpper, new Vector3(0, -285, 0), new Vector3(0, 40, 0), effectMoveDuration));

        yield return new WaitForSeconds(1f);

        // Limpieza de memoria exhaustiva de la escena del menú principal ANTES de montar la colosal escena del Juego (Old 3DS Fix)
        isUnloadingNative = true;
        if (backgroundManager != null) 
        { 
            backgroundManager.UnloadContent(); 
        }
        yield return Resources.UnloadUnusedAssets();

        SceneTransferData.shouldLoadSave = false;
        SceneTransferData.slotToLoad = -1;
        
        SceneOrchestrator.Instance.GoToGame();
    }

    private IEnumerator OpenSubMenu(CanvasGroup sub, MenuState targetState, bool setupCargar)
    {
        currentState = MenuState.Animating;
        SetEffectScale(-1);
        SetEffectImageAlpha(0.49f);
        effectUpper.anchoredPosition = new Vector2(0, 285);
        effectLower.anchoredPosition = new Vector2(0, 285);

        yield return StartCoroutine(UIAnimationHelper.Move(effectUpper, new Vector3(0, 285, 0), new Vector3(0, -40, 0), effectMoveDuration));
        yield return StartCoroutine(UIAnimationHelper.Move(effectLower, new Vector3(0, 285, 0), new Vector3(0, -40, 0), effectMoveDuration));

        if (setupCargar) SetupCargar();
        if (targetState == MenuState.SubOpciones) SetupOpciones();
        sub.alpha = 0;
        sub.gameObject.SetActive(true);
        yield return StartCoroutine(UIAnimationHelper.Fade(sub, 0, 1, fadeDuration));

        currentState = targetState;
    }

    private IEnumerator CloseSubMenu(CanvasGroup sub)
    {
        currentState = MenuState.Animating;

        yield return StartCoroutine(UIAnimationHelper.Fade(sub, sub.alpha, 0, fadeDuration));
        sub.gameObject.SetActive(false);

        yield return StartCoroutine(UIAnimationHelper.Move(effectLower, new Vector3(0, -40, 0), new Vector3(0, 285, 0), effectMoveDuration));
        yield return StartCoroutine(UIAnimationHelper.Move(effectUpper, new Vector3(0, -40, 0), new Vector3(0, 285, 0), effectMoveDuration));

        currentState = MenuState.MainMenu;
    }

    // ===================== SUB CARGAR =====================
    private void SetupCargar()
    {
        headers = SaveSystem.LoadHeaders();
        if (headers.Count == 0)
        {
            focusOnButtons = true;
        }
        else
        {
            focusOnButtons = false;
            slotIndex = 0;
            scrollOffset = 0;
        }
        RefreshCargarVisuals();
    }

    private void UpdateSubCargar()
    {
        bool up, down, left, right, a, b, start, x;
        ReadInput(out up, out down, out left, out right, out a, out b, out start, out x);

        if (b || start)
        {
            StartCoroutine(CloseSubMenu(menuCargar));
            return;
        }

        if (focusOnButtons)
        {
            if (up && headers.Count > 0)
            {
                focusOnButtons = false;
                scrollOffset = Mathf.Max(0, slotIndex - 2);
                RefreshCargarVisuals();
            }
            else if (down && headers.Count > 0)
            {
                focusOnButtons = false;
                scrollOffset = Mathf.Max(0, slotIndex - 2);
                RefreshCargarVisuals();
            }
            else if (a)
            {
                StartCoroutine(CloseSubMenu(menuCargar));
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
                }
                else if (slotIndex >= scrollOffset + 3) scrollOffset++;
                RefreshCargarVisuals();
            }
            else if (up)
            {
                slotIndex--;
                if (slotIndex < 0)
                {
                    slotIndex = 0;
                    focusOnButtons = true;
                }
                else if (slotIndex < scrollOffset) scrollOffset--;
                RefreshCargarVisuals();
            }
            else if (left || right)
            {
                focusOnButtons = true;
                RefreshCargarVisuals();
            }
            else if (a)
            {
                targetSlotID = headers[slotIndex].slotIndex;
                SetupPopup(confirmarCarga, cargaPopupTexts, ActionLoad);
            }
            else if (x)
            {
                targetSlotID = headers[slotIndex].slotIndex;
                SetupPopup(confirmarBorrado, borradoPopupTexts, ActionDelete);
            }
        }
    }

    private void RefreshCargarVisuals()
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
            else uiSlots[i].gameObject.SetActive(false);
        }
        if (btnVolverCG != null)
        {
            btnVolverCG.alpha = focusOnButtons ? 1f : 0.5f;
            SetTextAlpha(btnVolverText, focusOnButtons);
        }
    }

    // ===================== POPUP CONFIRM =====================
    private void SetupPopup(CanvasGroup pg, Text[] texts, Action action)
    {
        onConfirmAction = action;
        popupIndex = 1; // default on "No"
        currentPopupGroup = pg;
        currentPopupTexts = texts;
        RefreshPopupVisuals();
        currentState = MenuState.Animating;
        StartCoroutine(CrossfadePopup(menuCargar, pg, MenuState.PopupConfirm));
    }

    private void UpdatePopupConfirm()
    {
        bool up, down, left, right, a, b, start, x;
        ReadInput(out up, out down, out left, out right, out a, out b, out start, out x);

        if (left || up)
        {
            if (popupIndex == -1) popupIndex = 0;
            else popupIndex = (popupIndex == 0) ? 1 : 0;
            RefreshPopupVisuals();
        }
        else if (right || down)
        {
            if (popupIndex == -1) popupIndex = 1;
            else popupIndex = (popupIndex == 1) ? 0 : 1;
            RefreshPopupVisuals();
        }

        if (popupIndex != -1 && a)
        {
            if (popupIndex == 0) onConfirmAction.Invoke();
            else ReturnFromPopup();
        }

        if (b) ReturnFromPopup();
    }

    private void RefreshPopupVisuals()
    {
        if (currentPopupTexts == null) return;
        for (int i = 0; i < currentPopupTexts.Length; i++)
            SetTextAlpha(currentPopupTexts[i], popupIndex == i);
    }

    private void ReturnFromPopup()
    {
        currentState = MenuState.Animating;
        StartCoroutine(CrossfadePopup(currentPopupGroup, menuCargar, MenuState.SubCargar));
    }

    private IEnumerator CrossfadePopup(CanvasGroup from, CanvasGroup to, MenuState target)
    {
        yield return StartCoroutine(UIAnimationHelper.Fade(from, from.alpha, 0, fadeDuration));
        from.gameObject.SetActive(false);
        to.alpha = 0;
        to.gameObject.SetActive(true);
        yield return StartCoroutine(UIAnimationHelper.Fade(to, 0, 1, fadeDuration));
        if (target == MenuState.SubCargar) RefreshCargarVisuals();
        currentState = target;
    }

    // ===================== LOAD / DELETE ACTIONS =====================
    private void ActionLoad()
    {
        StartCoroutine(LoadSequence());
    }

    private IEnumerator LoadSequence()
    {
        currentState = MenuState.Animating;

        CanvasGroup from = currentPopupGroup != null ? currentPopupGroup : confirmarCarga;
        yield return StartCoroutine(UIAnimationHelper.Fade(from, from.alpha, 0, fadeDuration));
        HideAllSubs();

        if (bgmSource != null) StartCoroutine(FadeBGMVolume(0f, transitionFadeDuration));
        yield return StartCoroutine(FadeEffectImages(effectUpperImg.color.a, 1f, transitionFadeDuration));
        
        if (menuCargar != null) menuCargar.gameObject.SetActive(false);
        if (menuOpciones != null) menuOpciones.gameObject.SetActive(false);
        if (menuCreditos != null) menuCreditos.gameObject.SetActive(false);

        isUnloadingNative = true;
        if (backgroundManager != null) 
        { 
            backgroundManager.UnloadContent(); 
        }
        yield return Resources.UnloadUnusedAssets();

        yield return new WaitForSeconds(1f);

        SceneTransferData.shouldLoadSave = true;
        SceneTransferData.slotToLoad = targetSlotID;
        int hIdx = headers.FindIndex(hd => hd.slotIndex == targetSlotID);
        SceneTransferData.playTimeToLoad = (hIdx >= 0) ? headers[hIdx].playTime : 0f;
        
        SceneOrchestrator.Instance.GoToGame();
    }

    private void ActionDelete()
    {
        SaveSystem.DeleteSlot(targetSlotID);
        headers = SaveSystem.LoadHeaders();
        if (headers.Count == 0) focusOnButtons = true;
        else if (slotIndex >= headers.Count) slotIndex = headers.Count - 1;
        RefreshCargarVisuals();
        ReturnFromPopup();
    }

    // ===================== SUB OPCIONES =====================
    private void SetupOpciones()
    {
        if (LocalizationManager.Instance != null)
            opcionesIndex = (LocalizationManager.Instance.currentLanguage == "EN") ? 1 : 0;
        else
            opcionesIndex = 0;
        RefreshOpcionesVisuals();
    }

    private void UpdateSubOpciones()
    {
        bool up, down, left, right, a, b, start, x;
        ReadInput(out up, out down, out left, out right, out a, out b, out start, out x);

        if (b || start)
        {
            StartCoroutine(CloseSubMenu(menuOpciones));
            return;
        }

        if (up || down)
        {
            opcionesIndex += up ? -1 : 1;
            if (opcionesIndex < 0) opcionesIndex = OPCIONES_COUNT - 1;
            if (opcionesIndex >= OPCIONES_COUNT) opcionesIndex = 0;
            RefreshOpcionesVisuals();
        }

        if (a)
        {
            if (opcionesIndex == 0)
            {
                if (LocalizationManager.Instance != null) LocalizationManager.Instance.SetLanguage("ES");
                RefreshAllTexts();
            }
            else if (opcionesIndex == 1)
            {
                if (LocalizationManager.Instance != null) LocalizationManager.Instance.SetLanguage("EN");
                RefreshAllTexts();
            }
            else if (opcionesIndex == 2)
            {
                StartCoroutine(CloseSubMenu(menuOpciones));
            }
        }
    }

    private void RefreshOpcionesVisuals()
    {
        SetTextAlpha(txtEspanol, opcionesIndex == 0);
        SetTextAlpha(txtIngles, opcionesIndex == 1);
        
        if (btnVolverOpcionesCG != null)
            btnVolverOpcionesCG.alpha = (opcionesIndex == 2) ? 1f : 0.5f;
    }

    private void UpdateSubCreditos()
    {
        bool up, down, left, right, a, b, start, x;
        ReadInput(out up, out down, out left, out right, out a, out b, out start, out x);

        if (btnVolverCreditosCG != null) btnVolverCreditosCG.alpha = 1f;

        if (b || start || a)
        {
            StartCoroutine(CloseSubMenu(menuCreditos));
        }
    }

    // ===================== LOCALIZATION REFRESH =====================
    private void RefreshAllTexts()
    {
        if (LocalizationManager.Instance == null) return;
        var loc = LocalizationManager.Instance;

        // Main menu texts
        if (mainTexts.Length >= 4)
        {
            mainTexts[0].text = loc.Get("inicio");
            mainTexts[1].text = loc.Get("cargar");
            mainTexts[2].text = loc.Get("opciones");
            mainTexts[3].text = loc.Get("creditos");
        }

        // Opciones texts
        if (txtEspanol != null) txtEspanol.text = loc.Get("espanol");
        if (txtIngles != null) txtIngles.text = loc.Get("ingles");
        if (txtVolverOpciones != null) txtVolverOpciones.text = loc.Get("volver");

        // Cargar
        if (titleCargar != null) titleCargar.text = loc.Get("cargar");
        if (btnVolverText != null) btnVolverText.text = loc.Get("volver");

        // Creditos Volver
        if (txtVolverCreditos != null) txtVolverCreditos.text = loc.Get("volver");

        // Popup texts
        if (txtPreguntaCargar != null) txtPreguntaCargar.text = loc.Get("confirmar_carga_menu");
        if (cargaPopupTexts != null && cargaPopupTexts.Length >= 2)
        {
            cargaPopupTexts[0].text = loc.Get("si");
            cargaPopupTexts[1].text = loc.Get("no");
        }
        
        if (txtPreguntaBorrar != null) txtPreguntaBorrar.text = loc.Get("confirmar_borrado");
        if (borradoPopupTexts != null && borradoPopupTexts.Length >= 2)
        {
            borradoPopupTexts[0].text = loc.Get("si");
            borradoPopupTexts[1].text = loc.Get("no");
        }

        if (txtCreditosTexto != null) txtCreditosTexto.text = loc.Get("creditos_texto");

        RefreshMainVisuals();
        RefreshOpcionesVisuals();
    }

    // ===================== UTILITIES =====================
    private void HideAllSubs()
    {
        SetGroupOff(menuCargar);
        SetGroupOff(menuOpciones);
        SetGroupOff(menuCreditos);
        SetGroupOff(confirmarCarga);
        SetGroupOff(confirmarBorrado);
    }

    private void SetGroupOff(CanvasGroup g)
    {
        if (g == null) return;
        g.alpha = 0;
        g.gameObject.SetActive(false);
    }

    private void SetTextAlpha(Text t, bool selected)
    {
        if (t == null) return;
        Color c = t.color;
        c.a = selected ? 1f : 0.49f;
        t.color = c;
    }

    private IEnumerator FadeOutIntro()
    {
        if (cuadroInicial1 == null && cuadroInicial2 == null) yield break;

        // Activar los GameObjects (pueden estar desactivados en el editor)
        if (cuadroInicial1 != null) { cuadroInicial1.gameObject.SetActive(true); Color ic1 = cuadroInicial1.color; ic1.a = 1f; cuadroInicial1.color = ic1; }
        if (cuadroInicial2 != null) { cuadroInicial2.gameObject.SetActive(true); Color ic2 = cuadroInicial2.color; ic2.a = 1f; cuadroInicial2.color = ic2; }

        float elapsed = 0f;
        Color c1 = cuadroInicial1 != null ? cuadroInicial1.color : Color.black;
        Color c2 = cuadroInicial2 != null ? cuadroInicial2.color : Color.black;

        while (elapsed < introFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / introFadeDuration;
            if (cuadroInicial1 != null) { c1.a = Mathf.Lerp(1f, 0f, t); cuadroInicial1.color = c1; }
            if (cuadroInicial2 != null) { c2.a = Mathf.Lerp(1f, 0f, t); cuadroInicial2.color = c2; }
            yield return null;
        }

        if (cuadroInicial1 != null) { c1.a = 0; cuadroInicial1.color = c1; cuadroInicial1.gameObject.SetActive(false); }
        if (cuadroInicial2 != null) { c2.a = 0; cuadroInicial2.color = c2; cuadroInicial2.gameObject.SetActive(false); }
    }

    private IEnumerator FadeBGMVolume(float target, float duration)
    {
        if (bgmSource == null) yield break;
        float startVol = bgmSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, target, elapsed / duration);
            yield return null;
        }
        bgmSource.volume = target;
        if (target <= 0.001f) bgmSource.Stop();
    }

    private IEnumerator FadeEffectImages(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(from, to, elapsed / duration);
            SetEffectImageAlpha(a);
            yield return null;
        }
        SetEffectImageAlpha(to);
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}
