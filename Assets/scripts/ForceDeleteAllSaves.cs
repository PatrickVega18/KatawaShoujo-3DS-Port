using UnityEngine;
using System.IO;

public class ForceDeleteAllSaves : MonoBehaviour
{
    public KeyCode deleteKey = KeyCode.Y;

    void Update()
    {
        // Esto de aquí verifica si se ha pulsado la tecla de borrado para... borrar todo XD
        if (Input.GetKeyDown(deleteKey))
        {
            Debug.LogWarning("--- BORRADO FORZADO DE SAVES INICIADO ---");

            string path = Application.persistentDataPath;

            string[] saveFiles = Directory.GetFiles(path, "save*.bin");

            if (saveFiles.Length == 0)
            {
                Debug.Log("No se encontraron archivos de guardado para borrar.");
                return;
            }

            foreach (string file in saveFiles)
            {
                try
                {
                    File.Delete(file);
                    Debug.Log("Archivo borrado: " + file);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("No se pudo borrar el archivo: " + file + " - Error: " + e.Message);
                }
            }

            Debug.LogWarning("--- BORRADO FORZADO COMPLETADO. Reinicia el juego para que los cambios surtan efecto. ---");
        }
    }
}