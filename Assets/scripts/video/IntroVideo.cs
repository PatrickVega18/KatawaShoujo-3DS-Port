using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroVideo : MonoBehaviour
{
    void Start()
    {
        // Al empezar, ponemos el vídeo y activamos el chivato para saber cuándo acaba.
        string videoPath = Application.streamingAssetsPath + "/4Ls.moflex";
        UnityEngine.N3DS.Video.Play(videoPath, N3dsScreen.Top);
        StartCoroutine(CheckIfVideoFinished());
    }

    void Update()
    {
        // Lo saltamos alv
        if (Input.GetKeyDown(KeyCode.Return))
        {
            UnityEngine.N3DS.Video.Stop();
            StopAllCoroutines();
            SceneManager.LoadScene("menu");
        }
    }

    private System.Collections.IEnumerator CheckIfVideoFinished()
    {
        // Esta es la función chivata. Espera pacientemente a que el vídeo termine solo.
        while (UnityEngine.N3DS.Video.IsPlaying)
        {
            yield return null;
        }
        SceneManager.LoadScene("menu");
    }
}