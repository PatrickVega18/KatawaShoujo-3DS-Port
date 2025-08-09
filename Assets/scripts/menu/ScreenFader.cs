using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    [Header("Imágenes para Fundido")]
    [Tooltip("Arrastra la Imagen UI 'CuadroNegro' de UpperCanvas aquí.")]
    public Image fadeImageUpper;
    [Tooltip("Arrastra la Imagen UI 'CuadroNegro' de LowerCanvas aquí.")]
    public Image fadeImageLower;

    [Header("Configuración")]
    [Tooltip("Duración del fundido de salida en segundos.")]
    public float fadeOutDuration = 1.5f;

    [Tooltip("¿Desactivar las imágenes al terminar el fundido? (Recomendado)")]
    public bool disableAfterFade = true;

    private bool fadeStarted = false;

    // Al empezar, se pone todo negro y empieza el SHOW.
    void Start()
    {
        fadeImageUpper.gameObject.SetActive(true);
        fadeImageLower.gameObject.SetActive(true);
        InitializeImages();
        StartFadeOut();
    }

    // Pone los cuadros negros listos para el fade.
    void InitializeImages()
    {
        Color opaqueBlack = Color.black;

        if (fadeImageUpper != null)
        {
            fadeImageUpper.color = opaqueBlack;
        }
        if (fadeImageLower != null)
        {
            fadeImageLower.color = opaqueBlack;
        }
    }

    // Inicia el proceso de fundido de salida (si no ha iniciado ya)
    public void StartFadeOut()
    {
        if (!fadeStarted)
        {
            fadeStarted = true;
            StartCoroutine(FadeOutCoroutine());
        }
    }

    // Corutina que realiza la animación de fundido
    IEnumerator FadeOutCoroutine()
    {
        if (fadeOutDuration <= 0f)
        {
            SetAlpha(0f);
            FinishFade();
            yield break;
        }

        float timer = 0f;
        Color currentColor = Color.black; // Empezamos en negro opaco

        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(1f, 0f, timer / fadeOutDuration);
            currentColor.a = currentAlpha;
            SetColorOnImages(currentColor);
            yield return null;
        }

        SetAlpha(0f);
        FinishFade();
    }

    void SetColorOnImages(Color colorToSet)
    {
        if (fadeImageUpper != null) fadeImageUpper.color = colorToSet;
        if (fadeImageLower != null) fadeImageLower.color = colorToSet;
    }

    void SetAlpha(float alpha)
    {
        Color tempColor = Color.black;
        tempColor.a = alpha;
        SetColorOnImages(tempColor);
    }

    void FinishFade()
    {
        if (disableAfterFade)
        {
            if (fadeImageUpper != null) fadeImageUpper.gameObject.SetActive(false);
            if (fadeImageLower != null) fadeImageLower.gameObject.SetActive(false);
        }
    }
    //Ey, que haces revisando los scripts? XD
}