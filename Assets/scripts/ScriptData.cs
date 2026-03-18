using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public struct BackgroundData
{
    public int fileHash;
    public int actionType;
    public float time;
    public Vector2 startSize;
    public Vector2 size;
    public Vector3 coords;
}

[Serializable]
public struct MusicData
{
    public int fileHash;
    public int actionType;
    public float targetVolume;
    public float time;
}

[Serializable]
public struct SpritePartData
{
    public int fileHash;
    public int switchHash;
    public int actionType;
    public float time;
    public float sizeX;
    public float sizeY;
    public float posX;
    public float posY;
    public float startPosX;
    public float startPosY;
}

[Serializable]
public struct CharacterSlot
{
    public SpritePartData cuerpo;
    public SpritePartData cara;
    public SpritePartData cabeza;
    public SpritePartData brazoIzq;
    public SpritePartData brazoDer;
}

[Serializable]
public struct ScriptNode
{
    public int idHash;
    public int nextIdHash;
    public int dialogueHash; 
    public int isEvent; 
    public int nodeType;

    public string checkVarName; 
    public string checkCond;
    public int nextTrueHash; 
    public int nextFalseHash;

    public MusicData music;
    public BackgroundData principal;
    public BackgroundData secundario;
    public CharacterSlot char1;
    public CharacterSlot char2;
    public CharacterSlot char3;
    public CharacterSlot char4;
    public CharacterSlot char5;
    public CharacterSlot char6;
}

[Serializable]
public struct DialogueEntry
{
    public string name;
    public string text;
    public float speed;
}

[Serializable]
public struct DecisionOption
{
    public string text;
    public string actionCode;
    public int nextIdHash;
}

[Serializable]
public struct DecisionNode
{
    public int idHash;
    public int optionCount;
    public DecisionOption opt1;
    public DecisionOption opt2;
    public DecisionOption opt3;
    public DecisionOption opt4;
    public DecisionOption opt5;
}

[Serializable]
public struct EventData
{
    public int idHash;
    public int eventType; 
    public int auto;
    public int forzado;
    public float tiempo;
    public float targetAlpha; 
    public float timeIn; 
    public float timeOut;
    public float sizeX; public float sizeY; public float posX; public float posY;
    public int lowerType;
    public float lowerSizeX; public float lowerSizeY; public float lowerPosX; public float lowerPosY;
    public int sfxHash;
    public int sfxAction; 
    public float sfxVolume;
    public int sfxWait;
}