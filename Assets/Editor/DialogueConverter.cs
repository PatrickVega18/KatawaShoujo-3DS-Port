using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class DialogueConverter : EditorWindow
{
    // Cada entrada: { rutaCSV, rutaSalida }
    private static readonly string[][] archivos = new string[][]
    {
        new string[] { "Assets/resources/csv/prueba/ES/prueba_dialogos_ES.csv", "Assets/StreamingAssets/prueba_dialogos_ES.dat" },
        new string[] { "Assets/resources/csv/prueba/EN/prueba_dialogos_EN.csv", "Assets/StreamingAssets/prueba_dialogos_EN.dat" },

        new string[] { "Assets/resources/csv/intro/ES/intro_dialogos_ES.csv", "Assets/StreamingAssets/intro_dialogos_ES.dat" },
        new string[] { "Assets/resources/csv/intro/EN/intro_dialogos_EN.csv", "Assets/StreamingAssets/intro_dialogos_EN.dat" },
    
        new string[] { "Assets/resources/csv/doctor/ES/doctor_dialogos_ES.csv", "Assets/StreamingAssets/doctor_dialogos_ES.dat" },
        new string[] { "Assets/resources/csv/doctor/EN/doctor_dialogos_EN.csv", "Assets/StreamingAssets/doctor_dialogos_EN.dat" },

        new string[] { "Assets/resources/csv/acto01/ES/acto01_dialogos_ES.csv", "Assets/StreamingAssets/acto01_dialogos_ES.dat" },
        new string[] { "Assets/resources/csv/acto01/EN/acto01_dialogos_EN.csv", "Assets/StreamingAssets/acto01_dialogos_EN.dat" },
    };

    [MenuItem("Proyect/Convert Dialogue CSV to Binary")]
    public static void Convert()
    {
        foreach (string[] entry in archivos)
        {
            string csvPath = entry[0];
            string outputPath = entry[1];

            if (!File.Exists(csvPath)) { Debug.LogWarning("Dialogos CSV no encontrado: " + csvPath); continue; }

            string content = File.ReadAllText(csvPath, Encoding.UTF8);
            List<string[]> validRows = new List<string[]>();

            // Custom CSV Parser para sorportar multilineas (enters) dentro de strings con comillas
            List<string> currentCols = new List<string>();
            bool inQuotes = false;
            StringBuilder currentVal = new StringBuilder();

            // Salta la primera linea (header)
            int charIndex = 0;
            while (charIndex < content.Length && content[charIndex] != '\n') charIndex++;
            if (charIndex < content.Length) charIndex++;

            for (int i = charIndex; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    currentVal.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    currentCols.Add(currentVal.ToString());
                    currentVal.Length = 0;
                }
                else if ((c == '\n' || c == '\r') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++; // Skip \n of \r\n
                    currentCols.Add(currentVal.ToString());
                    if (currentCols.Count >= 3 && !string.IsNullOrEmpty(currentCols[0].Trim())) 
                    {
                        validRows.Add(currentCols.ToArray());
                    }
                    currentCols.Clear();
                    currentVal.Length = 0;
                }
                else
                {
                    currentVal.Append(c);
                }
            }
            if (currentVal.Length > 0 || currentCols.Count > 0)
            {
                currentCols.Add(currentVal.ToString());
                if (currentCols.Count >= 3 && !string.IsNullOrEmpty(currentCols[0].Trim())) validRows.Add(currentCols.ToArray());
            }

            using (FileStream fs = new FileStream(outputPath, FileMode.Create)) {
                using (BinaryWriter w = new BinaryWriter(fs, Encoding.UTF8)) {
                    w.Write(validRows.Count);
                    long tablePos = fs.Position;
                    for (int i = 0; i < validRows.Count; i++) { w.Write(0); w.Write(0L); }
                    List<int> hashes = new List<int>();
                    List<long> offsets = new List<long>();

                    foreach (string[] row in validRows) {
                        hashes.Add(HashHelper.GetID(row[0].Trim().ToLower()));
                        offsets.Add(fs.Position);
                        w.Write(row[1].Trim());
                        string t = row[2].Trim();
                        if (t.StartsWith("\"") && t.EndsWith("\"")) t = t.Substring(1, t.Length - 2);
                        t = t.Replace("\"\"", "\"").Replace("\\n", "\n").Replace(" \n ", "\n");
                        w.Write(t);
                        float spd = 0.025f;
                        if (row.Length > 3) float.TryParse(row[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out spd);
                        w.Write(spd);
                    }
                    fs.Seek((int)tablePos, SeekOrigin.Begin);
                    for (int i = 0; i < validRows.Count; i++) { w.Write(hashes[i]); w.Write(offsets[i]); }
                }
            }
            Debug.Log("Convertido " + csvPath + " a .dat con " + validRows.Count + " entradas.");
        }
    }
}