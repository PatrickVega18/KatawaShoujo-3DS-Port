using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NovelaSaveData
{
    // Datos de Flujo y Variables
    public string sceneName;
    public string currentNodeID;
    public string sceneDescriptionNodeID;
    public float playtimeInSeconds;
    public Dictionary<string, int> sumVariables;
    public Dictionary<string, bool> flagVariables;

    public SFXSaveData sfxState;
    public MusicSaveData musicState;
    public BackgroundSaveData backgroundState;
    public List<SpriteSaveData> spriteStates;
    public List<SpriteSaveData> faceStates;
    public List<SpriteSaveData> headStates;
    public List<SpriteSaveData> leftArmStates;
    public List<SpriteSaveData> rightArmStates;

    public NovelaSaveData()
    {
        sumVariables = new Dictionary<string, int>();
        flagVariables = new Dictionary<string, bool>();
        spriteStates = new List<SpriteSaveData>();
        faceStates = new List<SpriteSaveData>();
        headStates = new List<SpriteSaveData>();
        leftArmStates = new List<SpriteSaveData>();
        rightArmStates = new List<SpriteSaveData>();
        musicState = new MusicSaveData();
        backgroundState = new BackgroundSaveData();
        sfxState = new SFXSaveData();
    }
}