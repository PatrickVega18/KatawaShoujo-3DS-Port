using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class DecisionConverter : EditorWindow
{
    // Cada entrada: { rutaCSV, rutaSalida }
    private static readonly string[][] archivos = new string[][]
    {
        new string[] { "Assets/resources/csv/prueba/ES/prueba_decisiones_ES.csv", "Assets/StreamingAssets/prueba_decisiones_ES.dat" },
        new string[] { "Assets/resources/csv/prueba/EN/prueba_decisiones_EN.csv", "Assets/StreamingAssets/prueba_decisiones_EN.dat" },

        new string[] { "Assets/resources/csv/intro/ES/intro_decisiones_ES.csv", "Assets/StreamingAssets/intro_decisiones_ES.dat" },
        new string[] { "Assets/resources/csv/intro/EN/intro_decisiones_EN.csv", "Assets/StreamingAssets/intro_decisiones_EN.dat" },

        new string[] { "Assets/resources/csv/doctor/ES/doctor_decisiones_ES.csv", "Assets/StreamingAssets/doctor_decisiones_ES.dat" },
        new string[] { "Assets/resources/csv/doctor/EN/doctor_decisiones_EN.csv", "Assets/StreamingAssets/doctor_decisiones_EN.dat" },
    
        new string[] { "Assets/resources/csv/acto01/ES/acto01_decisiones_ES.csv", "Assets/StreamingAssets/acto01_decisiones_ES.dat" },
        new string[] { "Assets/resources/csv/acto01/EN/acto01_decisiones_EN.csv", "Assets/StreamingAssets/acto01_decisiones_EN.dat" },
    };

    [MenuItem("Proyect/Convert Decision CSV to Binary")]
    public static void Convert()
    {
        foreach (string[] entry in archivos)
        {
            string csvPath = entry[0];
            string outputPath = entry[1];

            if (!File.Exists(csvPath)) { Debug.LogWarning("Decisiones CSV no encontrado: " + csvPath); continue; }

            string content = File.ReadAllText(csvPath, Encoding.UTF8);
            string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            List<string[]> validRows = new List<string[]>();

            for (int i = 1; i < lines.Length; i++) {
                string[] cols = Regex.Split(lines[i], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                if (cols.Length >= 2 && !string.IsNullOrEmpty(cols[0].Trim())) validRows.Add(cols);
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
                        w.Write(hashes[hashes.Count - 1]);
                        int count = 0;
                        for (int k = 0; k < 5; k++) { int idx = 1 + (k * 3); if (row.Length > idx && !string.IsNullOrEmpty(row[idx].Trim())) count++; }
                        w.Write(count);
                        for (int k = 0; k < 5; k++) {
                            int iT = 1+(k*3); int iV = 2+(k*3); int iN = 3+(k*3);
                            string txt = (row.Length > iT) ? CleanText(row[iT]) : "";
                            string val = (row.Length > iV) ? row[iV].Trim() : "";
                            string nxt = (row.Length > iN) ? row[iN].Trim().ToLower() : "";
                            w.Write(txt); w.Write(val); w.Write(HashHelper.GetID(nxt));
                        }
                    }
                    fs.Seek((int)tablePos, SeekOrigin.Begin);
                    for (int i = 0; i < validRows.Count; i++) { w.Write(hashes[i]); w.Write(offsets[i]); }
                }
            }
            Debug.Log("Convertido " + csvPath + " a .dat con " + validRows.Count + " entradas.");
        }
    }
    static string CleanText(string t) { t = t.Trim(); if (t.StartsWith("\"") && t.EndsWith("\"")) t = t.Substring(1, t.Length - 2); return t.Replace("\"\"", "\""); }
}