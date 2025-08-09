using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class NameColorChanger : MonoBehaviour
{
    private Text textComponent;

    void Awake()
    {
        textComponent = GetComponent<Text>();
        if (textComponent == null)
        {
            Debug.LogError("NameColorChanger: No se encontró el componente Text en " + gameObject.name, gameObject);
            this.enabled = false;
        }
    }

    public Color GetColorForCharacter(string characterName)
    {
        if (string.IsNullOrEmpty(characterName))
        {
            return Color.white;
        }

        // Convertimos el nombre a minúsculas para que no importe cómo se escriba.
        string lowerCharacterName = characterName.ToLowerInvariant();

        // Extraemos solo el nombre base si tiene formato especial (ej. "C_Misha-Shizune" -> "shizune")
        if (lowerCharacterName.Contains("-"))
        {
            lowerCharacterName = lowerCharacterName.Split('-')[1];
        }

        switch (lowerCharacterName)
        {
            case "cratobeta":
                Color cratobetaColor;
                ColorUtility.TryParseHtmlString("#5e90fcff", out cratobetaColor);
                return cratobetaColor;

            case "hisao":
                Color hisaoColor;
                ColorUtility.TryParseHtmlString("#629276", out hisaoColor);
                return hisaoColor;

            case "narradora":
                Color narradorColor;
                ColorUtility.TryParseHtmlString("#A9A9A9FF", out narradorColor);
                return narradorColor;

            case "misha":
                Color mishaColor;
                ColorUtility.TryParseHtmlString("#FF809F", out mishaColor);
                return mishaColor;

            case "shizune":
            //Por si acaso, aunque usamos Shizunent XD
            case " ̶S̶h̶i̶z̶u̶n̶e̶":
                Color shizuneColor;
                ColorUtility.TryParseHtmlString("#72ADEE", out shizuneColor);
                return shizuneColor;

            default:
                return Color.white; // Color por defecto si no encontramos el nombre
        }
    }

    public void UpdateCharacterColor(string characterName)
    {
        if (textComponent == null) return;

        // Simplemente llamamos a nuestro nuevo método y aplicamos el color que nos devuelve.
        textComponent.color = GetColorForCharacter(characterName);
    }
}