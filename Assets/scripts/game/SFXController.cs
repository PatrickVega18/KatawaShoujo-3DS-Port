using UnityEngine;
using System.Collections;


[RequireComponent(typeof(AudioSource))]
public class SFXController : MonoBehaviour
{
    private AudioSource sfxAudioSource;

    private Coroutine sfxDurationCoroutine = null;
    private AudioClip currentSFXClip = null;
    public bool IsPlayingAndWaiting { get; private set; }

    private Coroutine sfxFadeCoroutine = null;
    private string currentLoopSfxPath = null;


    // ===== INICIALIZACIÓN =====
    void Awake()
    {
        sfxAudioSource = GetComponent<AudioSource>();
        if (sfxAudioSource == null)
        {
            Debug.LogError("SFXController: No se encontró el componente AudioSource.", gameObject);
            this.enabled = false;
            return;
        }

        sfxAudioSource.playOnAwake = false; // Lo controlaremos todo por script
        IsPlayingAndWaiting = false;
    }

    // ===== REPRODUCE UN SFX NORMAL (UNA SOLA VEZ) =====
    public void PlaySFX(string sfxPathInResources, float volume, bool waitForCompletion = false)
    {
        if (sfxAudioSource == null) return;
        if (string.IsNullOrEmpty(sfxPathInResources)) return;
        ForceStopAndClean();

        StopCurrentSFX(); // Detiene cualquier SFX normal anterior

        string pathWithoutExtension = sfxPathInResources;
        int dotIndex = sfxPathInResources.LastIndexOf('.');
        if (dotIndex > 0 && dotIndex > sfxPathInResources.LastIndexOf('/'))
        {
            pathWithoutExtension = sfxPathInResources.Substring(0, dotIndex);
        }
        string resourcePath = "sfx/" + pathWithoutExtension;

        currentSFXClip = Resources.Load<AudioClip>(resourcePath);

        if (currentSFXClip != null)
        {
            // Aseguramos que el loop esté DESACTIVADO para SFX normales
            sfxAudioSource.loop = false;
            sfxAudioSource.volume = 1.0f;
            sfxAudioSource.PlayOneShot(currentSFXClip, Mathf.Clamp01(volume));

            if (waitForCompletion)
            {
                IsPlayingAndWaiting = true;
                sfxDurationCoroutine = StartCoroutine(WaitForSFXToEnd(currentSFXClip.length));
            }
        }
        else
        {
            Debug.LogError("SFXController: No se pudo cargar el AudioClip desde: " + resourcePath);
            currentSFXClip = null;
            IsPlayingAndWaiting = false;
        }
    }

    // ===== INICIA UN SFX EN BUCLE CON FADE-IN =====
    public void PlayLoopingSFX(string sfxPath, float volume, float fadeDuration)
    {
        ForceStopAndClean();
        if (sfxFadeCoroutine != null) StopCoroutine(sfxFadeCoroutine);

        // Detenemos cualquier sonido (normal o en bucle) que estuviera sonando antes.
        sfxAudioSource.Stop();

        string pathWithoutExtension = sfxPath;
        int dotIndex = sfxPath.LastIndexOf('.');
        if (dotIndex > 0 && dotIndex > sfxPath.LastIndexOf('/'))
        {
            pathWithoutExtension = sfxPath.Substring(0, dotIndex);
        }
        string resourcePath = "sfx/" + pathWithoutExtension;
        AudioClip clipToPlay = Resources.Load<AudioClip>(resourcePath);

        if (clipToPlay == null)
        {
            Debug.LogError("SFXController (Loop): No se pudo cargar el AudioClip desde: " + resourcePath);
            return;
        }

        sfxAudioSource.clip = clipToPlay;
        currentLoopSfxPath = sfxPath;
        sfxAudioSource.volume = 0f;
        sfxAudioSource.loop = true;
        sfxAudioSource.Play();

        sfxFadeCoroutine = StartCoroutine(FadeSFX(Mathf.Clamp01(volume), fadeDuration));
    }

    // ===== DETIENE EL SFX EN BUCLE CON FADE-OUT =====
    public void StopLoopingSFX(float fadeDuration)
    {
        if (sfxFadeCoroutine != null) StopCoroutine(sfxFadeCoroutine);

        // Solo hacemos fade out si hay un clip en bucle sonando.
        if (sfxAudioSource.isPlaying && sfxAudioSource.loop)
        {
            sfxFadeCoroutine = StartCoroutine(FadeSFX(0f, fadeDuration));
        }
    }

    // ===== CORRUTINA GENÉRICA PARA FADES DE VOLUMEN =====
    private IEnumerator FadeSFX(float targetVolume, float duration)
    {
        float startVolume = sfxAudioSource.volume;
        float elapsedTime = 0f;

        if (duration <= 0)
        {
            sfxAudioSource.volume = targetVolume;
        }
        else
        {
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                sfxAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsedTime / duration);
                yield return null;
            }
            sfxAudioSource.volume = targetVolume;
        }

        if (targetVolume == 0f)
        {
            sfxAudioSource.Stop();
            sfxAudioSource.clip = null;
            sfxAudioSource.loop = false;
            currentLoopSfxPath = null;
        }

        sfxFadeCoroutine = null;
    }

    // ===== LÓGICA PARA SFX NORMALES (SIN CAMBIOS) =====
    private IEnumerator WaitForSFXToEnd(float duration)
    {
        yield return new WaitForSeconds(duration);
        IsPlayingAndWaiting = false;
        sfxDurationCoroutine = null;
        currentSFXClip = null;
    }
    public void AdjustLoopVolume(float targetVolume, float fadeDuration)
    {
        // Si hay un fade en curso, lo detenemos para empezar el nuevo.
        if (sfxFadeCoroutine != null)
        {
            StopCoroutine(sfxFadeCoroutine);
        }

        // Solo ajustamos el volumen si hay un clip en bucle sonando.
        if (sfxAudioSource.isPlaying && sfxAudioSource.loop)
        {
            Debug.Log("SFXController: Ajustando volumen del loop a " + targetVolume + " en " + fadeDuration + "s.");
            sfxFadeCoroutine = StartCoroutine(FadeSFX(Mathf.Clamp01(targetVolume), fadeDuration));
        }
    }
    public void StopCurrentSFX()
    {
        if (sfxDurationCoroutine != null)
        {
            StopCoroutine(sfxDurationCoroutine);
            sfxDurationCoroutine = null;
        }
        currentSFXClip = null;
        IsPlayingAndWaiting = false;
    }
    private void ForceStopAndClean()
    {
        if (sfxFadeCoroutine != null)
        {
            StopCoroutine(sfxFadeCoroutine);
            sfxFadeCoroutine = null;
        }

        sfxAudioSource.Stop();

        sfxAudioSource.clip = null;
        sfxAudioSource.loop = false;
        currentLoopSfxPath = null;
        
        Debug.Log("SFXController: Limpieza forzada ejecutada.");
    }
    public SFXSaveData GetCurrentState()
    {
        SFXSaveData data = new SFXSaveData();

        // Comprobamos si el AudioSource está en modo loop y sonando
        if (sfxAudioSource.isPlaying && sfxAudioSource.loop && currentLoopSfxPath != null)
        {
            data.isLooping = true;
            data.loopPath = currentLoopSfxPath;
            data.loopVolume = sfxAudioSource.volume;
        }
        else
        {
            data.isLooping = false;
        }
        return data;
    }

    public void RestoreState(SFXSaveData data)
    {
        // Detenemos cualquier fade o sonido en curso
        if (sfxFadeCoroutine != null) StopCoroutine(sfxFadeCoroutine);
        sfxAudioSource.Stop();
        sfxAudioSource.loop = false;
        currentLoopSfxPath = null;

        if (data.isLooping && !string.IsNullOrEmpty(data.loopPath))
        {
            // Si había un loop guardado, lo restauramos
            string pathWithoutExtension = data.loopPath;
            int dotIndex = data.loopPath.LastIndexOf('.');
            if (dotIndex > 0 && dotIndex > data.loopPath.LastIndexOf('/'))
            {
                pathWithoutExtension = data.loopPath.Substring(0, dotIndex);
            }
            string resourcePath = "sfx/" + pathWithoutExtension;
            AudioClip clipToPlay = Resources.Load<AudioClip>(resourcePath);

            if (clipToPlay != null)
            {
                sfxAudioSource.clip = clipToPlay;
                sfxAudioSource.volume = data.loopVolume;
                sfxAudioSource.loop = true;
                sfxAudioSource.Play();
                currentLoopSfxPath = data.loopPath;
            }
        }
    }
}