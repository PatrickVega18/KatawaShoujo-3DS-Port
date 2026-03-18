using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class BackgroundManager : MonoBehaviour
{
    public Image fondoA, fondoB;
    private Image principal, secundario;
    
    private AssetBundle currentBundle;
    private AssetBundle eventosBundle;
    private string[] cachedAssetNames = new string[0];
    private string[] cachedEventAssetNames = new string[0];
    private Dictionary<int, string> assetNameCache = new Dictionary<int, string>();
    
    public bool isAnimating = false;
    private bool isSwitching = false;

    private int currentMainHash = 0;
    private int currentSubHash = 0;

    private Vector2 targetMainSize = new Vector2(400, 240);
    private Vector2 targetSubSize = new Vector2(400, 240);
    private Vector2 targetMainPos = Vector2.zero;
    private Vector2 targetSubPos = Vector2.zero;
    private Coroutine activeFadeIn;
    private Coroutine activeFadeOut;
    private Coroutine activeSwitch;
    private Coroutine activeZoom;
    private Coroutine activeMove;
    private Coroutine activeShake;
    private bool activeMoveIsForce = false;

    void Awake()
    {
        principal = fondoA; secundario = fondoB;

        if (principal != null) principal.gameObject.SetActive(false);
        if (secundario != null) secundario.gameObject.SetActive(false);

        if (secundario != null) secundario.transform.SetAsFirstSibling();
        if (principal != null) principal.transform.SetAsLastSibling();
    }

    private void SetAlpha(Image img, float a) 
    { 
        if(img == null) return;
        Color c = img.color; 
        c.a = a; 
        img.color = c; 
    }

    public void LoadBundle(string bundleName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, bundleName);
        if (System.IO.File.Exists(path))
        {
            if (currentBundle != null) { currentBundle.Unload(true); currentBundle = null; }
            currentBundle = AssetBundle.LoadFromFile(path);
            
            // Pre-indexar hashes para evitar búsquedas lentas durante el juego
            if (currentBundle != null)
            {
                string[] names = currentBundle.GetAllAssetNames();
                IndexNames(names);
            }
        }
    }

    private void IndexNames(string[] names)
    {
        string[] allCollections = new string[] { "bgs/", "bgs/event/", "bgs/events/", "bg/", "event/", "events/" };
        foreach (string name in names)
        {
            string cleanName = Path.GetFileNameWithoutExtension(name).ToLower();
            // Indexar nombre directo
            assetNameCache[HashHelper.GetID(cleanName)] = name;
            // Indexar con prefijos comunes
            foreach (string prefix in allCollections)
            {
                assetNameCache[HashHelper.GetID(prefix + cleanName)] = name;
            }
        }
    }

    private static AssetBundle thumbnailBundle;
    private static string[] cachedThumbnailAssetNames = new string[0];

    public void LoadThumbnailBundle(string bundleName)
    {
        if (thumbnailBundle != null) 
        {
            // Si el bundle ya existe pero la caché se limpió (por UnloadContent), re-indexamos
            if (cachedThumbnailAssetNames.Length > 0 && assetNameCache.Count == 0)
            {
                IndexNames(cachedThumbnailAssetNames);
            }
            return;
        }

        string path = Path.Combine(Application.streamingAssetsPath, bundleName);
        if (System.IO.File.Exists(path))
        {
            thumbnailBundle = AssetBundle.LoadFromFile(path);
            if (thumbnailBundle != null)
            {
                cachedThumbnailAssetNames = thumbnailBundle.GetAllAssetNames();
                IndexNames(cachedThumbnailAssetNames);
            }
        }
    }

    public void LoadEventosBundle(string bundleName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, bundleName);
        if (System.IO.File.Exists(path))
        {
            if (eventosBundle != null) { eventosBundle.Unload(true); eventosBundle = null; }
            eventosBundle = AssetBundle.LoadFromFile(path);
            if (eventosBundle != null)
            {
                IndexNames(eventosBundle.GetAllAssetNames());
            }
        }
    }

    public void UnloadContent()
    {
        StopAllCoroutines();

        if (currentBundle != null) { currentBundle.Unload(false); currentBundle = null; }
        if (eventosBundle != null) { eventosBundle.Unload(false); eventosBundle = null; }
        
        assetNameCache.Clear();
    }

    public void ExecuteCommand(BackgroundData pData, BackgroundData sData)
    {
        if (pData.actionType == 0) return; // Si no hay acción, ignorar los hashes "fantasma" que arrastra el ScriptConverter

        if (activeSwitch != null) 
        { 
            StopCoroutine(activeSwitch); 
            FinalizeSwitchState(); 
            activeSwitch = null; 
        }
        if (activeFadeIn != null) { StopCoroutine(activeFadeIn); SetAlpha(principal, 1); activeFadeIn = null; }
        if (activeFadeOut != null) { StopCoroutine(activeFadeOut); SetAlpha(principal, 0); principal.gameObject.SetActive(false); currentMainHash = 0; activeFadeOut = null; }

        isAnimating = false;
        isSwitching = false;

        if (pData.actionType != 8 && pData.actionType != 5 && pData.actionType != 11 && pData.actionType != 6 && pData.actionType != 7 && pData.actionType != 0)
        {
            if (activeZoom != null) { StopCoroutine(activeZoom); principal.rectTransform.sizeDelta = targetMainSize; activeZoom = null; }
            if (activeMove != null) { StopCoroutine(activeMove); principal.rectTransform.localPosition = new Vector3(targetMainPos.x, targetMainPos.y, 0f); activeMove = null; }
            if (activeShake != null) { StopCoroutine(activeShake); principal.rectTransform.localPosition = new Vector3(targetMainPos.x, targetMainPos.y, 0f); activeShake = null; }
        }

        if (pData.actionType == 3)
        {
            if (sData.fileHash != 0) currentSubHash = sData.fileHash;
            PrepareImage(secundario, currentSubHash, sData.coords, sData.size);
            secundario.gameObject.SetActive(true);
            SetAlpha(secundario, 1);
            activeSwitch = StartCoroutine(HandleSwitch(pData.time));
        }
        else if (pData.actionType == 1)
        {
            if (pData.fileHash != 0) currentMainHash = pData.fileHash;
            PrepareImage(principal, currentMainHash, pData.coords, pData.size);
            principal.gameObject.SetActive(true);
            activeFadeIn = StartCoroutine(HandleFadeIn(pData.time));
        }
        else if (pData.actionType == 2)
        {
            activeFadeOut = StartCoroutine(HandleFadeOut(pData.time));
        }
        else if (pData.actionType == 4)
        {
            if (pData.fileHash != 0) currentMainHash = pData.fileHash;
            PrepareImage(principal, currentMainHash, pData.coords, pData.size);
            principal.gameObject.SetActive(true);
            SetAlpha(principal, 1);
        }
        else if (pData.actionType == 8)
        {
            if (pData.fileHash != 0) currentMainHash = pData.fileHash;
            if (activeZoom != null) { StopCoroutine(activeZoom); activeZoom = null; }
            PrepareImage(principal, currentMainHash, pData.coords, pData.startSize);
            targetMainSize = pData.size;
            principal.gameObject.SetActive(true);
            SetAlpha(principal, 1);
            activeZoom = StartCoroutine(HandleZoom(principal, pData.startSize, pData.size, pData.time));
        }
        else if (pData.actionType == 5 || pData.actionType == 11)
        {
            if (activeMove != null) { StopCoroutine(activeMove); activeMove = null; }
            if (activeShake != null) { StopCoroutine(activeShake); activeShake = null; }
            Vector2 startPos = new Vector2(principal.rectTransform.localPosition.x, principal.rectTransform.localPosition.y);
            targetMainPos = new Vector2(pData.coords.x, pData.coords.y);
            activeMoveIsForce = (pData.actionType == 11);
            activeMove = StartCoroutine(HandleMove(principal, startPos, targetMainPos, pData.time));
        }
        else if (pData.actionType == 6 || pData.actionType == 7)
        {
            if (activeMove != null) { StopCoroutine(activeMove); principal.rectTransform.localPosition = new Vector3(targetMainPos.x, targetMainPos.y, 0f); activeMove = null; }
            if (activeShake != null) { StopCoroutine(activeShake); principal.rectTransform.localPosition = new Vector3(targetMainPos.x, targetMainPos.y, 0f); activeShake = null; }
            
            activeShake = StartCoroutine(HandleShake(principal, pData.startSize.x, pData.startSize.y, pData.time, pData.actionType == 6, targetMainPos));
        }
    }

    private void PrepareImage(Image img, int fileHash, Vector3 coords, Vector2 size)
    {
        if (fileHash == 0) return;
        string assetName = GetAssetName(fileHash);
        if (!string.IsNullOrEmpty(assetName))
        {
            Sprite s = null;
            if (currentBundle != null) s = currentBundle.LoadAsset<Sprite>(assetName);
            if (s == null && eventosBundle != null) s = eventosBundle.LoadAsset<Sprite>(assetName);

            if (s != null)
            {
                img.sprite = s;
                img.rectTransform.localPosition = new Vector3(coords.x, coords.y, 0);
                img.rectTransform.sizeDelta = size;
                if (img == principal) { targetMainSize = size; targetMainPos = new Vector2(coords.x, coords.y); }
                if (img == secundario) { targetSubSize = size; targetSubPos = new Vector2(coords.x, coords.y); }
            }
        }
    }

    private string GetAssetName(int hash)
    {
        string n; 
        if (assetNameCache.TryGetValue(hash, out n)) return n;
        return null;
    }

    private IEnumerator HandleFadeIn(float duration)
    {
        isAnimating = true;
        SetAlpha(principal, 0);
        float elapsed = 0;
        if (duration <= 0) elapsed = 1;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(principal, Mathf.Lerp(0f, 1f, elapsed / duration));
            yield return null;
        }
        SetAlpha(principal, 1);
        isAnimating = false;
        activeFadeIn = null;
    }

    private IEnumerator HandleFadeOut(float duration)
    {
        isAnimating = true;
        float elapsed = 0;
        float startA = principal.color.a;
        if (duration <= 0) elapsed = 1;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(principal, Mathf.Lerp(startA, 0f, elapsed / duration));
            yield return null;
        }
        SetAlpha(principal, 0);
        principal.gameObject.SetActive(false);
        currentMainHash = 0;
        isAnimating = false;
        activeFadeOut = null;
    }

    private IEnumerator HandleSwitch(float duration)
    {
        isAnimating = true; isSwitching = true;
        float elapsed = 0;
        if (duration <= 0) elapsed = 1;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(principal, Mathf.Lerp(1f, 0f, elapsed / duration));
            yield return null;
        }
        FinalizeSwitchState();
        activeSwitch = null;
    }

    private void FinalizeSwitchState()
    {
        SetAlpha(principal, 0); 
        principal.gameObject.SetActive(false);
        SetAlpha(secundario, 1);
        
        Image oldP = principal;
        principal = secundario;
        secundario = oldP;
        
        int tempHash = currentMainHash;
        currentMainHash = currentSubHash;
        currentSubHash = tempHash;

        Vector2 tempSize = targetMainSize;
        targetMainSize = targetSubSize;
        targetSubSize = tempSize;

        Vector2 tempPos = targetMainPos;
        targetMainPos = targetSubPos;
        targetSubPos = tempPos;

        secundario.transform.SetAsFirstSibling();
        principal.transform.SetAsLastSibling();
        isAnimating = false; isSwitching = false;
    }

    private IEnumerator HandleZoom(Image img, Vector2 start, Vector2 end, float duration)
    {
        float elapsed = 0;
        if (duration <= 0) elapsed = 1;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            img.rectTransform.sizeDelta = Vector2.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        img.rectTransform.sizeDelta = end;
        activeZoom = null;
    }

    private IEnumerator HandleMove(Image img, Vector2 start, Vector2 end, float duration)
    {
        float elapsed = 0;
        if (duration <= 0) elapsed = 1;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            img.rectTransform.localPosition = Vector3.Lerp(new Vector3(start.x, start.y, 0), new Vector3(end.x, end.y, 0), elapsed / duration);
            yield return null;
        }
        img.rectTransform.localPosition = new Vector3(end.x, end.y, 0);
        activeMove = null;
    }

    private IEnumerator HandleShake(Image img, float amplitude, float bounces, float duration, bool isX, Vector2 basePos)
    {
        isAnimating = true;
        
        yield return null;
        yield return null;
        
        float elapsed = 0;
        if (duration <= 0) elapsed = duration + 1;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = amplitude * Mathf.Sin((elapsed / duration) * bounces * Mathf.PI * 2f);
            if (isX) 
                img.rectTransform.localPosition = new Vector3(basePos.x + offset, basePos.y, 0);
            else 
                img.rectTransform.localPosition = new Vector3(basePos.x, basePos.y + offset, 0);
            yield return null;
        }
        img.rectTransform.localPosition = new Vector3(basePos.x, basePos.y, 0);
        isAnimating = false;
        activeShake = null;
    }

    public void InstantFinish()
    {
        if (activeMove != null) { 
            if (activeMoveIsForce) {
                principal.rectTransform.localPosition = new Vector3(targetMainPos.x, targetMainPos.y, 0f); 
                StopCoroutine(activeMove); 
                activeMove = null; 
            }
        }

        if (!isAnimating) return;
        
        if (activeFadeIn != null) { StopCoroutine(activeFadeIn); SetAlpha(principal, 1); activeFadeIn = null; }
        if (activeFadeOut != null) { StopCoroutine(activeFadeOut); SetAlpha(principal, 0); principal.gameObject.SetActive(false); currentMainHash = 0; activeFadeOut = null; }
        if (activeSwitch != null) { StopCoroutine(activeSwitch); activeSwitch = null; }
        if (activeZoom != null) { StopCoroutine(activeZoom); principal.rectTransform.sizeDelta = targetMainSize; activeZoom = null; }
        if (activeShake != null) { StopCoroutine(activeShake); principal.rectTransform.localPosition = new Vector3(targetMainPos.x, targetMainPos.y, 0f); activeShake = null; }

        if (isSwitching) 
        {
            FinalizeSwitchState();
        }
        else
        {
            if (principal.color.a > 0.01f) { SetAlpha(principal, 1); principal.gameObject.SetActive(true); }
            else { SetAlpha(principal, 0); principal.gameObject.SetActive(false); currentMainHash = 0; }
            isAnimating = false;
        }
    }

    public int GetMainHash() { return currentMainHash; }
    public int GetSubHash() { return currentSubHash; }
    
    public int GetActiveVisibleHash()
    {
        if (principal != null && principal.color.a >= secundario.color.a && principal.gameObject.activeSelf)
        {
            return currentMainHash;
        }
        else if (secundario != null && secundario.gameObject.activeSelf)
        {
            return currentSubHash;
        }
        return currentMainHash;
    }

    public float GetMainAlpha() { return principal != null ? principal.color.a : 0; }
    public float GetSubAlpha() { return secundario != null ? secundario.color.a : 0; }
    public Vector2 GetMainSize() { return targetMainSize; }
    public Vector2 GetSubSize() { return targetSubSize; }
    public Vector2 GetMainPos() { return targetMainPos; }
    public Vector2 GetSubPos() { return targetSubPos; }
    public bool GetMainActive() { return principal.gameObject.activeSelf; }
    public bool GetSubActive() { return secundario.gameObject.activeSelf; }
    public bool GetIsSwapped() { return principal == fondoB; }

    public Sprite GetSpriteByHash(int hash)
    {
        if (hash == 0) return null;
        string assetName = GetAssetName(hash);
        if (!string.IsNullOrEmpty(assetName)) 
        {
            Sprite s = null;
            if (currentBundle != null) s = currentBundle.LoadAsset<Sprite>(assetName);
            if (s == null && eventosBundle != null) s = eventosBundle.LoadAsset<Sprite>(assetName);
            return s;
        }
        return null;
    }

    private static Dictionary<int, string> thumbnailNameCache = new Dictionary<int, string>();

    public Sprite GetThumbnailSpriteByHash(int hash)
    {
        if (hash == 0 || thumbnailBundle == null) return null;

        string assetName;
        if (thumbnailNameCache.TryGetValue(hash, out assetName))
        {
            return thumbnailBundle.LoadAsset<Sprite>(assetName);
        }

        foreach (string name in cachedThumbnailAssetNames)
        {
            string cleanName = name.ToLower().Replace(".jpg", "").Replace(".png", "").Replace(".jpeg", "");
            string baseName = System.IO.Path.GetFileName(cleanName);

            if (HashHelper.GetID("bgs/" + baseName) == hash || 
                HashHelper.GetID("bgs/event/" + baseName) == hash ||
                HashHelper.GetID("bgs/events/" + baseName) == hash ||
                HashHelper.GetID("bg/" + baseName) == hash ||
                HashHelper.GetID("event/" + baseName) == hash ||
                HashHelper.GetID("events/" + baseName) == hash)
            {
                thumbnailNameCache[hash] = name;
                return thumbnailBundle.LoadAsset<Sprite>(name);
            }
        }
        return null;
    }

    public void RestoreInstant(int mH, int sH, float mA, float sA, bool mAct, bool sAct, bool swapped, Vector2 mSz, Vector2 sSz, Vector2 mPos, Vector2 sPos)
    {
        if (mSz == Vector2.zero) mSz = new Vector2(400, 240);
        if (sSz == Vector2.zero) sSz = new Vector2(400, 240);

        if (activeFadeIn != null) { StopCoroutine(activeFadeIn); activeFadeIn = null; }
        if (activeFadeOut != null) { StopCoroutine(activeFadeOut); activeFadeOut = null; }
        if (activeSwitch != null) { StopCoroutine(activeSwitch); activeSwitch = null; }
        if (activeZoom != null) { StopCoroutine(activeZoom); activeZoom = null; }
        if (activeMove != null) { StopCoroutine(activeMove); activeMove = null; }
        if (activeShake != null) { StopCoroutine(activeShake); activeShake = null; }

        principal = swapped ? fondoB : fondoA;
        secundario = swapped ? fondoA : fondoB;
        currentMainHash = mH; currentSubHash = sH;
        PrepareImage(principal, mH, new Vector3(mPos.x, mPos.y, 0f), mSz);
        PrepareImage(secundario, sH, new Vector3(sPos.x, sPos.y, 0f), sSz);
        SetAlpha(principal, mA); SetAlpha(secundario, sA);
        principal.gameObject.SetActive(mAct); secundario.gameObject.SetActive(sAct);
        secundario.transform.SetAsFirstSibling();
        principal.transform.SetAsLastSibling();
        isAnimating = false;
        isSwitching = false;
    }

    public void UnloadThumbnails()
    {
        if (thumbnailBundle != null) { thumbnailBundle.Unload(true); thumbnailBundle = null; }
        thumbnailNameCache.Clear();
        cachedThumbnailAssetNames = new string[0];
    }

    public void ClearReferences()
    {
        if (fondoA != null) fondoA.sprite = null;
        if (fondoB != null) fondoB.sprite = null;
    }

    void OnDestroy()
    {
        UnloadContent();
    }
}