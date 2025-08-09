using UnityEngine;

[System.Serializable]
public class BackgroundSaveData
{
    public bool rolesEstanInvertidos;
    public int principalSiblingIndex;
    public int secundarioSiblingIndex;
    
    // --- Estado para Fondo Principal ---
    public bool principalIsActive;
    public string principalPath;
    public float principalAlpha;
    public float principalPosX, principalPosY, principalPosZ;
    public float principalSizeX, principalSizeY;

    // --- Estado para Fondo Secundario ---
    public bool secundarioIsActive;
    public string secundarioPath;
    public float secundarioPosX, secundarioPosY, secundarioPosZ;
    public float secundarioSizeX, secundarioSizeY;
}