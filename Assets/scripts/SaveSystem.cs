using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class SaveSystem
{
    private static string GetPath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public static void SaveGame(int slotIndex, SaveHeader header, SaveBody body)
    {
        #if UNITY_N3DS && !UNITY_EDITOR
        UnityEngine.N3DS.FileSystemSave.Mount();
        #endif

        string bodyPath = GetPath("slot_" + slotIndex + ".sav");
        using (BinaryWriter w = new BinaryWriter(File.Open(bodyPath, FileMode.Create)))
        {
            w.Write(body.currentNodeHash);
            w.Write(body.currentActName);
            w.Write(body.currentActDescription ?? "");
            w.Write(body.currentSceneDescription ?? "");
            
            w.Write(body.bgMainHash); w.Write(body.bgSubHash);
            w.Write(body.bgMainAlpha); w.Write(body.bgSubAlpha);
            w.Write(body.bgMainSizeX); w.Write(body.bgMainSizeY);
            w.Write(body.bgSubSizeX); w.Write(body.bgSubSizeY);
            w.Write(body.bgMainActive); w.Write(body.bgSubActive);
            w.Write(body.bgIsSwapped);

            w.Write(body.vfxActive);
            w.Write(body.vfxSpriteHash);
            w.Write(body.vfxAlpha);
            w.Write(body.vfxSizeX); w.Write(body.vfxSizeY);
            w.Write(body.vfxPosX); w.Write(body.vfxPosY);

            w.Write(body.musicHash); w.Write(body.musicVolume);

            w.Write(body.sfxLoopHash); w.Write(body.sfxLoopVolume);

            w.Write(body.counterKeys.Count);
            for (int i = 0; i < body.counterKeys.Count; i++)
            {
                w.Write(body.counterKeys[i]);
                w.Write(body.counterValues[i]);
            }

            w.Write(body.flagKeys.Count);
            for (int i = 0; i < body.flagKeys.Count; i++)
            {
                w.Write(body.flagKeys[i]);
                w.Write(body.flagValues[i]);
            }

            w.Write(body.spriteStates.Count);
            foreach (SpriteState s in body.spriteStates)
            {
                w.Write(s.isActive);
                w.Write(s.mainHash); w.Write(s.subHash);
                w.Write(s.mainAlpha); w.Write(s.subAlpha);
                w.Write(s.mainSizeX); w.Write(s.mainSizeY); w.Write(s.mainPosX); w.Write(s.mainPosY);
                w.Write(s.subSizeX); w.Write(s.subSizeY); w.Write(s.subPosX); w.Write(s.subPosY);
                w.Write(s.siblingIndex);
                w.Write(s.isSwapped);
            }

            w.Write(body.snowActive);
            
            w.Write(body.bgMainPosX); w.Write(body.bgMainPosY);
            w.Write(body.bgSubPosX); w.Write(body.bgSubPosY);
            w.Write(body.lastSceneHash);
        }

        List<SaveHeader> headers = LoadHeaders();
        int foundIdx = -1;
        for (int i = 0; i < headers.Count; i++)
        {
            if (headers[i].slotIndex == slotIndex) { foundIdx = i; break; }
        }

        if (foundIdx != -1) headers[foundIdx] = header;
        else headers.Add(header);
        using (BinaryWriter w = new BinaryWriter(File.Open(GetPath("headers.dat"), FileMode.Create)))
        {
            w.Write(headers.Count);
            foreach (SaveHeader h in headers)
            {
                w.Write(h.slotIndex);
                w.Write(h.date);
                w.Write(h.actName);
                w.Write(h.sceneName);
                w.Write(h.backgroundHash);
                w.Write(h.playTime);
                w.Write(h.language ?? "ES");
                w.Write(h.lastSceneHash);
            }
        }

        #if UNITY_N3DS && !UNITY_EDITOR
        UnityEngine.N3DS.FileSystemSave.Unmount();
        #endif
    }

    public static List<SaveHeader> LoadHeaders()
    {
        List<SaveHeader> list = new List<SaveHeader>();
        string path = GetPath("headers.dat");
        
        if (!File.Exists(path)) return list;

        try
        {
            using (BinaryReader r = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    SaveHeader h = new SaveHeader();
                    h.slotIndex = r.ReadInt32();
                    h.date = r.ReadString();
                    h.actName = r.ReadString();
                    h.sceneName = r.ReadString();
                    h.backgroundHash = r.ReadInt32();
                    h.playTime = r.ReadSingle();
                    
                    h.language = "ES";
                    if (r.BaseStream.Position < r.BaseStream.Length)
                        h.language = r.ReadString();
                    
                    h.lastSceneHash = 0;
                    if (r.BaseStream.Position < r.BaseStream.Length)
                        h.lastSceneHash = r.ReadInt32();

                    list.Add(h);
                }
            }
        }
        catch (System.Exception)
        {
            list.Clear();
        }
        return list;
    }

    public static SaveBody LoadBody(int slotIndex)
    {
        string path = GetPath("slot_" + slotIndex + ".sav");
        if (!File.Exists(path)) return new SaveBody();

        SaveBody body = new SaveBody();
        using (BinaryReader r = new BinaryReader(File.Open(path, FileMode.Open)))
        {
            body.currentNodeHash = r.ReadInt32();
            body.currentActName = r.ReadString();
            
            body.currentActDescription = "";
            body.currentSceneDescription = "";
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                body.currentActDescription = r.ReadString();
                if (r.BaseStream.Position < r.BaseStream.Length)
                    body.currentSceneDescription = r.ReadString();
            }

            body.bgMainHash = r.ReadInt32(); body.bgSubHash = r.ReadInt32();
            body.bgMainAlpha = r.ReadSingle(); body.bgSubAlpha = r.ReadSingle();
            body.bgMainSizeX = r.ReadSingle(); body.bgMainSizeY = r.ReadSingle();
            body.bgSubSizeX = r.ReadSingle(); body.bgSubSizeY = r.ReadSingle();
            body.bgMainActive = r.ReadBoolean(); body.bgSubActive = r.ReadBoolean();
            body.bgIsSwapped = r.ReadBoolean();

            body.vfxActive = r.ReadBoolean();
            body.vfxSpriteHash = r.ReadInt32();
            body.vfxAlpha = r.ReadSingle();
            body.vfxSizeX = r.ReadSingle(); body.vfxSizeY = r.ReadSingle();
            body.vfxPosX = r.ReadSingle(); body.vfxPosY = r.ReadSingle();

            body.musicHash = r.ReadInt32(); body.musicVolume = r.ReadSingle();

            body.sfxLoopHash = r.ReadInt32(); body.sfxLoopVolume = r.ReadSingle();

            int cCount = r.ReadInt32();
            body.counterKeys = new List<string>(); body.counterValues = new List<int>();
            for (int i = 0; i < cCount; i++) {
                body.counterKeys.Add(r.ReadString());
                body.counterValues.Add(r.ReadInt32());
            }

            int fCount = r.ReadInt32();
            body.flagKeys = new List<string>(); body.flagValues = new List<bool>();
            for (int i = 0; i < fCount; i++) {
                body.flagKeys.Add(r.ReadString());
                body.flagValues.Add(r.ReadBoolean());
            }

            int sCount = r.ReadInt32();
            body.spriteStates = new List<SpriteState>();
            for (int i = 0; i < sCount; i++)
            {
                SpriteState s = new SpriteState();
                s.isActive = r.ReadBoolean();
                s.mainHash = r.ReadInt32(); s.subHash = r.ReadInt32();
                s.mainAlpha = r.ReadSingle(); s.subAlpha = r.ReadSingle();
                s.mainSizeX = r.ReadSingle(); s.mainSizeY = r.ReadSingle(); s.mainPosX = r.ReadSingle(); s.mainPosY = r.ReadSingle();
                s.subSizeX = r.ReadSingle(); s.subSizeY = r.ReadSingle(); s.subPosX = r.ReadSingle(); s.subPosY = r.ReadSingle();
                s.siblingIndex = r.ReadInt32();
                s.isSwapped = r.ReadBoolean();
                body.spriteStates.Add(s);
            }

            body.snowActive = true;
            if (r.BaseStream.Position < r.BaseStream.Length)
                body.snowActive = r.ReadBoolean();

            try {
                body.bgMainPosX = r.ReadSingle(); body.bgMainPosY = r.ReadSingle();
                body.bgSubPosX = r.ReadSingle(); body.bgSubPosY = r.ReadSingle();
            } catch {
                body.bgMainPosX = 0f; body.bgMainPosY = 0f;
                body.bgSubPosX = 0f; body.bgSubPosY = 0f;
            }

            body.lastSceneHash = 0;
            if (r.BaseStream.Position < r.BaseStream.Length)
                body.lastSceneHash = r.ReadInt32();
        }
        return body;
    }

    public static void DeleteSlot(int slotIndex)
    {
        #if UNITY_N3DS && !UNITY_EDITOR
        UnityEngine.N3DS.FileSystemSave.Mount();
        #endif

        string bodyPath = GetPath("slot_" + slotIndex + ".sav");
        if (File.Exists(bodyPath)) File.Delete(bodyPath);

        List<SaveHeader> headers = LoadHeaders();
        SaveHeader toRemove = new SaveHeader { slotIndex = -1 };
        
        foreach (var h in headers) {
            if (h.slotIndex == slotIndex) { toRemove = h; break; }
        }

        if (toRemove.slotIndex != -1)
        {
            headers.Remove(toRemove);
            using (BinaryWriter w = new BinaryWriter(File.Open(GetPath("headers.dat"), FileMode.Create)))
            {
                w.Write(headers.Count);
                foreach (SaveHeader h in headers)
                {
                    w.Write(h.slotIndex); w.Write(h.date); w.Write(h.actName);
                    w.Write(h.sceneName); w.Write(h.backgroundHash); w.Write(h.playTime);
                    w.Write(h.language ?? "ES");
                    w.Write(h.lastSceneHash);
                }
            }
        }

        #if UNITY_N3DS && !UNITY_EDITOR
        UnityEngine.N3DS.FileSystemSave.Unmount();
        #endif
    }
}