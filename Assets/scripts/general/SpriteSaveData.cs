using UnityEngine;

[System.Serializable]
public class SpriteSaveData
{
    public bool isActive;
    public string spritePath;
    public float alpha;
    public int siblingIndex;
    public float posX, posY, posZ;
    
    public float sizeX;
    public float sizeY;

    public bool isSwitchImageActive;
}