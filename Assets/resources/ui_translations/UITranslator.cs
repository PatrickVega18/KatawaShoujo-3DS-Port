using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class UITranslator : MonoBehaviour
{
    [Tooltip("La clave de traducción para este texto (ej: 'menu_start'). Debe coincidir con una clave en los archivos CSV.")]
    public string translationKey;

    private Text textComponent;

    void Start()
    {
        textComponent = GetComponent<Text>();
        Translate();
    }

    public void Translate()
    {
        if (textComponent != null && !string.IsNullOrEmpty(translationKey) && TranslationManager.Instance != null)
        {
            textComponent.text = TranslationManager.Instance.GetText(translationKey);
        }
    }
}