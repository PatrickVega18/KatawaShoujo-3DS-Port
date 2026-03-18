using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ScriptConverter : EditorWindow
{
    // Cada entrada: { rutaCSV, rutaSalida }
    private static readonly string[][] archivos = new string[][]
    {
        new string[] { "Assets/resources/csv/prueba/prueba_guion.csv", "Assets/StreamingAssets/prueba_guion.dat" },
        
        new string[] { "Assets/resources/csv/intro/intro_guion.csv", "Assets/StreamingAssets/intro_guion.dat" },
    
        new string[] { "Assets/resources/csv/doctor/doctor_guion.csv", "Assets/StreamingAssets/doctor_guion.dat" },
    
        new string[] { "Assets/resources/csv/acto01/acto01_guion.csv", "Assets/StreamingAssets/acto01_guion.dat" },
    };

    [MenuItem("Proyect/Convert CSV to Binary")]
    public static void Convert()
    {
      foreach (string[] entry in archivos)
      {
        string csvPath = entry[0];
        string outputPath = entry[1];

        if (!File.Exists(csvPath)) { Debug.LogWarning("Guion CSV no encontrado: " + csvPath); continue; }

        string[] lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        List<ScriptNode> nodes = new List<ScriptNode>();

        string lastM_File = ""; string lastM_Action = ""; string lastM_Time = "0";
        string lastP_File = ""; string lastP_Size = "400x240"; string lastP_Coords = "0 0 0";
        string lastS_File = ""; string lastS_Size = "400x240"; string lastS_Coords = "0 0 0";
        string[,] lastSpriteFiles = new string[6, 5];
        string[,] lastSpriteSizes = new string[6, 5];
        string[,] lastSpriteCoords = new string[6, 5];

        for (int s = 0; s < 6; s++) {
            for (int p = 0; p < 5; p++) {
                lastSpriteFiles[s, p] = ""; lastSpriteSizes[s, p] = "0x0"; lastSpriteCoords[s, p] = "0 0";
            }
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            if (cols.Length < 1) continue;

            // Limpiar variables de arrastre que causaban bleeding de ramificaciones
            lastM_File = ""; lastM_Action = ""; lastM_Time = "0";

            ScriptNode node = new ScriptNode();
            node.idHash = HashHelper.GetID(cols[0].Trim().ToLower());
            node.nextIdHash = (cols.Length > 2 && !string.IsNullOrEmpty(cols[2].Trim())) ? HashHelper.GetID(cols[2].Trim().ToLower()) : 0;
            node.dialogueHash = node.idHash;

            node.checkVarName = ""; node.checkCond = ""; node.nextTrueHash = 0; node.nextFalseHash = 0;

            string tipoStr = (cols.Length > 1) ? cols[1].Trim().ToLower() : "";
            node.nodeType = 0; node.isEvent = 0; 
            if (tipoStr == "evento") { node.nodeType = 1; node.isEvent = 1; }
            else if (tipoStr == "decision") { node.nodeType = 2; }
            else if (tipoStr == "check" || tipoStr == "checksum" || tipoStr == "checkflag") { node.nodeType = 3; }
            else if (tipoStr.StartsWith("changeescene-")) { node.nodeType = 4; node.checkVarName = tipoStr.Replace("changeescene-", "").Trim(); }
            else if (tipoStr.StartsWith("video-")) { node.nodeType = 5; node.checkVarName = tipoStr.Replace("video-", "").Trim(); }
            else if (tipoStr == "gomenu") { node.nodeType = 8; }
            else if (tipoStr == "eventotexto") { node.nodeType = 6; node.isEvent = 0; }
            else if (tipoStr == "eventotextoespecial") { node.nodeType = 7; node.isEvent = 1; }

            if (node.nodeType == 3) {
                if (cols.Length > 3) node.checkVarName = cols[3].Trim().ToLower();
                if (cols.Length > 4) node.checkCond = cols[4].Trim().ToLower();
                if (cols.Length > 5) node.nextTrueHash = HashHelper.GetID(cols[5].Trim().ToLower());
                if (cols.Length > 6) node.nextFalseHash = HashHelper.GetID(cols[6].Trim().ToLower());
            }

            if (cols.Length > 9) {
                if (!string.IsNullOrEmpty(cols[7].Trim())) lastM_File = cols[7].Trim().ToLower();
                if (!string.IsNullOrEmpty(cols[8].Trim())) lastM_Action = cols[8].Trim().ToLower();
                if (!string.IsNullOrEmpty(cols[9].Trim())) lastM_Time = cols[9].Trim();
                node.music = ParseMusicData(lastM_File, lastM_Action, lastM_Time);
            }

            if (cols.Length > 14) {
                if (!string.IsNullOrEmpty(cols[10].Trim())) lastP_File = cols[10].Trim().ToLower();
                if (!string.IsNullOrEmpty(cols[11].Trim())) lastP_Size = cols[11].Trim();
                if (cols.Length > 14 && !string.IsNullOrEmpty(cols[14].Trim())) lastP_Coords = cols[14].Trim();
            }
            node.principal = new BackgroundData();
            node.principal.fileHash = HashHelper.GetID(lastP_File);
            node.principal.actionType = (cols.Length > 12 && !string.IsNullOrEmpty(cols[12])) ? GetActionID(cols[12].Trim()) : 0;
            node.principal.time = (cols.Length > 13 && !string.IsNullOrEmpty(cols[13])) ? ParseFloat(cols[13].Trim()) : 0;
            
            if (node.principal.actionType == 8 && lastP_Size.Contains("-")) {
                string[] sizes = lastP_Size.Split('-');
                node.principal.startSize = ParseSize(sizes[0]);
                node.principal.size = ParseSize(sizes[1]);
            } else if ((node.principal.actionType == 6 || node.principal.actionType == 7) && cols.Length > 12 && cols[12].Contains("_")) {
                string[] parts = cols[12].Trim().Split('_');
                if (parts.Length >= 3) {
                    node.principal.startSize = new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2]));
                } else {
                    node.principal.startSize = Vector2.zero;
                }
                node.principal.size = ParseSize(lastP_Size);
            } else {
                node.principal.startSize = ParseSize(lastP_Size);
                node.principal.size = ParseSize(lastP_Size);
            }
            node.principal.coords = ParseCoords(lastP_Coords);

            if (cols.Length > 17) {
                if (!string.IsNullOrEmpty(cols[15].Trim())) lastS_File = cols[15].Trim().ToLower();
                if (!string.IsNullOrEmpty(cols[16].Trim())) lastS_Size = cols[16].Trim();
                if (!string.IsNullOrEmpty(cols[17].Trim())) lastS_Coords = cols[17].Trim();
            }
            node.secundario = new BackgroundData();
            node.secundario.fileHash = HashHelper.GetID(lastS_File);
            node.secundario.startSize = ParseSize(lastS_Size);
            node.secundario.size = ParseSize(lastS_Size);
            node.secundario.coords = ParseCoords(lastS_Coords);

            node.char1 = ProcessCharacter(cols, 0, ref lastSpriteFiles, ref lastSpriteSizes, ref lastSpriteCoords);
            node.char2 = ProcessCharacter(cols, 1, ref lastSpriteFiles, ref lastSpriteSizes, ref lastSpriteCoords);
            node.char3 = ProcessCharacter(cols, 2, ref lastSpriteFiles, ref lastSpriteSizes, ref lastSpriteCoords);
            node.char4 = ProcessCharacter(cols, 3, ref lastSpriteFiles, ref lastSpriteSizes, ref lastSpriteCoords);
            node.char5 = ProcessCharacter(cols, 4, ref lastSpriteFiles, ref lastSpriteSizes, ref lastSpriteCoords);
            node.char6 = ProcessCharacter(cols, 5, ref lastSpriteFiles, ref lastSpriteSizes, ref lastSpriteCoords);
            nodes.Add(node);
        }

        using (BinaryWriter w = new BinaryWriter(File.Open(outputPath, FileMode.Create), Encoding.UTF8))
        {
            w.Write(nodes.Count);
            foreach (ScriptNode n in nodes) {
                w.Write(n.idHash); w.Write(n.nextIdHash); w.Write(n.dialogueHash); w.Write(n.isEvent); w.Write(n.nodeType);
                WriteFixedString(w, n.checkVarName, 32); WriteFixedString(w, n.checkCond, 32); 
                w.Write(n.nextTrueHash); w.Write(n.nextFalseHash);
                w.Write(n.music.fileHash); w.Write(n.music.actionType); w.Write(n.music.targetVolume); w.Write(n.music.time);
                WriteBG(w, n.principal); WriteBG(w, n.secundario);
                WriteChar(w, n.char1); WriteChar(w, n.char2); WriteChar(w, n.char3);
                WriteChar(w, n.char4); WriteChar(w, n.char5); WriteChar(w, n.char6);
            }
        }
        Debug.Log("Convertido " + csvPath + " a .dat con " + nodes.Count + " entradas.");
      } // foreach
    }

    static void WriteFixedString(BinaryWriter w, string s, int length) {
        byte[] buffer = new byte[length];
        if (!string.IsNullOrEmpty(s)) {
            byte[] strBytes = Encoding.UTF8.GetBytes(s);
            int copyLen = Mathf.Min(strBytes.Length, length);
            System.Array.Copy(strBytes, buffer, copyLen);
        }
        w.Write(buffer);
    }

    static MusicData ParseMusicData(string file, string action, string time) {
        MusicData data = new MusicData(); data.fileHash = HashHelper.GetID(file); data.time = ParseFloat(time); data.targetVolume = 1.0f;
        if (action.StartsWith("fadein")) { data.actionType = 1; string[] p = action.Split('-'); if (p.Length > 1) data.targetVolume = ParseFloat(p[1]); }
        else if (action.StartsWith("fadeout")) { data.actionType = 2; data.targetVolume = 0; }
        else if (action.StartsWith("v-")) { data.actionType = 3; string[] p = action.Split('-'); if (p.Length > 1) data.targetVolume = ParseFloat(p[1]); }
        return data;
    }

    static CharacterSlot ProcessCharacter(string[] cols, int charIdx, ref string[,] lastFiles, ref string[,] lastSizes, ref string[,] lastCoords) {
        CharacterSlot slot = new CharacterSlot(); int startCol = 18 + (charIdx * 30);
        slot.cuerpo = ProcessPart(cols, startCol, charIdx, 0, ref lastFiles, ref lastSizes, ref lastCoords);
        slot.cara = ProcessPart(cols, startCol + 6, charIdx, 1, ref lastFiles, ref lastSizes, ref lastCoords);
        slot.cabeza = ProcessPart(cols, startCol + 12, charIdx, 2, ref lastFiles, ref lastSizes, ref lastCoords);
        slot.brazoIzq = ProcessPart(cols, startCol + 18, charIdx, 3, ref lastFiles, ref lastSizes, ref lastCoords);
        slot.brazoDer = ProcessPart(cols, startCol + 24, charIdx, 4, ref lastFiles, ref lastSizes, ref lastCoords);
        return slot;
    }

    static SpritePartData ProcessPart(string[] cols, int start, int sIdx, int pIdx, ref string[,] lastFiles, ref string[,] lastSizes, ref string[,] lastCoords) {
        SpritePartData data = new SpritePartData();
        if (cols.Length > start) {
            if (!string.IsNullOrEmpty(cols[start].Trim())) lastFiles[sIdx, pIdx] = cols[start].Trim().ToLower();
            if (cols.Length > start + 1 && !string.IsNullOrEmpty(cols[start + 1].Trim())) lastSizes[sIdx, pIdx] = cols[start + 1].Trim();
            if (cols.Length > start + 4 && !string.IsNullOrEmpty(cols[start + 4].Trim())) lastCoords[sIdx, pIdx] = cols[start + 4].Trim();
            data.fileHash = HashHelper.GetID(lastFiles[sIdx, pIdx]);
            data.actionType = (cols.Length > start + 2 && !string.IsNullOrEmpty(cols[start+2])) ? GetActionID(cols[start + 2].Trim()) : 0;
            data.switchHash = (cols.Length > start + 3) ? HashHelper.GetID(cols[start + 3].Trim().ToLower()) : 0;
            data.time = (cols.Length > start + 5) ? ParseFloat(cols[start + 5].Trim()) : 0;
            Vector2 sz = ParseSize(lastSizes[sIdx, pIdx]); data.sizeX = sz.x; data.sizeY = sz.y;
            string coordStr = lastCoords[sIdx, pIdx];
            if (!string.IsNullOrEmpty(coordStr) && coordStr.Contains("|")) {
                string[] parts = coordStr.Split('|');
                Vector3 startPos = ParseCoords(parts[0]);
                Vector3 endPos = ParseCoords(parts[1]);
                data.startPosX = startPos.x; data.startPosY = startPos.y;
                data.posX = endPos.x; data.posY = endPos.y;
            } else if ((data.actionType == 6 || data.actionType == 7 || data.actionType == 12 || data.actionType == 13) && cols.Length > start + 2 && cols[start + 2].Contains("_")) {
                string[] parts = cols[start + 2].Trim().Split('_');
                bool isCompound = (data.actionType == 12 || data.actionType == 13);
                int offset = isCompound ? 3 : 1; 

                if (parts.Length >= (offset + 1)) {
                    data.startPosX = ParseFloat(parts[offset]);
                    data.startPosY = ParseFloat(parts[offset + 1]);
                } else {
                    data.startPosX = 0; data.startPosY = 0;
                }
                Vector3 pos = ParseCoords(coordStr); 
                data.posX = pos.x; data.posY = pos.y;
            } else {
                Vector3 pos = ParseCoords(coordStr); 
                data.startPosX = pos.x; data.startPosY = pos.y;
                data.posX = pos.x; data.posY = pos.y;
            }
        }
        return data;
    }

    static void WriteBG(BinaryWriter w, BackgroundData d) { w.Write(d.fileHash); w.Write(d.actionType); w.Write(d.time); w.Write(d.startSize.x); w.Write(d.startSize.y); w.Write(d.size.x); w.Write(d.size.y); w.Write(d.coords.x); w.Write(d.coords.y); w.Write(d.coords.z); }
    static void WriteChar(BinaryWriter w, CharacterSlot s) { WritePart(w, s.cuerpo); WritePart(w, s.cara); WritePart(w, s.cabeza); WritePart(w, s.brazoIzq); WritePart(w, s.brazoDer); }
    static void WritePart(BinaryWriter w, SpritePartData d) { w.Write(d.fileHash); w.Write(d.switchHash); w.Write(d.actionType); w.Write(d.time); w.Write(d.sizeX); w.Write(d.sizeY); w.Write(d.posX); w.Write(d.posY); w.Write(d.startPosX); w.Write(d.startPosY); }
    static int GetActionID(string act) { act = act.ToLower(); if (act == "fadein") return 1; if (act == "fadeout") return 2; if (act == "switch") return 3; if (act == "visible") return 4; if (act == "move") return 5; if (act.StartsWith("visible_and_shakex")) return 12; if (act.StartsWith("visible_and_shakey")) return 13; if (act.StartsWith("shakex")) return 6; if (act.StartsWith("shakey")) return 7; if (act == "zoomout") return 8; if (act == "movefadein") return 9; if (act == "movefadeout") return 10; if (act == "moveforce") return 11; return 0; }
    static float ParseFloat(string s) { float f; float.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f); return f; }
    static Vector2 ParseSize(string s) { if (string.IsNullOrEmpty(s) || s == "0x0") return Vector2.zero; string[] p = s.ToLower().Split('x'); return p.Length < 2 ? Vector2.zero : new Vector2(ParseFloat(p[0]), ParseFloat(p[1])); }
    static Vector3 ParseCoords(string s) { if (string.IsNullOrEmpty(s)) return Vector3.zero; string[] p = s.Trim().Split(' '); return new Vector3(p.Length > 0 ? ParseFloat(p[0]) : 0, p.Length > 1 ? ParseFloat(p[1]) : 0, 0); }
}