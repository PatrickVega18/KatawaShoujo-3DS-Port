using UnityEngine;
using System.Collections.Generic;

public class TranslationManager : MonoBehaviour
{
	// --- Singleton ---
	public static TranslationManager Instance { get; private set; }

	private Dictionary<string, string> currentTranslations;

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
			currentTranslations = new Dictionary<string, string>();
			LoadTranslations();
		}
	}

	// Carga las traducciones para el idioma actual
	public void LoadTranslations()
	{
		if (LanguageManager.Instance == null)
		{
			Debug.LogError("TranslationManager: No se puede cargar traducciones porque LanguageManager no existe.");
			return;
		}

		string langCode = LanguageManager.Instance.GetCurrentLanguage();
		string path = "ui_translations/ui_" + langCode;

		TextAsset csvFile = Resources.Load<TextAsset>(path);

		if (csvFile != null)
		{
			currentTranslations.Clear();
			string[] lines = csvFile.text.Split(new char[] { '\n' });

			// Empezamos desde la línea 1 para saltar la cabecera (Key,Text)
			for (int i = 1; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (string.IsNullOrEmpty(line)) continue;

				string[] parts = line.Split(new char[] { ',' }, 2);
				if (parts.Length == 2)
				{
					string key = parts[0];
					// Limpiamos las comillas dobles si existen
					string value = parts[1].Trim().Trim('"');

					if (!currentTranslations.ContainsKey(key))
					{
						currentTranslations.Add(key, value);
					}
				}
			}
			Debug.Log("Traducciones cargadas para el idioma: " + langCode);
		}
		else
		{
			Debug.LogError("TranslationManager: No se encontró el archivo de traducción en la ruta: " + path);
		}
	}

	public string GetText(string key)
	{
		if (currentTranslations.ContainsKey(key))
		{
			return currentTranslations[key];
		}

		Debug.LogWarning("No se encontró traducción para la clave: " + key);
		return "!!!" + key + "!!!"; // Devuelve la clave como error para verlo en pantalla
	}
	
	public void BroadcastTranslationUpdate()
	{
		UITranslator[] translators = FindObjectsOfType<UITranslator>();
		foreach (UITranslator translator in translators)
		{
			translator.Translate();
		}
		Debug.Log("Enviada actualización de traducción a " + translators.Length + " textos.");
	}
}