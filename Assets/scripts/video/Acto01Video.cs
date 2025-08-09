using UnityEngine;
using UnityEngine.SceneManagement;

public class Acto01Video : MonoBehaviour
{
    void Start()
    {
        // Al empezar, ponemos el vídeo y activamos el chivato para saber cuándo acaba.
        string videoPath = Application.streamingAssetsPath + "/acto_01.moflex";
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
            SceneManager.LoadScene("acto_01_game");
        }
    }

    private System.Collections.IEnumerator CheckIfVideoFinished()
    {
        // Esta es la función chivata. Espera pacientemente a que el vídeo termine solo.
        while (UnityEngine.N3DS.Video.IsPlaying)
        {
            yield return null;
        }
        SceneManager.LoadScene("acto_01_game");
    }
}