using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicPlayer : MonoBehaviour
{
    [Header("Configuración de Música")]
    public AudioClip backgroundMusicClip;
    [Tooltip("Duración del fundido de entrada en segundos.")]
    public float fadeInDuration = 3.0f;
    [Tooltip("Volumen máximo al que llegará la música (0.0 a 1.0).")]
    [Range(0f, 1f)]
    public float maxVolume = 1f;

    [Header("Fundido de Salida")]
    [Tooltip("Duración del fundido de salida en segundos.")]
    public float fadeOutDuration = 2.0f;

    private AudioSource audioSource;
    private Coroutine activeFadeCoroutine = null;

    void Awake()
    {
        // Al despertar, dejamos el audio listo para la acción.
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;
    }

    // Aquí empieza la fiesta: ponemos la rola con fade in.
    void Start()
    {
        if (backgroundMusicClip == null) {
            Debug.LogError("BGM Error: No se asignó AudioClip.", gameObject);
            this.enabled = false; return;
        }
        if (audioSource == null) {
            Debug.LogError("BGM Error: No se encontró AudioSource.", gameObject);
             this.enabled = false; return;
        }

        audioSource.clip = backgroundMusicClip;
        audioSource.Play();
        StartFadeIn();
    }

    // Para que la música entre con un fade in bien sabrozo.
    public void StartFadeIn() {
        if (audioSource == null) return;
        if (activeFadeCoroutine != null) StopCoroutine(activeFadeCoroutine);
        activeFadeCoroutine = StartCoroutine(FadeAudioCoroutine(0f, maxVolume, fadeInDuration));
    }

    // Y con esta, la música se va apagando lentamente.
    public void StartFadeOut() {
        if (audioSource == null) return;
        if (activeFadeCoroutine != null) StopCoroutine(activeFadeCoroutine);
        activeFadeCoroutine = StartCoroutine(FadeAudioCoroutine(audioSource.volume, 0f, fadeOutDuration));
    }

    // El cerebro del fade. Hace que el volumen cambie suavemente... xd.
    IEnumerator FadeAudioCoroutine(float startVolume, float endVolume, float duration) {
        if (duration <= 0f) {
            audioSource.volume = endVolume;
            activeFadeCoroutine = null;
            if (endVolume <= 0f) audioSource.Stop();
            yield break;
        }

        float elapsedTime = 0f;
        while (elapsedTime < duration) {
            elapsedTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, endVolume, elapsedTime / duration);
            yield return null;
        }

        audioSource.volume = endVolume;
        activeFadeCoroutine = null;

        if (endVolume <= 0f) {
            audioSource.Stop();
            Debug.Log("BGM detenida después de Fade Out.");
        }
    }
}