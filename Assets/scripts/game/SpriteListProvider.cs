using UnityEngine;
using System.Collections.Generic;

public class SpriteListProvider : MonoBehaviour
{
    [Header("Listas de Sprites por Idioma")]
    public List<Sprite> sprites_ES; 
    public List<Sprite> sprites_EN; 

    public List<Sprite> GetSpriteList(string languageCode)
    {
        switch (languageCode.ToUpper())
        {
            case "ES":
                return sprites_ES;
            case "EN":
                return sprites_EN;
            default:
                Debug.LogWarning("SpriteListProvider: Código de idioma no reconocido '" + languageCode + "'. Usando Español por defecto.");
                return sprites_ES;
        }
    }
}