using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    public Image effectUpper;
    public Image effectLower;
    public Image backEventEffectUpper;
    public Image backEventEffectLower;
    public Image imageEffect;
    public Image[] transitionPool;
    public SFXManager sfxManager;
    public DialogueManager dialogueManager;
    public GameObject snowCanvas;
    public Camera upperCamera;

    public bool isVisualBusy = false;
    private Coroutine upperFade;
    private Coroutine lowerFade;
    private Coroutine backUpperFade;
    private Coroutine backLowerFade;
    private Coroutine activeVfx;
    private Coroutine activeMedicalText;
    private Coroutine cameraFade;
    private float targetUpperAlpha = 0;
    private float targetLowerAlpha = 0;
    private float targetBackUpperAlpha = 0;
    private float targetBackLowerAlpha = 0;
    private float targetVfxAlpha = 0;
    private AssetBundle vfxBundle;

    // Track state for saving
    [HideInInspector] public bool currentVfxActive;
    [HideInInspector] public int currentVfxSpriteHash;
    [HideInInspector] public float currentVfxAlpha;
    [HideInInspector] public Vector2 currentVfxSize;
    [HideInInspector] public Vector2 currentVfxPos;

    void Awake()
    {
        SetupEffect(effectUpper);
        SetupEffect(effectLower);
        SetupEffect(backEventEffectUpper);
        SetupEffect(backEventEffectLower);
        SetupEffect(imageEffect);
    }

    public void LoadBundle(string bundleName)
    {
        if (vfxBundle != null) { vfxBundle.Unload(true); vfxBundle = null; }
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, bundleName);
        if (System.IO.File.Exists(path)) vfxBundle = AssetBundle.LoadFromFile(path);
    }

    public void UnloadContent()
    {
        StopAllCoroutines();
        // IMPORTANTE OLD 3DS: Unload(false)
        if (vfxBundle != null) { vfxBundle.Unload(false); vfxBundle = null; }
    }

    private void SetupEffect(Image img)
    {
        if (img == null) return;
        Color c = img.color; c.a = 0;
        img.color = c;
        img.gameObject.SetActive(true);
    }

    public void ExecuteEvent(EventData data)
    {
        if (data.eventType == 9)
        {
            GameManager.Instance.persistentSaveLock = false;
        }
        else if (data.eventType == 8)
        {
            GameManager.Instance.persistentSaveLock = true;
            HandleFlashback(data);
        }
        else if (data.eventType == 10)
        {
            GameManager.Instance.persistentSaveLock = true;
        }
        else if (data.eventType == 11)
        {
            GameManager.Instance.persistentSaveLock = false;
            if (dialogueManager != null)
            {
                dialogueManager.FadeOutBBS();
            }
        }
        else if (data.eventType == 25) // paneoIn
        {
            GameManager.Instance.persistentSaveLock = true;
            HandlePaneoIn(data);
        }
        else if (data.eventType == 26) // paneoOut
        {
            GameManager.Instance.persistentSaveLock = true;
            HandlePaneoOut(data);
        }
        else if (data.eventType == 7)
        {
            currentVfxActive = false; // El pulso normal no es persistente
            ProcessVfx(imageEffect, data.targetAlpha, data.timeIn, data.timeOut, "heart_attack", data.sizeX, data.sizeY, data.posX, data.posY);
        }
        else if (data.eventType == 12)
        {
            // HeartStop: Persistente
            currentVfxActive = true;
            currentVfxSpriteHash = HashHelper.GetID("heart_attack");
            currentVfxAlpha = data.targetAlpha;
            currentVfxSize = new Vector2(data.sizeX, data.sizeY);
            currentVfxPos = new Vector2(data.posX, data.posY);
            ProcessVfx(imageEffect, data.targetAlpha, data.timeIn, 0f, "heart_attack", data.sizeX, data.sizeY, data.posX, data.posY);
        }
        else if (data.eventType == 13) // clearAll: FadeOut ImageEffect + ImageTransitions, luego limpiar
        {
            StartCoroutine(HandleClearAll(data.tiempo));
        }
        else if (data.eventType == 14) // restoreInputs
        {
            GameManager.Instance.inputLock = false;
        }
        else if (data.eventType == 15) // clearImageEffect: FadeOut ImageEffect, luego limpiar
        {
            StartCoroutine(HandleClearImageEffect(data.tiempo));
        }
        else if (data.eventType == 16) // hideSnow
        {
            if (snowCanvas != null) snowCanvas.SetActive(false);
        }
        else if (data.eventType == 17) // showSnow
        {
            if (snowCanvas != null) snowCanvas.SetActive(true);
        }
        else if (data.eventType == 18) // centerText
        {
            if (dialogueManager != null) dialogueManager.useCenterText = true;
        }
        else if (data.eventType == 23) // medicalText
        {
            GameManager.Instance.persistentSaveLock = true;
            if (activeMedicalText != null) StopCoroutine(activeMedicalText);
            activeMedicalText = StartCoroutine(HandleMedicalText(data.tiempo, data.sizeX, data.sizeY));
        }
        else if (data.eventType == 24) // medicalTextOut
        {
            HandleMedicalTextOut();
        }
        else if (data.eventType >= 19 && data.eventType <= 22)
        {
            // Map the "back-" variants to standard types for color processing
            int mapType = data.eventType - 18; // 19->1, 20->2, 21->3, 22->4
            ProcessVisual(backEventEffectUpper, mapType, data.tiempo, data.sizeX, data.sizeY, data.posX, data.posY);
        }
        else if (data.eventType == 5)
        {
            // Es un evento puro de SFX, no hacemos nada visual.
        }
        else
        {
            ProcessVisual(effectUpper, data.eventType, data.tiempo, data.sizeX, data.sizeY, data.posX, data.posY);
        }
        
        if (data.lowerType >= 19 && data.lowerType <= 22)
        {
            int mapType = data.lowerType - 18;
            ProcessVisual(backEventEffectLower, mapType, data.tiempo, data.lowerSizeX, data.lowerSizeY, data.lowerPosX, data.lowerPosY);
        }
        else
        {
            ProcessVisual(effectLower, data.lowerType, data.tiempo, data.lowerSizeX, data.lowerSizeY, data.lowerPosX, data.lowerPosY);
        }
        
        if (sfxManager != null)
        {
            sfxManager.ExecuteSFX(data.sfxHash, data.sfxAction, data.sfxVolume, data.tiempo);
        }
    }

    private void ProcessVisual(Image img, int type, float time, float sx, float sy, float px, float py)
    {
        if (img == null || type == 0 || type == 6) return;

        img.gameObject.SetActive(true);

        if (sx > 0 && sy > 0) img.rectTransform.sizeDelta = new Vector2(sx, sy);
        img.rectTransform.anchoredPosition = new Vector2(px, py);

        Color col = (type == 1 || type == 2) ? Color.white : Color.black;
        bool isFadeIn = (type == 1 || type == 3);

        if (img == effectUpper) { if (upperFade != null) StopCoroutine(upperFade); targetUpperAlpha = isFadeIn ? 1 : 0; }
        else if (img == effectLower) { if (lowerFade != null) StopCoroutine(lowerFade); targetLowerAlpha = isFadeIn ? 1 : 0; }
        else if (img == backEventEffectUpper) { if (backUpperFade != null) StopCoroutine(backUpperFade); targetBackUpperAlpha = isFadeIn ? 1 : 0; }
        else if (img == backEventEffectLower) { if (backLowerFade != null) StopCoroutine(backLowerFade); targetBackLowerAlpha = isFadeIn ? 1 : 0; }

        col.a = isFadeIn ? 0 : 1f;
        img.color = col;

        Coroutine c = StartCoroutine(HandleEffectFade(img, isFadeIn ? 1 : 0, time));
        if (img == effectUpper) 
        { 
            upperFade = c; 
            if (isFadeIn && (type == 1 || type == 3) && upperCamera != null)
            {
                if (cameraFade != null) StopCoroutine(cameraFade);
                Color targetCol = (type == 1) ? Color.white : Color.black;
                cameraFade = StartCoroutine(HandleCameraFade(targetCol, time));
            }
        }
        else if (img == effectLower) lowerFade = c;
        else if (img == backEventEffectUpper) backUpperFade = c;
        else if (img == backEventEffectLower) backLowerFade = c;
    }

    private IEnumerator HandleCameraFade(Color targetColor, float duration)
    {
        Color startColor = upperCamera.backgroundColor;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            upperCamera.backgroundColor = Color.Lerp(startColor, targetColor, elapsed / duration);
            yield return null;
        }
        upperCamera.backgroundColor = targetColor;
        cameraFade = null;
    }

	private IEnumerator HandleEffectFade(Image img, float target, float dur)
	{
		isVisualBusy = true;
		yield return StartCoroutine(UIAnimationHelper.FadeImage(img, img.color.a, target, dur));
		
		if (target <= 0.01f) 
		{
			img.gameObject.SetActive(false);
		}

		if (img == effectUpper) upperFade = null; 
        else if (img == effectLower) lowerFade = null;
        else if (img == backEventEffectUpper) backUpperFade = null;
        else if (img == backEventEffectLower) backLowerFade = null;

		UpdateVisualStatus();
	}

    private void ProcessVfx(Image img, float tAlpha, float tIn, float tOut, string spriteName, float sx, float sy, float px, float py)
    {
        if (img == null || vfxBundle == null) return;
        
        Sprite sp = vfxBundle.LoadAsset<Sprite>(spriteName);
        if (sp != null) img.sprite = sp;

        if (sx > 0 && sy > 0) img.rectTransform.sizeDelta = new Vector2(sx, sy);
        img.rectTransform.anchoredPosition = new Vector2(px, py);

        img.gameObject.SetActive(true);
        Color col = img.color;
        col.a = activeVfx == null ? 0 : col.a; 
        img.color = col;

        if (activeVfx != null) StopCoroutine(activeVfx);
        targetVfxAlpha = tAlpha; 
        activeVfx = StartCoroutine(HandleVfxPulse(img, tAlpha, tIn, tOut));
    }

    private IEnumerator HandleVfxPulse(Image img, float tAlpha, float tIn, float tOut)
    {
        isVisualBusy = true;
        yield return StartCoroutine(UIAnimationHelper.FadeImage(img, img.color.a, tAlpha, tIn));
        
        if (tOut > 0)
        {
            yield return StartCoroutine(UIAnimationHelper.FadeImage(img, tAlpha, 0f, tOut));
            img.gameObject.SetActive(false);
            currentVfxActive = false;
        }
        else
        {
            // Persistente
            currentVfxActive = true;
        }

        activeVfx = null;
        UpdateVisualStatus();
    }

    private void UpdateVisualStatus()
    {
        isVisualBusy = (upperFade != null || lowerFade != null || backUpperFade != null || backLowerFade != null || activeVfx != null);
    }

    public void StopAll()
    {
        if (upperFade != null) StopCoroutine(upperFade);
        if (lowerFade != null) StopCoroutine(lowerFade);
        if (backUpperFade != null) StopCoroutine(backUpperFade);
        if (backLowerFade != null) StopCoroutine(backLowerFade);
        if (activeVfx != null) StopCoroutine(activeVfx);
        if (activeMedicalText != null) StopCoroutine(activeMedicalText);
        if (cameraFade != null) StopCoroutine(cameraFade);
        
        upperFade = null;
        lowerFade = null;
        backUpperFade = null;
        backLowerFade = null;
        activeVfx = null;
        activeMedicalText = null;
        cameraFade = null;

        targetUpperAlpha = 0;
        targetLowerAlpha = 0;
        targetBackUpperAlpha = 0;
        targetBackLowerAlpha = 0;
        targetVfxAlpha = 0;

        if (effectUpper != null) effectUpper.gameObject.SetActive(false);
        if (effectLower != null) effectLower.gameObject.SetActive(false);
        if (backEventEffectUpper != null) backEventEffectUpper.gameObject.SetActive(false);
        if (backEventEffectLower != null) backEventEffectLower.gameObject.SetActive(false);
        if (imageEffect != null)
        {
            imageEffect.gameObject.SetActive(false);
            imageEffect.sprite = null;
        }

        currentVfxActive = false;
        currentVfxSpriteHash = 0;
        currentVfxAlpha = 0;

        if (transitionPool != null)
        {
            foreach (var img in transitionPool) if (img != null) img.gameObject.SetActive(false);
        }

        UpdateVisualStatus();
    }

    // clearAll: FadeOut del ImageEffect y las ImageTransition, luego desactivar
    private IEnumerator HandleClearAll(float duration)
    {
        isVisualBusy = true;
        List<Coroutine> fades = new List<Coroutine>();

        // FadeOut ImageEffect si está activo
        if (imageEffect != null && imageEffect.gameObject.activeSelf)
        {
            if (activeVfx != null) { StopCoroutine(activeVfx); activeVfx = null; }
            fades.Add(StartCoroutine(UIAnimationHelper.FadeImage(imageEffect, imageEffect.color.a, 0f, duration)));
        }

        if (activeMedicalText != null) { StopCoroutine(activeMedicalText); activeMedicalText = null; }
        medicalStopFlag = true;

        // FadeOut cada ImageTransition activa
        if (transitionPool != null)
        {
            foreach (var img in transitionPool)
            {
                if (img != null && img.gameObject.activeSelf)
                {
                    fades.Add(StartCoroutine(UIAnimationHelper.FadeImage(img, img.color.a, 0f, duration)));
                }
            }
        }

        // Esperar a que terminen todos los fades
        foreach (var f in fades) yield return f;

        // Limpiar y desactivar
        if (imageEffect != null)
        {
            imageEffect.sprite = null;
            imageEffect.gameObject.SetActive(false);
        }
        currentVfxActive = false;

        if (transitionPool != null)
        {
            foreach (var img in transitionPool)
            {
                if (img != null) { img.sprite = null; img.gameObject.SetActive(false); }
            }
        }

        GameManager.Instance.persistentSaveLock = false;
        medicalStopFlag = false;

        isVisualBusy = false;
        UpdateVisualStatus();
    }

    // clearImageEffect: FadeOut solo del ImageEffect, luego desactivar
    private IEnumerator HandleClearImageEffect(float duration)
    {
        if (imageEffect == null || !imageEffect.gameObject.activeSelf) yield break;

        isVisualBusy = true;
        if (activeVfx != null) { StopCoroutine(activeVfx); activeVfx = null; }

        yield return StartCoroutine(UIAnimationHelper.FadeImage(imageEffect, imageEffect.color.a, 0f, duration));

        imageEffect.sprite = null;
        imageEffect.gameObject.SetActive(false);
        currentVfxActive = false;

        isVisualBusy = false;
        UpdateVisualStatus();
    }


    private Image GetFreeTransitionImage(Image exclude = null)
    {
        if (transitionPool == null) return null;
        for (int i = 0; i < transitionPool.Length; i++)
        {
            if (transitionPool[i] != null && !transitionPool[i].gameObject.activeSelf && transitionPool[i] != exclude)
                return transitionPool[i];
        }
        return null;
    }

    private bool medicalStopFlag = false;

    private IEnumerator HandleMedicalText(float duration, float sizeX, float sizeY)
    {
        medicalStopFlag = false;
        float elapsed = 0f;
        int imgIndex = 1;

        while (elapsed < duration)
        {
            Image img = GetFreeTransitionImage();
            if (img != null && vfxBundle != null)
            {
                string lang = LocalizationManager.Instance != null && LocalizationManager.Instance.currentLanguage == "EN" ? "En" : "Es";
                string spriteName = "med" + imgIndex + lang;
                Sprite sp = vfxBundle.LoadAsset<Sprite>(spriteName);
                if (sp != null)
                {
                    img.sprite = sp;
                    StartCoroutine(HandleMedicalFlight(img, sizeX, sizeY));
                }

                imgIndex++;
                if (imgIndex > 5) imgIndex = 1;
            }

            // Wait 3 seconds per spawn
            float waitTimer = 0;
            while (waitTimer < 3f && elapsed < duration)
            {
                waitTimer += Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        medicalStopFlag = true;
        activeMedicalText = null;
        UpdateVisualStatus();
    }

    private IEnumerator HandleMedicalFlight(Image img, float sizeX, float sizeY)
    {
        float startHeight = 0f;
        float startX = Random.Range(55f, 455f);
        img.rectTransform.anchoredPosition = new Vector2(startX, startHeight); // Aparecen en random entre X=55 y X=455
        img.rectTransform.sizeDelta = new Vector2(sizeX, sizeY); 
        img.gameObject.SetActive(true);
        SetAlpha(img, 0);

        float flightDuration = 20f; // Aumentado para que tarden más en cruzar la pantalla
        float elapsed = 0f;
        float fadeInDuration = 1.0f;
        float currentX = startX;

        while (elapsed < flightDuration && img.gameObject.activeSelf)
        {
            elapsed += Time.deltaTime;
            
            // Solo avanza y se mueve si LA BANDERA AUN NO SE HA ENCENDIDO
            if (!medicalStopFlag)
            {
                currentX = Mathf.Lerp(startX, -455f, elapsed / flightDuration);
                img.rectTransform.anchoredPosition = new Vector2(currentX, startHeight);
            }

            // El Alfa sigue su curso independientemente (permitiendo que los ultimos tengan su fade in completo)
            if (elapsed < fadeInDuration)
            {
                SetAlpha(img, Mathf.Lerp(0f, 1f, elapsed / fadeInDuration));
            }
            else
            {
                SetAlpha(img, 1f);
            }

            // Si llegamos a detenernos, y YA ESTÁ en alpha 1, se rompe el ciclo para quedarse ahi flotando 
            if (medicalStopFlag && elapsed >= fadeInDuration)
            {
                break;
            }

            yield return null;
        }

        // Solo apagarlo por sí mismo si genuinamente terminó de volar en el espacio/tiempo
        if (elapsed >= flightDuration)
        {
            img.gameObject.SetActive(false);
            img.sprite = null;
        }
    }

    private void HandleMedicalTextOut()
    {
        if (activeMedicalText != null) { StopCoroutine(activeMedicalText); activeMedicalText = null; }
        
        if (transitionPool != null)
        {
            foreach (var img in transitionPool)
            {
                if (img != null && img.gameObject.activeSelf)
                {
                    img.gameObject.SetActive(false);
                    img.sprite = null;
                }
            }
        }
        
        GameManager.Instance.persistentSaveLock = false;
        medicalStopFlag = false;
    }

    private void HandleFlashback(EventData data)
    {
        if (vfxBundle == null) return;

        Image img1 = GetFreeTransitionImage();
        Image img2 = GetFreeTransitionImage(img1);

        if (img1 == null || img2 == null) return;

        Sprite sp1 = vfxBundle.LoadAsset<Sprite>("flashback1");
        Sprite sp2 = vfxBundle.LoadAsset<Sprite>("flashback2");

        if (sp1 != null) img1.sprite = sp1;
        if (sp2 != null) img2.sprite = sp2;

        img1.rectTransform.sizeDelta = new Vector2(450, 240);
        img2.rectTransform.sizeDelta = new Vector2(450, 240);

        img1.rectTransform.anchoredPosition = new Vector2(data.posX, data.posY);
        img2.rectTransform.anchoredPosition = new Vector2(data.posX, data.posY);

        // FIX: Forzar Pivot 0.5 y Color Blanco por seguridad
        img1.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img2.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        Color c = Color.white; c.a = 0;
        img1.color = c; img2.color = c;

        img1.gameObject.SetActive(true);
        img2.gameObject.SetActive(true);

        StartCoroutine(HandleFlashbackSequence(img1, img2, data.tiempo));
    }

    private IEnumerator HandleFlashbackSequence(Image img1, Image img2, float duration)
    {
        StartCoroutine(UIAnimationHelper.FadeImage(img1, 0f, 1f, duration));
        
        yield return new WaitForSeconds(duration / 4f);
        
        yield return StartCoroutine(UIAnimationHelper.FadeImage(img2, 0f, 0.25f, duration / 8f));
        
        yield return StartCoroutine(UIAnimationHelper.FadeImage(img2, 0.25f, 1f, duration * 0.75f));
    }

    private void HandlePaneoIn(EventData data)
    {
        List<Image> shutters = new List<Image>();
        for (int i = 0; i < 5; i++)
        {
            Image img = GetFreeTransitionImage();
            if (img != null)
            {
                img.sprite = null;
                img.color = Color.black;
                img.rectTransform.sizeDelta = new Vector2(0, 240);
                img.rectTransform.pivot = new Vector2(0f, 0.5f);
                float xPos = -200f + (i * 80f);
                img.rectTransform.anchoredPosition = new Vector2(xPos, 0f);
                img.gameObject.SetActive(true);
                shutters.Add(img);
            }
        }

        if (shutters.Count > 0) StartCoroutine(HandlePaneoInSequence(shutters, data.tiempo));
    }

    private IEnumerator HandlePaneoInSequence(List<Image> shutters, float totalDuration)
    {
        isVisualBusy = true;
        float shutterDuration = totalDuration / 2f;
        float staggerDelay = shutterDuration * 0.25f;

        for (int i = 0; i < shutters.Count; i++)
        {
            StartCoroutine(AnimateShutterWidth(shutters[i], 0f, 80f, shutterDuration));
            if (i < shutters.Count - 1) yield return new WaitForSeconds(staggerDelay);
        }

        // Esperar a que el ultimo termine
        yield return new WaitForSeconds(shutterDuration - staggerDelay);
        isVisualBusy = false;
        UpdateVisualStatus();
    }

    private void HandlePaneoOut(EventData data)
    {
        List<Image> shutters = new List<Image>();
        // Buscamos las que esten activas y sean negras (las de PaneoIn)
        if (transitionPool != null)
        {
            for (int i = 0; i < transitionPool.Length; i++)
            {
                if (transitionPool[i] != null && transitionPool[i].gameObject.activeSelf && transitionPool[i].color == Color.black)
                {
                    shutters.Add(transitionPool[i]);
                }
            }
        }

        // Si por algun motivo no hay activas, forzamos la creacion para que el efecto visual ocurra igual
        if (shutters.Count < 5)
        {
            shutters.Clear();
            for (int i = 0; i < 5; i++)
            {
                Image img = GetFreeTransitionImage();
                if (img != null)
                {
                    img.sprite = null;
                    img.color = Color.black;
                    img.rectTransform.sizeDelta = new Vector2(80, 240);
                    img.rectTransform.pivot = new Vector2(1f, 0.5f);
                    float xPos = -200f + (i * 80f);
                    img.rectTransform.anchoredPosition = new Vector2(xPos + 80f, 0f); // Ajuste por pivot 1
                    img.gameObject.SetActive(true);
                    shutters.Add(img);
                }
            }
        }
        else
        {
            // Ordenar por posicion X para asegurar que el barrido sea de izquierda a derecha
            shutters.Sort((a, b) => a.rectTransform.anchoredPosition.x.CompareTo(b.rectTransform.anchoredPosition.x));
            foreach (var s in shutters)
            {
                Vector2 pos = s.rectTransform.anchoredPosition;
                s.rectTransform.pivot = new Vector2(1f, 0.5f);
                s.rectTransform.anchoredPosition = new Vector2(pos.x + 80f, pos.y); 
            }
        }

        if (shutters.Count > 0) StartCoroutine(HandlePaneoOutSequence(shutters, data.tiempo));
    }

    private IEnumerator HandlePaneoOutSequence(List<Image> shutters, float totalDuration)
    {
        isVisualBusy = true;
        float shutterDuration = totalDuration / 2f;
        float staggerDelay = shutterDuration * 0.25f;

        for (int i = 0; i < shutters.Count; i++)
        {
            StartCoroutine(AnimateShutterWidth(shutters[i], 80f, 0f, shutterDuration));
            if (i < shutters.Count - 1) yield return new WaitForSeconds(staggerDelay);
        }

        yield return new WaitForSeconds(shutterDuration - staggerDelay);

        // Limpieza y reset de pivots
        foreach (var s in shutters)
        {
            if (s != null)
            {
                s.gameObject.SetActive(false);
                s.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        isVisualBusy = false;
        UpdateVisualStatus();
    }

    private IEnumerator AnimateShutterWidth(Image img, float start, float end, float duration)
    {
        float elapsed = 0;
        RectTransform rt = img.rectTransform;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float w = Mathf.Lerp(start, end, elapsed / duration);
            rt.sizeDelta = new Vector2(w, 240);
            yield return null;
        }
        rt.sizeDelta = new Vector2(end, 240);
    }

    public void InstantFinish(int canForce)
    {
        if (canForce == 0) return;
        
        if (upperFade != null) 
        { 
            StopCoroutine(upperFade); 
            upperFade = null; 
            SetAlpha(effectUpper, targetUpperAlpha); 
        }
        
        if (lowerFade != null) 
        { 
            StopCoroutine(lowerFade); 
            lowerFade = null; 
            SetAlpha(effectLower, targetLowerAlpha); 
        }

        if (backUpperFade != null) 
        { 
            StopCoroutine(backUpperFade); 
            backUpperFade = null; 
            SetAlpha(backEventEffectUpper, targetBackUpperAlpha); 
        }

        if (backLowerFade != null) 
        { 
            StopCoroutine(backLowerFade); 
            backLowerFade = null; 
            SetAlpha(backEventEffectLower, targetBackLowerAlpha); 
        }
        
        if (activeVfx != null) 
        { 
            StopCoroutine(activeVfx); 
            activeVfx = null; 
            SetAlpha(imageEffect, targetVfxAlpha); 
            if (targetVfxAlpha <= 0.01f) 
            {
                imageEffect.gameObject.SetActive(false); 
                currentVfxActive = false;
            }
            else
            {
                currentVfxActive = true;
            }
        }
        
        if (cameraFade != null)
        {
            StopCoroutine(cameraFade);
            cameraFade = null;
        }

        isVisualBusy = false;
    }

    private void SetAlpha(Image img, float a) 
    { 
        Color c = img.color; 
        c.a = a; 
        img.color = c; 
    }

    public void RestoreVfx(bool active, int spriteHash, float alpha, Vector2 size, Vector2 pos)
    {
        if (activeVfx != null) StopCoroutine(activeVfx);
        activeVfx = null;

        currentVfxActive = active;
        currentVfxSpriteHash = spriteHash;
        currentVfxAlpha = alpha;
        currentVfxSize = size;
        currentVfxPos = pos;
        targetVfxAlpha = active ? alpha : 0;

        if (!active)
        {
            imageEffect.gameObject.SetActive(false);
            imageEffect.sprite = null;
            return;
        }

        if (vfxBundle != null)
        {
            string spriteName = (spriteHash == HashHelper.GetID("heart_attack")) ? "heart_attack" : "";
            if (!string.IsNullOrEmpty(spriteName))
            {
                imageEffect.sprite = vfxBundle.LoadAsset<Sprite>(spriteName);
            }
        }

        imageEffect.rectTransform.sizeDelta = size;
        imageEffect.rectTransform.anchoredPosition = pos;
        imageEffect.gameObject.SetActive(true);
        SetAlpha(imageEffect, alpha);
        UpdateVisualStatus();
    }

    public void ClearVFXReferences()
    {
        if (imageEffect != null) imageEffect.sprite = null;
        if (transitionPool != null)
        {
            for (int i = 0; i < transitionPool.Length; i++)
                if (transitionPool[i] != null) transitionPool[i].sprite = null;
        }
    }

    void OnDestroy()
    {
        UnloadContent();
    }
}