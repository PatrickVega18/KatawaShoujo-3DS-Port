using UnityEngine;
using System.Collections;
using System.Globalization;

[RequireComponent(typeof(AudioSource))]
public class MusicController : MonoBehaviour
{
    [Header("Referencias a Otros Controladores")]
    public SFXController sfxController;

    private AudioSource audioSource;
    private Coroutine activeFadeCoroutine = null;
    private string currentMusicPath = null;


    [Tooltip("Volumen máximo al que llegará la música después del fade-in.")]
    [Range(0f, 1f)]
    public float maxVolume = 1f;

    // ===== SE EJECUTA AL INICIAR PARA OBTENER EL COMPONENTE AUDIOSOURCE =====
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("MusicController: No se encontró el componente AudioSource.", gameObject);
            this.enabled = false;
            return;
        }
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        if (sfxController == null)
        {
            Debug.LogWarning("MusicController: SFXController no está asignado. Los comandos 'up' y 'down' no afectarán a los SFX en bucle.");
        }
    }

    //==== PROCESA UN COMANDO DE MÚSICA DESDE EL GAMECONTROLLER ====
    public void ProcessMusicCommand(string musicPath, string action, float fadeDuration)
    {
        if (string.IsNullOrEmpty(action))
        {
            Debug.LogWarning("MusicController: La acción de música está vacía o nula.");
            return;
        }

        // Dividimos la acción para separar el comando del valor (ej: "in" y "0.8")
        string[] actionParts = action.ToLowerInvariant().Split('-');
        string command = actionParts[0];

        float targetVolume = -1f; // Usamos -1 como señal de que no se ha especificado un volumen
        if (actionParts.Length > 1)
        {
            // Intentamos parsear el valor numérico después del guion
            if (!float.TryParse(actionParts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out targetVolume))
            {
                Debug.LogWarning("MusicController: No se pudo parsear el valor de volumen en la acción: " + action);
                targetVolume = -1f; // Reseteamos si el parseo falla
            }
        }

        switch (command)
        {
            case "in":
                if (string.IsNullOrEmpty(musicPath))
                {
                    Debug.LogWarning("MusicController: Acción 'in' pero musicPath está vacío o nulo.");
                    return;
                }
                // Si no se especificó un volumen (targetVolume sigue en -1), usamos maxVolume. Si no, usamos el especificado.
                float finalTargetVolumeIn = (targetVolume == -1f) ? maxVolume : Mathf.Clamp01(targetVolume);
                Debug.Log("MusicController: Recibido comando IN - Pista: " + musicPath + ", Volumen Final: " + finalTargetVolumeIn);
                PlayMusicWithFadeIn(musicPath, fadeDuration, finalTargetVolumeIn);
                break;

            case "out":
                Debug.Log("MusicController: Recibido comando OUT - TiempoFade: " + fadeDuration);
                StopMusicWithFadeOut(fadeDuration);
                break;

            // ===== Lógica para "up" y "down" =====
            case "up":
            case "down":
                if (!audioSource.isPlaying)
                {
                    Debug.LogWarning("MusicController: Se intentó usar 'up'/'down' sin música en reproducción.");
                }
                if (targetVolume == -1f)
                {
                    Debug.LogWarning("MusicController: La acción 'up'/'down' requiere un valor de volumen (ej: up-1).");
                    return;
                }

                float clampedTargetVolume = Mathf.Clamp01(targetVolume);
                Debug.Log("MusicController: Ajustando volumen a " + clampedTargetVolume + " en " + fadeDuration + "s.");
                if (activeFadeCoroutine != null) StopCoroutine(activeFadeCoroutine);
                activeFadeCoroutine = StartCoroutine(FadeVolume(clampedTargetVolume, fadeDuration, false, null));
                if (sfxController != null)
                {
                    sfxController.AdjustLoopVolume(clampedTargetVolume, fadeDuration);
                }
                break;

            default:
                Debug.LogWarning("MusicController: Acción de música desconocida: '" + action + "'");
                break;
        }
    }

    // ===== REPRODUCE UNA PISTA DE MÚSICA CON FADE-IN (AHORA PODRÍA SER PRIVADO O INTERNO) =====
    private void PlayMusicWithFadeIn(string musicPath, float fadeDuration, float targetVolume)
    {
        // ===== Guardamos el path de la música que vamos a reproducir =====
        currentMusicPath = musicPath;

        AudioClip clipToPlay = Resources.Load<AudioClip>(musicPath);

        if (clipToPlay == null)
        {
            Debug.LogError("MusicController: No se pudo cargar el AudioClip desde Resources en la ruta: " + musicPath);
            return;
        }

        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }

        // Si ya está sonando la misma pista, solo ajustamos el volumen
        if (audioSource.isPlaying && audioSource.clip == clipToPlay)
        {
            activeFadeCoroutine = StartCoroutine(FadeVolume(targetVolume, fadeDuration, true, clipToPlay));
            return;
        }

        // Si es una nueva pista, la asignamos y empezamos el fade desde 0
        audioSource.clip = clipToPlay;
        activeFadeCoroutine = StartCoroutine(FadeVolume(targetVolume, fadeDuration, true, clipToPlay));
    }

    // ===== DETIENE LA MÚSICA ACTUAL CON FADE-OUT (AHORA PODRÍA SER PRIVADO O INTERNO) =====
    private void StopMusicWithFadeOut(float fadeDuration)
    {
        if (!audioSource.isPlaying && audioSource.volume == 0)
        {
            return;
        }

        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }
        // El fade out siempre va a volumen 0
        activeFadeCoroutine = StartCoroutine(FadeVolume(0f, fadeDuration, false, null));
    }

    // ===== REALIZA EL FADE DE VOLUMEN DEL AUDIOSOURCE =====
    private IEnumerator FadeVolume(float targetVolume, float duration, bool isNewTrack, AudioClip clipToAssign)
    {
        float startVolume = audioSource.volume;
        float currentTime = 0f;

        // Si es una pista nueva, siempre empezamos desde 0
        if (isNewTrack && (audioSource.clip != clipToAssign || !audioSource.isPlaying))
        {
            if (clipToAssign != null) audioSource.clip = clipToAssign;
            else { yield break; }

            audioSource.volume = 0f;
            startVolume = 0f;
            audioSource.Play();
        }

        if (duration <= 0)
        {
            audioSource.volume = targetVolume;
            if (targetVolume == 0f)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
            activeFadeCoroutine = null;
            yield break;
        }

        while (currentTime < duration)
        {
            currentTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, currentTime / duration);
            yield return null;
        }

        audioSource.volume = targetVolume;

        if (targetVolume == 0f && !isNewTrack)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
        activeFadeCoroutine = null;
    }
    
    // ===== MÉTODOS PARA EL SISTEMA DE GUARDADO Y CARGA =====
    public MusicSaveData GetCurrentState()
    {
        MusicSaveData data = new MusicSaveData();
        data.isPlaying = audioSource.isPlaying;
        if (data.isPlaying)
        {
            data.path = currentMusicPath;
            data.volume = audioSource.volume;
        }
        return data;
    }

    public void RestoreState(MusicSaveData data)
    {
        if (activeFadeCoroutine != null) StopCoroutine(activeFadeCoroutine);

        if (data.isPlaying && !string.IsNullOrEmpty(data.path))
        {
            AudioClip clipToPlay = Resources.Load<AudioClip>(data.path);
            if (clipToPlay != null)
            {
                audioSource.clip = clipToPlay;
                audioSource.volume = data.volume;
                audioSource.Play();
                currentMusicPath = data.path; // Restauramos el path
            }
        }
        else
        {
            audioSource.Stop();
            audioSource.clip = null;
            currentMusicPath = null;
        }
    }
}