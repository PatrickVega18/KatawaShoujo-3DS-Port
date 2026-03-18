using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class EventConverter : EditorWindow
{
    // Cada entrada: { rutaCSV, rutaSalida }
    private static readonly string[][] archivos = new string[][]
    {
        new string[] { "Assets/resources/csv/prueba/prueba_eventos.csv", "Assets/StreamingAssets/prueba_eventos.dat" },

        new string[] { "Assets/resources/csv/intro/intro_eventos.csv", "Assets/StreamingAssets/intro_eventos.dat" },
    
        new string[] { "Assets/resources/csv/doctor/doctor_eventos.csv", "Assets/StreamingAssets/doctor_eventos.dat" },

        new string[] { "Assets/resources/csv/acto01/acto01_eventos.csv", "Assets/StreamingAssets/acto01_eventos.dat" },
    };

    [MenuItem("Proyect/Convert Event CSV to Binary")]
    public static void Convert()
    {
        foreach (string[] entry in archivos)
        {
            string csvPath = entry[0];
            string outputPath = entry[1];

            if (!File.Exists(csvPath)) { Debug.LogWarning("Eventos CSV no encontrado: " + csvPath); continue; }

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
                    if (currentCols.Count >= 2 && !string.IsNullOrEmpty(currentCols[0].Trim())) 
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
                if (currentCols.Count >= 2 && !string.IsNullOrEmpty(currentCols[0].Trim())) validRows.Add(currentCols.ToArray());
            }

            for (int r = 0; r < validRows.Count; r++) {
                for (int c = 0; c < validRows[r].Length; c++) {
                    string val = validRows[r][c];
                    if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2) {
                        validRows[r][c] = val.Substring(1, val.Length - 2).Replace("\"\"", "\"");
                    }
                }
            }

            string lastSFX = "";

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
                        int parsedType = ParseType(row[1].Trim());
                        w.Write(parsedType);
                        int isAuto = ParseInt(row[2]);
                        if (parsedType == 23 || parsedType == 24) isAuto = 1;
                        w.Write(isAuto);
                        w.Write(ParseInt(row[3])); w.Write(ParseFloat(row[4]));
                        
                        w.Write(ParseTargetAlpha(row[1].Trim()));
                        Vector2 hT = ParseHeartTime(row[4].Trim());
                        w.Write(hT.x); w.Write(hT.y);

                        Vector2 sz = ParseSize(row[5]); w.Write(sz.x); w.Write(sz.y);
                        Vector2 pos = ParseCoords(row[6]); w.Write(pos.x); w.Write(pos.y);
                        w.Write(ParseType(row[7].Trim()));
                        Vector2 lSz = ParseSize(row[8]); w.Write(lSz.x); w.Write(lSz.y);
                        Vector2 lPos = ParseCoords(row[9]); w.Write(lPos.x); w.Write(lPos.y);
                        if (row.Length > 10 && !string.IsNullOrEmpty(row[10].Trim())) lastSFX = row[10].Trim().ToLower();
                        w.Write(HashHelper.GetID(lastSFX));
                        int act = (row.Length > 11 && !string.IsNullOrEmpty(row[11].Trim())) ? ParseSFX(row[11].Trim()) : 0;
                        w.Write(act);
                        w.Write(row.Length > 12 ? ParseFloat(row[12]) : 1.0f);
                        w.Write(row.Length > 13 ? ParseInt(row[13]) : 0);
                    }
                    fs.Seek((int)tablePos, SeekOrigin.Begin);
                    for (int i = 0; i < validRows.Count; i++) { w.Write(hashes[i]); w.Write(offsets[i]); }
                }
            }
            Debug.Log("Convertido " + csvPath + " a .dat con " + validRows.Count + " entradas.");
        }
    }

    static int ParseType(string s) { string t = s.ToLower(); if (t.StartsWith("heartattack")) return 7; if (t.StartsWith("heartstop")) return 12; if (t == "flashback") return 8; if (t == "saveunlocker") return 9; if (t == "bbsin") return 10; if (t == "bbsout") return 11; if (t == "clearall") return 13; if (t == "restoreinputs") return 14; if (t == "clearimageeffect") return 15; if (t == "hidesnow") return 16; if (t == "showsnow") return 17; if (t == "centertext") return 18; if (t == "paneoin") return 25; if (t == "paneoout") return 26; if (t == "low-fadein-whitebox" || t == "back-fadein-whitebox") return 19; if (t == "low-fadeout-whitebox" || t == "back-fadeout-whitebox") return 20; if (t == "low-fadein-blackbox" || t == "back-fadein-blackbox") return 21; if (t == "low-fadeout-blackbox" || t == "back-fadeout-blackbox") return 22; if (t == "medicaltext") return 23; if (t == "medicaltextout") return 24; if (t == "fadein-whitebox") return 1; if (t == "fadeout-whitebox") return 2; if (t == "fadein-blackbox") return 3; if (t == "fadeout-blackbox") return 4; if (t == "sfx") return 5; if (t == "auto") return 6; return 0; }
    static int ParseSFX(string s) { string t = s.ToLower(); if (t == "play") return 1; if (t == "loop") return 2; if (t == "stop") return 3; return 0; }
    static int ParseInt(string s) { int i; int.TryParse(s.Trim(), out i); return i; }
    static float ParseFloat(string s) { float f; float.TryParse(s.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f); return f; }
    
    static float ParseTargetAlpha(string s) { string t = s.ToLower(); if (t.StartsWith("heartattack-") || t.StartsWith("heartstop-")) { string[] p = t.Split('-'); if (p.Length > 1) return ParseFloat(p[1]); } return 0f; }
    static Vector2 ParseHeartTime(string s) { string t = s.Trim(); if (t.Contains("-")) { string[] p = t.Split('-'); if (p.Length > 1) return new Vector2(ParseFloat(p[0]), ParseFloat(p[1])); } else if (!string.IsNullOrEmpty(t)) { return new Vector2(ParseFloat(t), 0); } return Vector2.zero; }
    
    static Vector2 ParseSize(string s) { if (string.IsNullOrEmpty(s)) return Vector2.zero; string[] p = s.ToLower().Split('x'); return p.Length < 2 ? Vector2.zero : new Vector2(ParseFloat(p[0]), ParseFloat(p[1])); }
    static Vector2 ParseCoords(string s) { if (string.IsNullOrEmpty(s)) return Vector2.zero; string[] p = s.Trim().Split(' '); return p.Length < 2 ? Vector2.zero : new Vector2(ParseFloat(p[0]), ParseFloat(p[1])); }
}