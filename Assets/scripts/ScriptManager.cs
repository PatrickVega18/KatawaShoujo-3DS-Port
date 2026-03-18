using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ScriptManager : MonoBehaviour
{
    private Dictionary<int, long> scriptOffsets = new Dictionary<int, long>();
    private Dictionary<int, long> eventOffsets = new Dictionary<int, long>();
    private Dictionary<int, long> decisionOffsets = new Dictionary<int, long>();
    private int nodeSize = 1388; 

    private FileStream fsScript, fsEvent, fsDecision;
    private BinaryReader brScript, brEvent, brDecision;

    private byte[] nodeBuffer;
    private MemoryStream msNode;
    private BinaryReader mbrNode;

    public ScriptNode nodeAnterior, nodeActual, nodeSiguiente;
    public int firstNodeHash = 0;

    public void Init(string scriptFile, string eventFile, string decisionFile)
    {
        CloseFiles();
        string pS = Path.Combine(Application.streamingAssetsPath, scriptFile);
        string pE = Path.Combine(Application.streamingAssetsPath, eventFile);
        string pD = Path.Combine(Application.streamingAssetsPath, decisionFile);

        nodeBuffer = new byte[nodeSize];
        msNode = new MemoryStream(nodeBuffer);
        mbrNode = new BinaryReader(msNode, Encoding.UTF8);

        if (File.Exists(pS)) { fsScript = new FileStream(pS, FileMode.Open, FileAccess.Read, FileShare.Read); brScript = new BinaryReader(fsScript, Encoding.UTF8); IndexScript(); }
        if (File.Exists(pE)) { fsEvent = new FileStream(pE, FileMode.Open, FileAccess.Read, FileShare.Read); brEvent = new BinaryReader(fsEvent, Encoding.UTF8); IndexMap(fsEvent, brEvent, eventOffsets); }
        if (File.Exists(pD)) { fsDecision = new FileStream(pD, FileMode.Open, FileAccess.Read, FileShare.Read); brDecision = new BinaryReader(fsDecision, Encoding.UTF8); IndexMap(fsDecision, brDecision, decisionOffsets); }
    }

    private void IndexScript()
    {
        scriptOffsets.Clear(); 
        if (fsScript.Length < 4) return;
        
        int count = brScript.ReadInt32();
        if (count > 0)
        {
            long pos = fsScript.Position;
            firstNodeHash = brScript.ReadInt32();
            if (!scriptOffsets.ContainsKey(firstNodeHash)) scriptOffsets.Add(firstNodeHash, pos);
            fsScript.Seek(nodeSize - 4, SeekOrigin.Current);

            for (int i = 1; i < count; i++)
            {
                pos = fsScript.Position;
                int idHash = brScript.ReadInt32();
                if (!scriptOffsets.ContainsKey(idHash)) scriptOffsets.Add(idHash, pos);
                fsScript.Seek(nodeSize - 4, SeekOrigin.Current);
            }
        }
    }

    private void IndexMap(FileStream fs, BinaryReader br, Dictionary<int, long> map)
    {
        map.Clear(); 
        if (fs.Length < 4) return;
        
        int count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        { 
            int hash = br.ReadInt32(); 
            long offset = br.ReadInt64(); 
            if (!map.ContainsKey(hash)) 
                map.Add(hash, offset); 
        }
    }

    public void UpdateCache(int currentHash)
    { 
        nodeAnterior = nodeActual; 
        nodeActual = ReadNodeFromDisk(currentHash); 
        nodeSiguiente = ReadNodeFromDisk(nodeActual.nextIdHash); 
    }

    private ScriptNode ReadNodeFromDisk(int hash) {
        if (hash == 0 || !scriptOffsets.ContainsKey(hash)) return new ScriptNode();
        fsScript.Seek(scriptOffsets[hash], SeekOrigin.Begin);
        fsScript.Read(nodeBuffer, 0, nodeSize);
        msNode.Seek(0, SeekOrigin.Begin);

        ScriptNode n = new ScriptNode();
        n.idHash = mbrNode.ReadInt32(); n.nextIdHash = mbrNode.ReadInt32(); n.dialogueHash = mbrNode.ReadInt32(); n.isEvent = mbrNode.ReadInt32(); n.nodeType = mbrNode.ReadInt32();
        n.checkVarName = Encoding.UTF8.GetString(mbrNode.ReadBytes(32)).Trim('\0');
        n.checkCond = Encoding.UTF8.GetString(mbrNode.ReadBytes(32)).Trim('\0');
        n.nextTrueHash = mbrNode.ReadInt32(); n.nextFalseHash = mbrNode.ReadInt32();
        n.music = ReadMusic(mbrNode); n.principal = ReadBG(mbrNode); n.secundario = ReadBG(mbrNode);
        n.char1 = ReadChar(mbrNode); n.char2 = ReadChar(mbrNode); n.char3 = ReadChar(mbrNode);
        n.char4 = ReadChar(mbrNode); n.char5 = ReadChar(mbrNode); n.char6 = ReadChar(mbrNode);
        return n;
    }

    public EventData GetEventData(int hash)
    {
        if (hash == 0 || !eventOffsets.ContainsKey(hash)) return new EventData();
        fsEvent.Seek(eventOffsets[hash], SeekOrigin.Begin);
        
        EventData d = new EventData();
        d.idHash = brEvent.ReadInt32(); d.eventType = brEvent.ReadInt32();
        d.auto = brEvent.ReadInt32(); d.forzado = brEvent.ReadInt32();
        d.tiempo = brEvent.ReadSingle(); d.targetAlpha = brEvent.ReadSingle();
        d.timeIn = brEvent.ReadSingle(); d.timeOut = brEvent.ReadSingle();
        d.sizeX = brEvent.ReadSingle(); d.sizeY = brEvent.ReadSingle();
        d.posX = brEvent.ReadSingle(); d.posY = brEvent.ReadSingle();
        d.lowerType = brEvent.ReadInt32(); d.lowerSizeX = brEvent.ReadSingle();
        d.lowerSizeY = brEvent.ReadSingle(); d.lowerPosX = brEvent.ReadSingle();
        d.lowerPosY = brEvent.ReadSingle();
        d.sfxHash = brEvent.ReadInt32(); d.sfxAction = brEvent.ReadInt32();
        d.sfxVolume = brEvent.ReadSingle(); d.sfxWait = brEvent.ReadInt32();
        return d;
    }

    public DecisionNode GetDecisionData(int hash)
    {
        if (hash == 0 || !decisionOffsets.ContainsKey(hash)) return new DecisionNode();
        fsDecision.Seek(decisionOffsets[hash], SeekOrigin.Begin);
        
        DecisionNode d = new DecisionNode();
        d.idHash = brDecision.ReadInt32(); d.optionCount = brDecision.ReadInt32();
        d.opt1 = ReadOption(brDecision); d.opt2 = ReadOption(brDecision);
        d.opt3 = ReadOption(brDecision); d.opt4 = ReadOption(brDecision);
        d.opt5 = ReadOption(brDecision);
        return d;
    }

    private DecisionOption ReadOption(BinaryReader r)
    {
        DecisionOption o = new DecisionOption();
        o.text = r.ReadString(); o.actionCode = r.ReadString(); o.nextIdHash = r.ReadInt32();
        return o;
    }

    private MusicData ReadMusic(BinaryReader r)
    {
        MusicData d = new MusicData();
        d.fileHash = r.ReadInt32(); d.actionType = r.ReadInt32();
        d.targetVolume = r.ReadSingle(); d.time = r.ReadSingle();
        return d;
    }

    private BackgroundData ReadBG(BinaryReader r)
    {
        BackgroundData d = new BackgroundData();
        d.fileHash = r.ReadInt32(); d.actionType = r.ReadInt32(); d.time = r.ReadSingle();
        d.startSize = new Vector2(r.ReadSingle(), r.ReadSingle());
        d.size = new Vector2(r.ReadSingle(), r.ReadSingle());
        d.coords = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        return d;
    }

    private CharacterSlot ReadChar(BinaryReader r)
    {
        CharacterSlot s = new CharacterSlot();
        s.cuerpo = ReadPart(r); s.cara = ReadPart(r); s.cabeza = ReadPart(r);
        s.brazoIzq = ReadPart(r); s.brazoDer = ReadPart(r);
        return s;
    }

    private SpritePartData ReadPart(BinaryReader r)
    {
        SpritePartData p = new SpritePartData();
        p.fileHash = r.ReadInt32(); p.switchHash = r.ReadInt32();
        p.actionType = r.ReadInt32(); p.time = r.ReadSingle();
        p.sizeX = r.ReadSingle(); p.sizeY = r.ReadSingle();
        p.posX = r.ReadSingle(); p.posY = r.ReadSingle();
        p.startPosX = r.ReadSingle(); p.startPosY = r.ReadSingle();
        return p;
    }

    public void UnloadContent()
    {
        CloseFiles();
        nodeBuffer = null;
        if (eventOffsets != null) eventOffsets.Clear();
        if (decisionOffsets != null) decisionOffsets.Clear();
    }

    private void CloseFiles()
    {
        if (brScript != null) { brScript.Close(); brScript = null; } 
        if (fsScript != null) { fsScript.Close(); fsScript = null; }
        if (brEvent != null) { brEvent.Close(); brEvent = null; } 
        if (fsEvent != null) { fsEvent.Close(); fsEvent = null; }
        if (brDecision != null) { brDecision.Close(); brDecision = null; } 
        if (fsDecision != null) { fsDecision.Close(); fsDecision = null; }
        if (mbrNode != null) { mbrNode.Close(); mbrNode = null; } 
        if (msNode != null) { msNode.Close(); msNode = null; }
    }

    void OnDestroy() 
    { 
        UnloadContent(); 
    }
}