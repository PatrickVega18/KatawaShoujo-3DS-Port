// Descomenta la siguiente línea para leer audios directo del proyecto en lugar del AssetBundle (Solo para Unity Editor)
#define TEST_AUDIO_EN_EDITOR

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    private AudioSource source;
    private AssetBundle musicBundle;
    private Dictionary<int, string> assetCache = new Dictionary<int, string>();
    private int currentTrackHash = 0;
    private Coroutine fadeRoutine;
    
    private int lastAction = -1;
    private float lastTargetVol = -1f;
    private int lastFileHash = -1;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0;
    }

    public void LoadBundle(string bundleName)
    {
        UnloadContent();
        assetCache.Clear();
        string path = Path.Combine(Application.streamingAssetsPath, bundleName);
        if (System.IO.File.Exists(path))
        {
            musicBundle = AssetBundle.LoadFromFile(path);
        }
    }

    public void UnloadContent()
    {
        StopImmediate(); // CRITICAL: Stop DSP playback before destroying the memory pointer
        if (musicBundle != null) { musicBundle.Unload(true); musicBundle = null; }
    }

    public void ExecuteCommand(MusicData data)
    {
        if (data.fileHash == 0 && data.actionType == 0) return;

        if (data.fileHash == lastFileHash && data.actionType == lastAction && Mathf.Abs(data.targetVolume - lastTargetVol) < 0.01f)
        {
            return;
        }

        lastFileHash = data.fileHash;
        lastAction = data.actionType;
        lastTargetVol = data.targetVolume;

        if (data.fileHash != currentTrackHash && data.fileHash != 0)
        {
            LoadAndPlay(data.fileHash);
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (data.actionType == 1 || data.actionType == 3)
        {
            fadeRoutine = StartCoroutine(VolumeFade(data.targetVolume, data.time));
        }
        else if (data.actionType == 2)
        {
            fadeRoutine = StartCoroutine(VolumeFade(0, data.time));
        }
    }

    private void LoadAndPlay(int hash)
    {
#if UNITY_EDITOR && TEST_AUDIO_EN_EDITOR
        AudioClip clip = GetDirectClip(hash, "bgm");
#else
        AudioClip clip = null;
        if (musicBundle != null)
        {
            string assetName = GetAssetName(hash);
            if (!string.IsNullOrEmpty(assetName))
            {
                clip = musicBundle.LoadAsset<AudioClip>(assetName);
            }
        }
#endif
        if (clip != null)
        {
            source.clip = clip;
            source.Play();
            currentTrackHash = hash;
        }
    }

    private string GetAssetName(int hash)
    {
        string n; 
        if (assetCache.TryGetValue(hash, out n)) return n;
        
        string[] allNames = musicBundle.GetAllAssetNames();
        for (int i = 0; i < allNames.Length; i++)
        {
            string clean = allNames[i].ToLower().Replace(".ogg", "").Replace(".wav", "").Replace(".mp3", "");
            if (clean.StartsWith("assets/bgm/")) clean = clean.Substring(11);
            
            if (HashHelper.GetID(clean) == hash) 
            { 
                assetCache[hash] = allNames[i]; 
                return allNames[i]; 
            }
        }
        return null;
    }

    private IEnumerator VolumeFade(float target, float duration)
    {
        float startVol = source.volume;
        float elapsed = 0;

        if (duration <= 0) {
            source.volume = target;
        } else {
            while (elapsed < duration) {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVol, target, elapsed / duration);
                yield return null;
            }
            source.volume = target;
        }

        if (source.volume <= 0.001f) {
            source.Stop(); source.clip = null; currentTrackHash = 0;
            lastFileHash = -1; lastAction = -1; lastTargetVol = -1;
        }
    }

    public int GetCurrentHash() { return currentTrackHash; }
    public float GetCurrentVolume() { return source.volume; }

    public string GetCurrentTrackName()
    {
        if (currentTrackHash == 0 || source.volume <= 0.01f) return "";
        string assetName = GetAssetName(currentTrackHash);
        if (string.IsNullOrEmpty(assetName)) return "";

        string cleanName = Path.GetFileNameWithoutExtension(assetName).Replace("_", " ");
        
        // Convertir a Title Case (Iniciales en mayúsculas) manualmente para evitar dependencias pesadas en 3DS
        string[] words = cleanName.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }

    public void RestoreInstant(int hash, float volume)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (hash == 0) { source.Stop(); source.clip = null; currentTrackHash = 0; return; }
        LoadAndPlay(hash);
        source.volume = volume;
        lastFileHash = hash; lastAction = 1; lastTargetVol = volume;
    }

    public void StopImmediate()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        source.Stop(); source.clip = null; source.volume = 0;
        currentTrackHash = 0; lastFileHash = -1; lastAction = -1; lastTargetVol = -1;
    }

    public void ClearReferences()
    {
        source.clip = null;
    }

#if UNITY_EDITOR && TEST_AUDIO_EN_EDITOR
    private AudioClip GetDirectClip(int hash, string type)
    {
        string folder = type == "bgm" ? "Assets/bgm" : "Assets/sfx";
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
        foreach(string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            string clean = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            if (HashHelper.GetID(clean) == hash)
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
        return null;
    }
#endif

    void OnDestroy()
    {
        UnloadContent();
    }
}