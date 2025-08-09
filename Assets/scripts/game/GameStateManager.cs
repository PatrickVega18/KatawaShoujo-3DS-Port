using UnityEngine;
using System.Collections.Generic;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }
    public float PlaytimeInSeconds { get; private set; }

    private Dictionary<string, int> _sumVariables = new Dictionary<string, int>();
    private Dictionary<string, bool> _flagVariables = new Dictionary<string, bool>();

    private void Awake()
    {
        // Lógica del Singleton: Asegurarse de que solo haya una instancia
        if (Instance != null && Instance != this)
        {
            // Si ya existe una instancia, destruimos este objeto para evitar duplicados
            Destroy(this.gameObject);
        }
        else
        {
            // Si no existe, esta se convierte en la instancia única
            Instance = this;
            // Hacemos que no se destruya al cambiar de escena, para mantener los datos
            DontDestroyOnLoad(this.gameObject);
        }
    }

    private void Update()
    {
        PlaytimeInSeconds += Time.deltaTime;
    }

    public void AddToSum(string key, int valueToAdd)
    {
        if (!_sumVariables.ContainsKey(key))
        {
            _sumVariables[key] = 0;
        }
        _sumVariables[key] += valueToAdd;
        Debug.Log("GameStateManager: SUM '" + key + "' actualizado a " + _sumVariables[key]);
    }

    public int GetSumValue(string key)
    {
        if (_sumVariables.ContainsKey(key))
        {
            return _sumVariables[key];
        }
        return 0; // Devolvemos 0 por defecto si la variable nunca se ha usado
    }


    // --- MÉTODOS PARA MANEJAR "FLAGS" (variables booleanas) ---
    public void SetFlag(string key, bool value)
    {
        _flagVariables[key] = value;
        Debug.Log("GameStateManager: FLAG '" + key + "' establecida a " + value);
    }

    public bool GetFlag(string key)
    {
        if (_flagVariables.ContainsKey(key))
        {
            return _flagVariables[key];
        }
        return false; // Por defecto, una flag no activada es 'false'
    }

    public Dictionary<string, int> GetAllSums()
    {
        return new Dictionary<string, int>(_sumVariables);
    }

    public Dictionary<string, bool> GetAllFlags()
    {
        return new Dictionary<string, bool>(_flagVariables);
    }
    public void RestoreState(Dictionary<string, int> loadedSums, Dictionary<string, bool> loadedFlags, float loadedPlaytime)
    {
        _sumVariables = new Dictionary<string, int>(loadedSums);
        _flagVariables = new Dictionary<string, bool>(loadedFlags);
        PlaytimeInSeconds = loadedPlaytime;
        Debug.Log("GameStateManager: Estado restaurado, Tiempo de juego: " + PlaytimeInSeconds);
    }
}