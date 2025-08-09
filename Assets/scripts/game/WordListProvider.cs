using UnityEngine;
using System.Collections.Generic;

public class WordListProvider : MonoBehaviour
{
    [Header("Listas de Palabras por Idioma")]
    public List<string> words_ES; // Español
    public List<string> words_EN; // Inglés

    public List<string> GetWordList(string languageCode)
    {
        switch (languageCode.ToUpper())
        {
            case "ES":
                return words_ES;
            case "EN":
                return words_EN;
            // case "FR":
            //     return words_FR;
            default:
                Debug.LogWarning("WordListProvider: Código de idioma no reconocido '" + languageCode + "'. Usando Español por defecto.");
                return words_ES; // Devolvemos una lista por defecto para evitar errores
        }
    }
}