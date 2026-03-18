using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneOrchestrator : MonoBehaviour
{
    public static SceneOrchestrator Instance;

    public float waitTime = 3f;

    // Elementos de la escena Maestra que queremos desactivar cuando NO estemos en transición (Cámaras, UI, etc.)
    public GameObject[] maestraObjects;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    IEnumerator Start()
    {
        // Al arrancar, nos aseguramos que Maestra esté visible para la carga inicial
        SetMaestraActive(true);

        // Al arrancar la escena maestra (después de la intro), cargamos el menú de forma aditiva
        yield return SceneManager.LoadSceneAsync("menu", LoadSceneMode.Additive);

        // Una vez cargado, apagamos la Maestra para que el Menú tenga toda la RAM y GPU
        SetMaestraActive(false);
    }

    public void GoToGame()
    {
        StartCoroutine(TransitionRoutine("menu", "game"));
    }

    public void GoToMenu()
    {
        StartCoroutine(TransitionRoutine("game", "menu"));
    }

    private void SetMaestraActive(bool active)
    {
        if (maestraObjects == null) return;
        foreach (var obj in maestraObjects)
        {
            if (obj != null) obj.SetActive(active);
        }
    }

    private IEnumerator TransitionRoutine(string fromScene, string toScene)
    {
        // 1. Antes de descargar, activamos la Maestra (para mostrar fondo negro o UI de carga si hay)
        SetMaestraActive(true);

        // 2. Descargar la escena actual
        yield return SceneManager.UnloadSceneAsync(fromScene);

        // 3. Esperar 3 segundos reales para que la consola respire
        yield return new WaitForSecondsRealtime(waitTime);

        // 4. Cargar la escena destino de forma aditiva
        yield return SceneManager.LoadSceneAsync(toScene, LoadSceneMode.Additive);

        // 5. Desactivar Maestra para que la nueva escena tenga 100% de recursos
        SetMaestraActive(false);
    }
}
