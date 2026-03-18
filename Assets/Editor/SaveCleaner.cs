using UnityEngine;
using UnityEditor;
using System.IO;

public class SaveCleaner : EditorWindow
{
    [MenuItem("Proyect/Borrar Todos los Saves")]
    public static void ClearAllSaves()
    {
        string path = Application.persistentDataPath;
        int deleted = 0;

        // Borrar headers.dat
        string headersPath = Path.Combine(path, "headers.dat");
        if (File.Exists(headersPath)) { File.Delete(headersPath); deleted++; }

        // Borrar todos los slot_*.sav
        string[] slotFiles = Directory.GetFiles(path, "slot_*.sav");
        foreach (string f in slotFiles) { File.Delete(f); deleted++; }

        Debug.Log("SaveCleaner: " + deleted + " archivo(s) eliminado(s) de " + path);
        EditorUtility.DisplayDialog("Saves Borrados", deleted + " archivo(s) eliminado(s).", "OK");
    }
}
