using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class CharacterSpriteManager : MonoBehaviour
{
    [System.Serializable]
    public class PartReference
    {
        public Image mainImg, subImg;
        public int baseSiblingIndex;
        [HideInInspector] public bool isSwapped = false, isPartAnimating = false, isPartSwitching = false;
        [HideInInspector] public float targetAlpha = 0;
        [HideInInspector] public float targetPosX = 0, targetPosY = 0;
        [HideInInspector] public bool hasTargetPos = false;
        [HideInInspector] public int loadedMainHash = 0, loadedSubHash = 0;
        [HideInInspector] public Coroutine activeRoutine = null;

        public Image GetCurrent() { return isSwapped ? subImg : mainImg; }
        public Image GetNext() { return isSwapped ? mainImg : subImg; }
    }

    [System.Serializable]
    public class CharacterSlotRefs { public PartReference cuerpo, cara, cabeza, brazoIzq, brazoDer; }

    public CharacterSlotRefs[] slots = new CharacterSlotRefs[6];
    public float switchFadeInMultiplier = 0.5f;
    private AssetBundle currentBundle;
    private string[] cachedAssetNames;
    private Dictionary<int, string> assetCache = new Dictionary<int, string>();
    public bool isAnimating = false;

    void Awake()
    {
        for (int i = 0; i < 6; i++) 
        { 
            SetupPart(slots[i].cuerpo); 
            SetupPart(slots[i].cara); 
            SetupPart(slots[i].cabeza); 
            SetupPart(slots[i].brazoIzq); 
            SetupPart(slots[i].brazoDer); 
        }
    }

    private void SetupPart(PartReference p) 
    { 
        if (p == null) return;
        SetAlpha(p.mainImg, 0); 
        SetAlpha(p.subImg, 0); 
        if (p.mainImg != null) { p.mainImg.sprite = null; p.mainImg.gameObject.SetActive(false); }
        if (p.subImg != null) { p.subImg.sprite = null; p.subImg.gameObject.SetActive(false); }
    }

    public void LoadBundle(string bundleName) 
    { 
        UnloadContent(); 
        assetCache.Clear(); 
        string path = Path.Combine(Application.streamingAssetsPath, bundleName); 
        if (System.IO.File.Exists(path))
        {
            if (currentBundle != null) { currentBundle.Unload(true); currentBundle = null; }
            currentBundle = AssetBundle.LoadFromFile(path); 
            if (currentBundle != null)
            {
                cachedAssetNames = currentBundle.GetAllAssetNames();
                // Pre-indexado inmediato para evitar búsquedas costosas de strings
                foreach (string name in cachedAssetNames)
                {
                    string clean = name.ToLower().Replace(".png", "").Replace(".jpg", "").Replace(".jpeg", ""); 
                    if (clean.StartsWith("assets/sprites/")) clean = clean.Substring(15); 
                    int hash = HashHelper.GetID(clean);
                    assetCache[hash] = name;
                }
            }
            else
            {
                cachedAssetNames = new string[0];
            }
        }
        else
        {
            cachedAssetNames = new string[0];
        }
    }

    public void UnloadContent() 
    { 
        StopAllCoroutines();

        // IMPORTANTE OLD 3DS: Unload(false) para NO destruir las texturas que están actualmente en pantalla durante el Cambio de Acto.
        if (currentBundle != null) 
        { 
            currentBundle.Unload(false); 
            currentBundle = null; 
        } 
    }

    public void ExecuteAllCharacters(ScriptNode node)
    {
        isAnimating = false;
        ExecuteSlot(slots[0], node.char1); 
        ExecuteSlot(slots[1], node.char2);
        ExecuteSlot(slots[2], node.char3); 
        ExecuteSlot(slots[3], node.char4);
        ExecuteSlot(slots[4], node.char5); 
        ExecuteSlot(slots[5], node.char6);
    }

    private void ExecuteSlot(CharacterSlotRefs s, CharacterSlot data) 
    { 
        ExecutePart(s.cuerpo, data.cuerpo); 
        ExecutePart(s.cara, data.cara); 
        ExecutePart(s.cabeza, data.cabeza); 
        ExecutePart(s.brazoIzq, data.brazoIzq); 
        ExecutePart(s.brazoDer, data.brazoDer); 
    }

    private void ExecutePart(PartReference p, SpritePartData d)
    {
        if (d.fileHash == 0 && d.actionType != 2 && d.actionType != 10) return;
        p.hasTargetPos = false;

        if (p.activeRoutine != null) { StopCoroutine(p.activeRoutine); p.activeRoutine = null; }

        if (d.actionType == 3) 
        {
            p.targetAlpha = 1;
            PrepareAndTrack(p, p.GetNext(), d.switchHash, d.sizeX, d.sizeY, d.posX, d.posY);
            p.GetNext().gameObject.SetActive(true);
            p.activeRoutine = StartCoroutine(HandlePartSwitch(p, d.time));
        }
        else if (d.actionType == 1) 
        {
            p.targetAlpha = 1;
            PrepareAndTrack(p, p.GetCurrent(), d.fileHash, d.sizeX, d.sizeY, d.posX, d.posY);
            p.GetCurrent().gameObject.SetActive(true);
            p.activeRoutine = StartCoroutine(HandlePartFadeIn(p, d.time));
        }
        else if (d.actionType == 2) 
        { 
            p.targetAlpha = 0; 
            p.activeRoutine = StartCoroutine(HandlePartFadeOut(p, d.time)); 
        }
        else if (d.actionType == 4 || d.actionType == 12 || d.actionType == 13) 
        {
            p.targetAlpha = 1;
            PrepareAndTrack(p, p.GetCurrent(), d.fileHash, d.sizeX, d.sizeY, d.posX, d.posY);
            p.GetCurrent().gameObject.SetActive(true);
            SetAlpha(p.GetCurrent(), 1f);

            if (d.actionType == 12 || d.actionType == 13)
            {
                p.targetPosX = d.posX; p.targetPosY = d.posY; p.hasTargetPos = true;
                p.activeRoutine = StartCoroutine(HandlePartShake(p, d, d.actionType == 12));
            }
        }
        else if (d.actionType == 9) 
        {
            p.targetAlpha = 1;
            p.targetPosX = d.posX; p.targetPosY = d.posY; p.hasTargetPos = true;
            PrepareAndTrack(p, p.GetCurrent(), d.fileHash, d.sizeX, d.sizeY, d.startPosX, d.startPosY);
            p.GetCurrent().gameObject.SetActive(true);
            SetAlpha(p.GetCurrent(), 0f);
            p.activeRoutine = StartCoroutine(HandlePartMoveFadeIn(p, d));
        }
        else if (d.actionType == 10) 
        {
            p.targetAlpha = 0;
            p.targetPosX = d.posX; p.targetPosY = d.posY; p.hasTargetPos = true;
            p.activeRoutine = StartCoroutine(HandlePartMoveFadeOut(p, d));
        }
        else if (d.actionType == 5)
        {
            p.targetAlpha = p.GetCurrent().color.a;
            p.targetPosX = d.posX; p.targetPosY = d.posY; p.hasTargetPos = true;
            p.activeRoutine = StartCoroutine(HandlePartMove(p, d));
        }
        else if (d.actionType == 6 || d.actionType == 7)
        {
            p.targetAlpha = p.GetCurrent().color.a;
            p.targetPosX = d.posX; p.targetPosY = d.posY; p.hasTargetPos = true;
            if (d.fileHash != 0) {
                PrepareAndTrack(p, p.GetCurrent(), d.fileHash, d.sizeX, d.sizeY, d.posX, d.posY);
                p.GetCurrent().gameObject.SetActive(p.targetAlpha > 0.01f);
            }
            p.activeRoutine = StartCoroutine(HandlePartShake(p, d, d.actionType == 6));
        }
    }

    public void ExecuteInlineUpdate(int slotIdx, Dictionary<int, string> parameters)
    {
        if (slotIdx < 0 || slotIdx >= slots.Length) return;
        CharacterSlotRefs s = slots[slotIdx];

        Dictionary<int, Dictionary<int, string>> partChanges = new Dictionary<int, Dictionary<int, string>>();

        foreach (var kvp in parameters) {
            int relId = kvp.Key;
            int pIdx = (relId - 1) / 6;
            int aId = (relId - 1) % 6;
            if (!partChanges.ContainsKey(pIdx)) partChanges[pIdx] = new Dictionary<int, string>();
            partChanges[pIdx][aId] = kvp.Value;
        }

        foreach (var pc in partChanges) {
            PartReference p = GetPartRef(s, pc.Key);
            if (p == null) continue;
            SpritePartData d = GetCurrentData(p);
            ApplyChanges(ref d, pc.Value);
            ExecutePart(p, d);
        }
    }

    private PartReference GetPartRef(CharacterSlotRefs s, int pIdx) {
        switch (pIdx) {
            case 0: return s.cuerpo;
            case 1: return s.cara;
            case 2: return s.cabeza;
            case 3: return s.brazoIzq;
            case 4: return s.brazoDer;
        }
        return null;
    }

    private SpritePartData GetCurrentData(PartReference p) {
        SpritePartData d = new SpritePartData();
        d.fileHash = p.loadedMainHash;
        Image img = p.GetCurrent();
        d.posX = img.rectTransform.anchoredPosition.x;
        d.posY = img.rectTransform.anchoredPosition.y;
        d.sizeX = img.rectTransform.sizeDelta.x;
        d.sizeY = img.rectTransform.sizeDelta.y;
        d.startPosX = d.posX; d.startPosY = d.posY;
        d.actionType = 0; d.time = 0;
        return d;
    }

    public void PreloadAsset(int hash)
    {
        if (currentBundle == null || hash == 0) return;
        string assetName = GetAssetName(hash);
        if (!string.IsNullOrEmpty(assetName))
        {
            currentBundle.LoadAsset<Sprite>(assetName);
        }
    }

    private void ApplyChanges(ref SpritePartData d, Dictionary<int, string> changes) {
        foreach (var kvp in changes) {
            int attr = kvp.Key; string val = kvp.Value;
            switch (attr) {
                case 0: d.fileHash = HashHelper.GetID(val.ToLower()); break;
                case 1: Vector2 sz = ParseSize(val); d.sizeX = sz.x; d.sizeY = sz.y; break;
                case 2: d.actionType = GetActionID(val); break;
                case 3: d.switchHash = HashHelper.GetID(val.ToLower()); break;
                case 4: Vector2 pos = ParseCoords(val); d.posX = pos.x; d.posY = pos.y; break;
                case 5: float.TryParse(val, out d.time); break;
            }
        }
    }

    private Vector2 ParseSize(string s) {
        if (string.IsNullOrEmpty(s)) return Vector2.zero;
        string[] p = s.ToLower().Split('x');
        if (p.Length < 2) return Vector2.zero;
        float x, y;
        float.TryParse(p[0], out x); float.TryParse(p[1], out y);
        return new Vector2(x, y);
    }

    private Vector2 ParseCoords(string s) {
        if (string.IsNullOrEmpty(s)) return Vector2.zero;
        string clean = s.Replace("_", " ").Trim();
        string[] p = clean.Split(' ');
        float x = 0, y = 0;
        if (p.Length > 0) float.TryParse(p[0], out x);
        if (p.Length > 1) float.TryParse(p[1], out y);
        return new Vector2(x, y);
    }

    private int GetActionID(string act) {
        act = act.ToLower();
        if (act == "fadein") return 1; if (act == "fadeout") return 2; if (act == "switch") return 3;
        if (act == "visible") return 4; if (act == "move") return 5; 
        if (act.StartsWith("visible_and_shakex")) return 12; if (act.StartsWith("visible_and_shakey")) return 13;
        if (act.StartsWith("shakex")) return 6; if (act.StartsWith("shakey")) return 7; 
        if (act == "zoomout") return 8; if (act == "movefadein") return 9;
        if (act == "movefadeout") return 10; return 0;
    }

    private void PrepareAndTrack(PartReference p, Image img, int hash, float sx, float sy, float px, float py)
    {
        if (currentBundle == null || hash == 0) return;

        bool isAlreadyLoaded = (img == p.mainImg && p.loadedMainHash == hash && img.sprite != null) ||
                               (img == p.subImg && p.loadedSubHash == hash && img.sprite != null);

        if (isAlreadyLoaded)
        {
            img.rectTransform.sizeDelta = new Vector2(sx, sy);
            img.rectTransform.anchoredPosition = new Vector2(px, py);
            return;
        }

        string assetName = GetAssetName(hash);
        if (!string.IsNullOrEmpty(assetName))
        {
            img.sprite = currentBundle.LoadAsset<Sprite>(assetName);
            img.rectTransform.sizeDelta = new Vector2(sx, sy);
            img.rectTransform.anchoredPosition = new Vector2(px, py);
            if (img == p.mainImg) p.loadedMainHash = hash;
            else p.loadedSubHash = hash;
        }
    }

    private string GetAssetName(int hash)
    {
        string n; 
        if (assetCache.TryGetValue(hash, out n)) return n;
        return null;
    }

    private IEnumerator HandlePartFadeIn(PartReference p, float dur) 
    { 
        p.isPartAnimating = true; 
        UpdateGlobalAnimState(); 
        
        yield return StartCoroutine(UIAnimationHelper.FadeImage(p.GetCurrent(), 0, 1, dur)); 
        
        p.isPartAnimating = false; 
        UpdateGlobalAnimState(); 
    }
    
    private IEnumerator HandlePartFadeOut(PartReference p, float dur) 
    { 
        p.isPartAnimating = true; 
        UpdateGlobalAnimState(); 
        
        yield return StartCoroutine(UIAnimationHelper.FadeImage(p.GetCurrent(), p.GetCurrent().color.a, 0, dur)); 
        
        p.GetCurrent().gameObject.SetActive(false); 
        p.isPartAnimating = false; 
        UpdateGlobalAnimState(); 
    }

    private IEnumerator HandlePartMoveFadeIn(PartReference p, SpritePartData d)
    {
        p.isPartAnimating = true;
        UpdateGlobalAnimState();
        
        Vector3 startPos = new Vector3(d.startPosX, d.startPosY, 0);
        Vector3 endPos = new Vector3(d.posX, d.posY, 0);
        
        yield return StartCoroutine(UIAnimationHelper.MoveAndFadeImage(p.GetCurrent().rectTransform, p.GetCurrent(), startPos, endPos, 0, 1, d.time));
        
        p.isPartAnimating = false;
        UpdateGlobalAnimState();
    }

    private IEnumerator HandlePartMoveFadeOut(PartReference p, SpritePartData d)
    {
        p.isPartAnimating = true;
        UpdateGlobalAnimState();
        
        Vector3 startPos = new Vector3(p.GetCurrent().rectTransform.anchoredPosition.x, p.GetCurrent().rectTransform.anchoredPosition.y, 0);
        Vector3 endPos = new Vector3(d.posX, d.posY, 0);
        
        yield return StartCoroutine(UIAnimationHelper.MoveAndFadeImage(p.GetCurrent().rectTransform, p.GetCurrent(), startPos, endPos, p.GetCurrent().color.a, 0, d.time));
        
        p.GetCurrent().gameObject.SetActive(false);
        p.isPartAnimating = false;
        UpdateGlobalAnimState();
    }

    private IEnumerator HandlePartMove(PartReference p, SpritePartData d)
    {
        p.isPartAnimating = true;
        UpdateGlobalAnimState();
        
        Vector3 startPos = new Vector3(p.GetCurrent().rectTransform.anchoredPosition.x, p.GetCurrent().rectTransform.anchoredPosition.y, 0);
        Vector3 endPos = new Vector3(d.posX, d.posY, 0);
        
        yield return StartCoroutine(UIAnimationHelper.MoveAndFadeImage(p.GetCurrent().rectTransform, p.GetCurrent(), startPos, endPos, p.GetCurrent().color.a, p.GetCurrent().color.a, d.time));
        
        p.isPartAnimating = false;
        UpdateGlobalAnimState();
    }

    private IEnumerator HandlePartShake(PartReference p, SpritePartData d, bool isX)
    {
        p.isPartAnimating = true;
        UpdateGlobalAnimState();
        
        yield return null;
        yield return null;
        
        float elapsed = 0;
        float amplitude = d.startPosX;
        float bounces = d.startPosY;
        float duration = d.time;
        Vector2 basePos = new Vector2(d.posX, d.posY);

        if (duration <= 0) elapsed = duration + 1;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = amplitude * Mathf.Sin((elapsed / duration) * bounces * Mathf.PI * 2f);
            if (isX) 
                p.GetCurrent().rectTransform.anchoredPosition = new Vector2(basePos.x + offset, basePos.y);
            else 
                p.GetCurrent().rectTransform.anchoredPosition = new Vector2(basePos.x, basePos.y + offset);
            yield return null;
        }

        p.GetCurrent().rectTransform.anchoredPosition = basePos;
        p.isPartAnimating = false;
        UpdateGlobalAnimState();
    }
    
    private IEnumerator HandlePartSwitch(PartReference p, float dur) 
    { 
        p.isPartAnimating = true; 
        p.isPartSwitching = true; 
        UpdateGlobalAnimState(); 
        
        StartCoroutine(UIAnimationHelper.FadeImage(p.GetNext(), 0, 1, dur * switchFadeInMultiplier)); 
        yield return StartCoroutine(UIAnimationHelper.FadeImage(p.GetCurrent(), 1, 0, dur)); 
        
        FinalizePartSwitch(p); 
    }

    private void FinalizePartSwitch(PartReference p) 
    { 
        SetAlpha(p.GetCurrent(), 0); 
        p.GetCurrent().gameObject.SetActive(false); 
        SetAlpha(p.GetNext(), 1); 
        
        p.isSwapped = !p.isSwapped; 
        p.subImg.transform.SetSiblingIndex(p.isSwapped ? p.baseSiblingIndex + 1 : p.baseSiblingIndex); 
        p.mainImg.transform.SetSiblingIndex(p.isSwapped ? p.baseSiblingIndex : p.baseSiblingIndex + 1); 
        
        p.isPartAnimating = false; 
        p.isPartSwitching = false; 
        UpdateGlobalAnimState(); 
    }

    public List<SpriteState> GetCurrentStates()
    {
        List<SpriteState> states = new List<SpriteState>();
        for (int i = 0; i < 6; i++) 
        { 
            states.Add(GetPartState(slots[i].cuerpo)); 
            states.Add(GetPartState(slots[i].cara)); 
            states.Add(GetPartState(slots[i].cabeza)); 
            states.Add(GetPartState(slots[i].brazoIzq)); 
            states.Add(GetPartState(slots[i].brazoDer)); 
        }
        return states;
    }

    private SpriteState GetPartState(PartReference p)
    {
        SpriteState s = new SpriteState();
        s.isActive = p.GetCurrent().gameObject.activeSelf;
        s.mainHash = p.loadedMainHash;
        s.subHash = p.loadedSubHash;
        s.mainAlpha = p.mainImg.color.a; 
        s.subAlpha = p.subImg.color.a;
        s.mainSizeX = p.mainImg.rectTransform.sizeDelta.x; 
        s.mainSizeY = p.mainImg.rectTransform.sizeDelta.y;
        s.mainPosX = p.mainImg.rectTransform.anchoredPosition.x; 
        s.mainPosY = p.mainImg.rectTransform.anchoredPosition.y;
        s.subSizeX = p.subImg.rectTransform.sizeDelta.x; 
        s.subSizeY = p.subImg.rectTransform.sizeDelta.y;
        s.subPosX = p.subImg.rectTransform.anchoredPosition.x; 
        s.subPosY = p.subImg.rectTransform.anchoredPosition.y;
        s.siblingIndex = p.mainImg.transform.GetSiblingIndex(); 
        s.isSwapped = p.isSwapped;
        return s;
    }

    public void RestoreStates(List<SpriteState> states)
    {
        StopAllCoroutines();
        if (states == null || states.Count == 0) return;
        
        int idx = 0;
        for (int i = 0; i < 6; i++) 
        { 
            RestorePart(slots[i].cuerpo, states[idx++]); 
            RestorePart(slots[i].cara, states[idx++]); 
            RestorePart(slots[i].cabeza, states[idx++]); 
            RestorePart(slots[i].brazoIzq, states[idx++]); 
            RestorePart(slots[i].brazoDer, states[idx++]); 
        }
        isAnimating = false;
    }

    public IEnumerator RestoreStatesCoroutine(List<SpriteState> states)
    {
        StopAllCoroutines();
        if (states == null || states.Count == 0) yield break;

        int idx = 0;
        for (int i = 0; i < 6; i++)
        {
            RestorePart(slots[i].cuerpo, states[idx++]);
            RestorePart(slots[i].cara, states[idx++]);
            RestorePart(slots[i].cabeza, states[idx++]);
            RestorePart(slots[i].brazoIzq, states[idx++]);
            RestorePart(slots[i].brazoDer, states[idx++]);
            
            // Sub-sistema escalonado: procesamos un personaje por frame
            yield return null;
        }
        isAnimating = false;
    }

    private void RestorePart(PartReference p, SpriteState s)
    {
        p.isSwapped = s.isSwapped;
        p.loadedMainHash = s.mainHash;
        p.loadedSubHash = s.subHash;

        // Limpieza de estados de animación para evitar "fantasmas" de sesiones previas
        p.isPartAnimating = false;
        p.isPartSwitching = false;
        p.targetAlpha = s.isSwapped ? s.subAlpha : s.mainAlpha;
        p.hasTargetPos = false;
        p.activeRoutine = null;
        
        RestoreImage(p.mainImg, s.mainHash, s.mainSizeX, s.mainSizeY, s.mainPosX, s.mainPosY);
        RestoreImage(p.subImg, s.subHash, s.subSizeX, s.subSizeY, s.subPosX, s.subPosY);
        
        SetAlpha(p.mainImg, s.mainAlpha); 
        SetAlpha(p.subImg, s.subAlpha);
        
        p.mainImg.gameObject.SetActive(s.mainAlpha > 0.01f);
        p.subImg.gameObject.SetActive(s.subAlpha > 0.01f);
        
        p.subImg.transform.SetSiblingIndex(s.isSwapped ? p.baseSiblingIndex + 1 : p.baseSiblingIndex);
        p.mainImg.transform.SetSiblingIndex(s.isSwapped ? p.baseSiblingIndex : p.baseSiblingIndex + 1);
    }

    private void RestoreImage(Image img, int hash, float sx, float sy, float px, float py)
    {
        img.rectTransform.sizeDelta = new Vector2(sx, sy);
        img.rectTransform.anchoredPosition = new Vector2(px, py);
        
        if (currentBundle == null || hash == 0) 
        { 
            img.sprite = null; 
            return; 
        }
        
        string assetName = GetAssetName(hash);
        if (!string.IsNullOrEmpty(assetName)) 
            img.sprite = currentBundle.LoadAsset<Sprite>(assetName);
        else 
            img.sprite = null;
    }

    public void InstantFinish() 
    { 
        StopAllCoroutines(); 
        for (int i = 0; i < 6; i++) 
        { 
            FinishPart(slots[i].cuerpo); 
            FinishPart(slots[i].cara); 
            FinishPart(slots[i].cabeza); 
            FinishPart(slots[i].brazoIzq); 
            FinishPart(slots[i].brazoDer); 
        } 
        isAnimating = false; 
    }

    private void FinishPart(PartReference p) 
    { 
        if (!p.isPartAnimating) return; 
        
        if (p.isPartSwitching) 
        {
            FinalizePartSwitch(p); 
        }
        else 
        { 
            if (p.targetAlpha > 0.1f) 
            { 
                SetAlpha(p.GetCurrent(), 1); 
                p.GetCurrent().gameObject.SetActive(true); 
            } 
            else 
            { 
                SetAlpha(p.GetCurrent(), 0); 
                p.GetCurrent().gameObject.SetActive(false); 
            } 
        } 
        
        if (p.hasTargetPos)
        {
            p.GetCurrent().rectTransform.anchoredPosition = new Vector2(p.targetPosX, p.targetPosY);
            p.hasTargetPos = false;
        }

        p.isPartAnimating = false; 
        p.isPartSwitching = false; 
        p.activeRoutine = null;
    }

    private void SetAlpha(Image img, float a) 
    { 
        Color c = img.color; 
        c.a = a; 
        img.color = c; 
    }

    private void UpdateGlobalAnimState() 
    { 
        for (int i = 0; i < 6; i++) 
        { 
            if (slots[i].cuerpo.isPartAnimating || slots[i].cara.isPartAnimating || slots[i].cabeza.isPartAnimating || slots[i].brazoIzq.isPartAnimating || slots[i].brazoDer.isPartAnimating) 
            { 
                isAnimating = true; 
                return; 
            } 
        } 
        isAnimating = false; 
    }

    public void ClearSlotReferences(int idx)
    {
        if (idx < 0 || idx >= slots.Length) return;
        CharacterSlotRefs s = slots[idx];
        ClearPart(s.cuerpo); ClearPart(s.cara); ClearPart(s.cabeza);
        ClearPart(s.brazoIzq); ClearPart(s.brazoDer);
    }

    private void ClearPart(PartReference p)
    {
        if (p == null) return;
        if (p.mainImg != null) p.mainImg.sprite = null;
        if (p.subImg != null) p.subImg.sprite = null;
        p.loadedMainHash = 0; p.loadedSubHash = 0;
    }

    void OnDestroy()
    {
        UnloadContent();
    }
}