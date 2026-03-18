// Descomenta la siguiente línea para leer audios directo del proyecto en lugar del AssetBundle (Solo para Unity Editor)
#define TEST_AUDIO_EN_EDITOR

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[RequireComponent(typeof(AudioSource))]
public class SFXManager : MonoBehaviour
{
    private AudioSource source;
    private AssetBundle sfxBundle;
    private Dictionary<int, string> assetCache = new Dictionary<int, string>();
    private int currentSFXHash = 0;
    private Coroutine fadeRoutine;

    public bool isPlaying { get { return source.isPlaying; } }

    void Awake() {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
    }

    public void LoadBundle(string bundleName) {
        UnloadContent();
        assetCache.Clear();
        string path = Path.Combine(Application.streamingAssetsPath, bundleName);
        if (System.IO.File.Exists(path))
        {
            sfxBundle = AssetBundle.LoadFromFile(path);
            if (sfxBundle != null)
            {
                string[] names = sfxBundle.GetAllAssetNames();
                foreach (string name in names)
                {
                    string clean = name.ToLower().Replace(".ogg", "").Replace(".wav", "").Replace(".mp3", "");
                    if (clean.StartsWith("assets/sfx/")) clean = clean.Substring(11);
                    assetCache[HashHelper.GetID(clean)] = name;
                }
            }
        }
    }

    public void UnloadContent()
    {
        StopAll(); // CRITICAL: Stop DSP playback before destroying the memory pointer
        if (sfxBundle != null) { sfxBundle.Unload(true); sfxBundle = null; }
    }

    public void ExecuteSFX(int hash, int action, float volume, float fadeTime)
    {
        if (action == 0) return;

        if (action == 3) 
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(SFXFadeOut(fadeTime));
            return;
        }

        if (hash == 0) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        StartCoroutine(SwitchClipRoutine(hash, action, volume));
    }

    private IEnumerator SwitchClipRoutine(int hash, int action, float volume)
    {
        if (hash != currentSFXHash)
        {
            source.Stop();
            source.clip = null;
            yield return null; // 1-frame delay to flush 3DS Native DSP

#if UNITY_EDITOR && TEST_AUDIO_EN_EDITOR
            AudioClip clip = GetDirectClip(hash, "sfx");
#else
            AudioClip clip = null;
            if (sfxBundle != null)
            {
                string assetName = GetAssetName(hash);
                if (!string.IsNullOrEmpty(assetName))
                {
                    clip = sfxBundle.LoadAsset<AudioClip>(assetName);
                }
            }
#endif
            if (clip != null) { source.clip = clip; currentSFXHash = hash; }
        }

        source.volume = volume;
        source.loop = (action == 2);
        source.Play();
    }

    private string GetAssetName(int hash)
    {
        string n; 
        if (assetCache.TryGetValue(hash, out n)) return n;
        return null;
    }

    private IEnumerator SFXFadeOut(float dur)
    {
        float startVol = source.volume;
        float elapsed = 0;
        if (dur <= 0) {
            source.volume = 0;
        } else {
            while (elapsed < dur) {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVol, 0, elapsed / dur);
                yield return null;
            }
            source.volume = 0;
        }
        source.Stop(); source.clip = null; currentSFXHash = 0; fadeRoutine = null;
    }

    public void FadeLoopVolume(float targetVolume, float duration)
    {
        if (source.isPlaying && source.loop)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(LoopVolumeFade(targetVolume, duration));
        }
    }

    private IEnumerator LoopVolumeFade(float target, float duration)
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
    }

    public void GetLoopState(out int hash, out float vol)
    {
        if (source.loop && source.isPlaying) { hash = currentSFXHash; vol = source.volume; }
        else { hash = 0; vol = 0; }
    }

    public void RestoreLoop(int hash, float vol)
    {
        if (hash == 0) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        StartCoroutine(SwitchClipRoutine(hash, 2, vol));
    }

    public void StopAll()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        source.Stop(); source.clip = null; source.loop = false; source.volume = 0; currentSFXHash = 0;
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