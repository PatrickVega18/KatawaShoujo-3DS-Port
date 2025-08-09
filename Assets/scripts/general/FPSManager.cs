using UnityEngine;
using UnityEngine.UI;
using System.Text;

public class FPSManager : MonoBehaviour
{
    [Header("Configuración de FPS")]
    public int fotogramasPorSegundoObjetivo = 60;

    [Header("Visualización de FPS (UI Text)")]
    public bool mostrarFPS = true;
    public Text uiTextFPS;

    // Variables para el cálculo de FPS
    private float acumuladorDeltaTime = 0.0f;
    private int contadorFrames = 0;
    private float intervaloActualizacionFPS = 0.5f;
    private StringBuilder stringBuilderFPS;

    void Awake()
    {
        if (fotogramasPorSegundoObjetivo > 0)
        {
            Application.targetFrameRate = fotogramasPorSegundoObjetivo;
            Debug.Log(string.Format("FPSManager: Fotogramas por segundo objetivo fijados a {0}", fotogramasPorSegundoObjetivo));
        }
        stringBuilderFPS = new StringBuilder(16);

        if (uiTextFPS == null && mostrarFPS)
        {
            Debug.LogError("FPSManager: uiTextFPS no está asignado en el Inspector, pero mostrarFPS está activado.", gameObject);
            mostrarFPS = false;
        }
    }

    void Update()
    {
        if (uiTextFPS == null)
        {
            if (mostrarFPS)
            {
                Debug.LogWarning("FPSManager: uiTextFPS no asignado. No se pueden mostrar los FPS.", gameObject);
                mostrarFPS = false; // Evitar que el mensaje se repita
            }
            return;
        }

        if (mostrarFPS)
        {
            // Nos aseguramos de que el objeto Text esté activo si vamos a mostrar FPS
            if (!uiTextFPS.gameObject.activeSelf)
            {
                uiTextFPS.gameObject.SetActive(true);
            }

            acumuladorDeltaTime += Time.unscaledDeltaTime;
            contadorFrames++;

            if (acumuladorDeltaTime >= intervaloActualizacionFPS)
            {
                float fps = contadorFrames / acumuladorDeltaTime;

                stringBuilderFPS.Length = 0;
                stringBuilderFPS.AppendFormat("{0:F1}", fps); // F1 para un decimal
                uiTextFPS.text = stringBuilderFPS.ToString();

                acumuladorDeltaTime = 0.0f;
                contadorFrames = 0;
            }
        }
        else
        {
            // Si no queremos mostrar FPS, nos aseguramos de que el objeto Text esté inactivo
            if (uiTextFPS.gameObject.activeSelf)
            {
                uiTextFPS.gameObject.SetActive(false);
            }
        }
    }
}