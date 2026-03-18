using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ActoEscenaConverter : EditorWindow
{
    // Cada entrada: { rutaCSV, rutaSalida }
    private static readonly string[][] archivos = new string[][]
    {
        new string[] { "Assets/resources/csv/prueba/ES/prueba_ActoEscena_ES.csv", "Assets/StreamingAssets/prueba_actoescena_ES.dat" },
        new string[] { "Assets/resources/csv/prueba/EN/prueba_ActoEscena_EN.csv", "Assets/StreamingAssets/prueba_actoescena_EN.dat" },

        new string[] { "Assets/resources/csv/intro/ES/intro_ActoEscena_ES.csv", "Assets/StreamingAssets/intro_actoescena_ES.dat" },
        new string[] { "Assets/resources/csv/intro/EN/intro_ActoEscena_EN.csv", "Assets/StreamingAssets/intro_actoescena_EN.dat" },

        new string[] { "Assets/resources/csv/doctor/ES/doctor_ActoEscena_ES.csv", "Assets/StreamingAssets/doctor_actoescena_ES.dat" },
        new string[] { "Assets/resources/csv/doctor/EN/doctor_ActoEscena_EN.csv", "Assets/StreamingAssets/doctor_actoescena_EN.dat" },

        new string[] { "Assets/resources/csv/acto01/ES/acto01_ActoEscena_ES.csv", "Assets/StreamingAssets/acto01_actoescena_ES.dat" },
        new string[] { "Assets/resources/csv/acto01/EN/acto01_ActoEscena_EN.csv", "Assets/StreamingAssets/acto01_actoescena_EN.dat" },
    };

    [MenuItem("Proyect/Convert ActoEscena CSV to Binary")]
    public static void Convert()
    {
        foreach (string[] entry in archivos)
        {
            string csvPath = entry[0];
            string outputPath = entry[1];

            if (!File.Exists(csvPath)) { Debug.LogWarning("ActoEscena CSV no encontrado: " + csvPath); continue; }

            string[] lines = File.ReadAllLines(csvPath, Encoding.UTF8);

            List<int> hashes = new List<int>();
            List<string> actos = new List<string>();
            List<string> escenas = new List<string>();

            string lastActo = "";
            string lastEscena = "";

            for (int i = 1; i < lines.Length; i++)
            {
                // Regex para ignorar comas dentro de comillas
                string[] cols = System.Text.RegularExpressions.Regex.Split(lines[i], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                if (cols.Length < 1 || string.IsNullOrEmpty(cols[0].Trim())) continue;

                string id = cols[0].Trim().ToLower();
                int hash = HashHelper.GetID(id);

                if (cols.Length > 1 && !string.IsNullOrEmpty(cols[1].Trim())) lastActo = CleanQuotes(cols[1]);
                if (cols.Length > 2 && !string.IsNullOrEmpty(cols[2].Trim())) lastEscena = CleanQuotes(cols[2]);

                hashes.Add(hash);
                actos.Add(lastActo);
                escenas.Add(lastEscena);
            }

            using (BinaryWriter w = new BinaryWriter(File.Open(outputPath, FileMode.Create), Encoding.UTF8))
            {
                w.Write(hashes.Count);
                for (int i = 0; i < hashes.Count; i++)
                {
                    w.Write(hashes[i]);
                    w.Write(actos[i]);
                    w.Write(escenas[i]);
                }
            }

            Debug.Log("Convertido " + csvPath + " a .dat con " + hashes.Count + " entradas.");
        }
    }

    private static string CleanQuotes(string t)
    {
        t = t.Trim();
        if (t.StartsWith("\"") && t.EndsWith("\"")) t = t.Substring(1, t.Length - 2);
        return t.Replace("\"\"", "\"");
    }
}
