using System;

[System.Serializable]
public class SaveSlotData
{
    public bool isSlotInUse = false;

    // --- Datos que mostraremos en la UI ---
    public string sceneDescription; 
    public string realWorldDateTime;
    public float playtimeInSeconds;

    // --- Almacenaremos la ruta del fondo en lugar de la captura ---
    public string backgroundPath; 
}