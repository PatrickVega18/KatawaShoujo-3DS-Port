using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public struct SaveHeader
{
    public int slotIndex;
    public string date;
    public string actName;
    public string sceneName;
    public int backgroundHash;
    public float playTime;
    public string language;
    public int lastSceneHash;
}

[Serializable]
public struct SpriteState
{
    public bool isActive;
    public int mainHash;
    public int subHash;
    public float mainAlpha;
    public float subAlpha;
    public float mainSizeX, mainSizeY, mainPosX, mainPosY;
    public float subSizeX, subSizeY, subPosX, subPosY;
    public int siblingIndex;
    public bool isSwapped;
}

[Serializable]
public struct SaveBody
{
    public int currentNodeHash;
    public string currentActName;
    public string currentActDescription;
    public string currentSceneDescription;
    public int lastSceneHash;
    
    // Estado de Fondos
    public int bgMainHash;
    public int bgSubHash;
    public float bgMainAlpha;
    public float bgSubAlpha;
    public float bgMainSizeX, bgMainSizeY;
    public float bgSubSizeX, bgSubSizeY;
    public float bgMainPosX, bgMainPosY;
    public float bgSubPosX, bgSubPosY;
    public bool bgMainActive;
    public bool bgSubActive;
    public bool bgIsSwapped;

    // Estado de Musica
    public int musicHash;
    public float musicVolume;

    // Estado de SFX Loop
    public int sfxLoopHash;
    public float sfxLoopVolume;

    // Estado de Variables
    public List<string> counterKeys;
    public List<int> counterValues;
    public List<string> flagKeys;
    public List<bool> flagValues;

    // Estado de VFX Overlay
    public bool vfxActive;
    public int vfxSpriteHash;
    public float vfxAlpha;
    public float vfxSizeX, vfxSizeY;
    public float vfxPosX, vfxPosY;

    // Estado de los 6 Personajes (5 partes * 6 chars = 30 estados)
    public List<SpriteState> spriteStates;

    // Estado de Nieve
    public bool snowActive;
}