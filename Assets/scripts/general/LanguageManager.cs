//Si esto no funciona, me mato :D

using UnityEngine;

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }

    [SerializeField]
    private string currentLanguageCode = "ES";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    public void SetLanguage(string newLanguageCode)
    {
        currentLanguageCode = newLanguageCode;
        Debug.Log("LanguageManager: Idioma cambiado a " + newLanguageCode);

        if (TranslationManager.Instance != null)
        {
            TranslationManager.Instance.LoadTranslations();
            TranslationManager.Instance.BroadcastTranslationUpdate();
        }
    }

    public string GetCurrentLanguage()
    {
        return currentLanguageCode;
    }
    //FUNCIONÓ ALV
}