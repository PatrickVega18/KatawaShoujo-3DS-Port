//Comenzamos con esta wbda

using UnityEngine;
using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class InspectorKeyValuePair
{
    public string Key;
    public string Value;
    public InspectorKeyValuePair(string key, string value) { Key = key; Value = value; }
}

[System.Serializable]
public class CsvEntryForInspector
{
    public string EntryID;
    public List<InspectorKeyValuePair> RowData;
    public CsvEntryForInspector(string id) { EntryID = id; RowData = new List<InspectorKeyValuePair>(); }
}

public class CSVLoader : MonoBehaviour
{
    [Header("Configuración del CSV")]
    public TextAsset mainCsvFile;
    public TextAsset dialogCsvFile;
    public TextAsset eventCsvFile;
    public TextAsset decisionCsvFile;

    [Header("Configuración de Carga Dinámica")]
    public string actIdentifier;

    [Header("Datos Cargados del Guion Principal (Inspector)")]
    public List<CsvEntryForInspector> inspectorGuionData = new List<CsvEntryForInspector>();
    [Header("Datos Cargados de Diálogos (Inspector)")]
    public List<CsvEntryForInspector> inspectorDialogData = new List<CsvEntryForInspector>();
    [Header("Datos Cargados de Eventos (Inspector)")]
    public List<CsvEntryForInspector> inspectorEventData = new List<CsvEntryForInspector>();
    [Header("Datos Cargados de Decisiones (Inspector)")]
    public List<CsvEntryForInspector> inspectorDecisionData = new List<CsvEntryForInspector>();

    private List<Dictionary<string, string>> allGuionEntries = new List<Dictionary<string, string>>();
    private string[] guionHeaders;
    private List<Dictionary<string, string>> allDialogEntries = new List<Dictionary<string, string>>();
    private string[] dialogHeaders;
    private List<Dictionary<string, string>> allEventEntries = new List<Dictionary<string, string>>();
    private string[] eventHeaders;
    private List<Dictionary<string, string>> allDecisionEntries = new List<Dictionary<string, string>>();
    private string[] decisionHeaders;

    // ===== SE EJECUTA AL INICIAR PARA CARGAR Y PARSEAR LOS ARCHIVOS CSV ASIGNADOS =====
    void Awake()
    {
        // Si no hay un identificador de acto, no podemos cargar dinámicamente.
        if (string.IsNullOrEmpty(actIdentifier))
        {
            Debug.LogError("CSVLoader: ¡'Act Identifier' no está asignado en el Inspector! No se pueden cargar los CSV.", this.gameObject);
            this.enabled = false;
            return;
        }

        // Si el LanguageManager no existe, es un error fatal.
        if (LanguageManager.Instance == null)
        {
            Debug.LogError("CSVLoader: ¡No se encontró una instancia de LanguageManager en la escena! Asegúrate de que exista y se cargue primero.", this.gameObject);
            this.enabled = false;
            return;
        }

        // Obtenemos el idioma actual del manager.
        string langCode = LanguageManager.Instance.GetCurrentLanguage();

        // ===== INICIO DE LA NUEVA LÓGICA DE NOMBRADO =====

        // 1. Creamos el prefijo del archivo capitalizando el identificador del acto.
        //    Ej: "acto01" -> "Acto01", "pruebas" -> "Pruebas"
        string fileNamePrefix = char.ToUpper(actIdentifier[0]) + actIdentifier.Substring(1);

        // 2. Construimos los nombres de archivo EXACTOS según tu estructura.
        string guionFileName = fileNamePrefix + "_guion_" + langCode;       // Ej: "Acto01_guion_ES"
        string dialogosFileName = fileNamePrefix + "_dialogos_" + langCode;   // Ej: "Acto01_dialogos_ES"
        string eventosFileName = fileNamePrefix + "_eventos_" + langCode;     // Ej: "Acto01_eventos_ES"
        string decisionesFileName = fileNamePrefix + "_decisiones_" + langCode; // Ej: "Acto01_decisiones_ES"

        // 3. Construimos la ruta base a la carpeta.
        //    Ej: "csv/acto01/ES/"
        string basePath = "csv/" + actIdentifier + "/" + langCode + "/";

        // 4. Cargamos cada archivo usando la ruta y el nombre construidos.
        mainCsvFile = Resources.Load<TextAsset>(basePath + guionFileName);
        dialogCsvFile = Resources.Load<TextAsset>(basePath + dialogosFileName);
        eventCsvFile = Resources.Load<TextAsset>(basePath + eventosFileName);
        decisionCsvFile = Resources.Load<TextAsset>(basePath + decisionesFileName);

        // --- Parseo de los datos cargados ---
        if (mainCsvFile != null)
        {
            ParseCsvData(mainCsvFile, ref guionHeaders, ref allGuionEntries, ref inspectorGuionData, "Guion Principal");
        }
        else
        {
            Debug.LogError("CSVLoader: ¡No se pudo cargar 'mainCsvFile'! Se buscó en: " + basePath + guionFileName, this.gameObject);
        }

        if (dialogCsvFile != null)
        {
            ParseCsvData(dialogCsvFile, ref dialogHeaders, ref allDialogEntries, ref inspectorDialogData, "Diálogos");
        }
        else
        {
            Debug.LogError("CSVLoader: ¡No se pudo cargar 'dialogCsvFile'! Se buscó en: " + basePath + dialogosFileName, this.gameObject);
        }

        if (eventCsvFile != null)
        {
            ParseCsvData(eventCsvFile, ref eventHeaders, ref allEventEntries, ref inspectorEventData, "Eventos");
        }
        else
        {
            Debug.LogError("CSVLoader: ¡No se pudo cargar 'eventCsvFile'! Se buscó en: " + basePath + eventosFileName, this.gameObject);
        }

        if (decisionCsvFile != null)
        {
            ParseCsvData(decisionCsvFile, ref decisionHeaders, ref allDecisionEntries, ref inspectorDecisionData, "Decisiones");
        }
        else
        {
            Debug.LogError("CSVLoader: ¡No se pudo cargar 'decisionCsvFile'! Se buscó en: " + basePath + decisionesFileName, this.gameObject);
        }
    }

    // ===== PARSEA UN ARCHIVO CSV, ALMACENA DATOS INTERNAMENTE Y PARA EL INSPECTOR =====
    private void ParseCsvData(TextAsset fileToParse, ref string[] headersArray,
                                 ref List<Dictionary<string, string>> internalDataList,
                                 ref List<CsvEntryForInspector> inspectorDataList,
                                 string logPrefix)
    {
        internalDataList.Clear();
        inspectorDataList.Clear();
        string csvFullText = fileToParse.text;
        string[] lines = csvFullText.Split(new string[] { "\r\n", "\n", "\r" }, System.StringSplitOptions.None);
        List<string> nonEmptyLines = new List<string>();
        foreach (string line in lines) { if (!string.IsNullOrEmpty(line.Trim())) nonEmptyLines.Add(line); }
        lines = nonEmptyLines.ToArray();

        if (lines.Length <= 1)
        {
            Debug.LogError(logPrefix + " CSVLoader: Archivo vacío o solo cabecera.");
            return;
        }

        headersArray = ParseCsvLine(lines[0].Trim()).ToArray();
        for (int i = 0; i < headersArray.Length; i++) headersArray[i] = headersArray[i].Trim();

        int idColumnIndex = -1; string idHeaderNameForDisplay = "ID Desconocido";
        for (int i = 0; i < headersArray.Length; i++)
        {
            if (headersArray[i].Equals("Id", System.StringComparison.OrdinalIgnoreCase) || headersArray[i].Equals("ID", System.StringComparison.OrdinalIgnoreCase))
            {
                idColumnIndex = i;
                idHeaderNameForDisplay = headersArray[i];
                break;
            }
        }
        if (idColumnIndex == -1)
        {
            Debug.LogWarning(logPrefix + " CSVLoader: Columna 'Id'/'ID' no encontrada.");
        }

        StringBuilder consoleLogBuilder = new StringBuilder();
        consoleLogBuilder.AppendLine("--- Contenido Detallado de " + fileToParse.name + " (Consola) ---");

        for (int i = 1; i < lines.Length; i++)
        {
            string dataLine = lines[i].Trim();
            List<string> valuesList = ParseCsvLine(dataLine);
            string[] values = valuesList.ToArray();

            Dictionary<string, string> currentInternalEntry = new Dictionary<string, string>();
            string currentEntryID = logPrefix + " Fila " + i;
            if (idColumnIndex != -1 && values.Length > idColumnIndex && !string.IsNullOrEmpty(values[idColumnIndex]))
            {
                currentEntryID = values[idColumnIndex];
            }

            CsvEntryForInspector inspectorEntry = new CsvEntryForInspector(currentEntryID);
            consoleLogBuilder.AppendLine(idHeaderNameForDisplay + " " + currentEntryID + ":");

            for (int j = 0; j < headersArray.Length; j++)
            {
                string headerName = headersArray[j];
                string cellValue = (j < values.Length) ? values[j] : "";
                currentInternalEntry[headerName] = cellValue;
                consoleLogBuilder.AppendLine("   " + headerName + ": " + cellValue);
                if (!(idColumnIndex == j && headerName.Equals(idHeaderNameForDisplay, System.StringComparison.OrdinalIgnoreCase)))
                {
                    inspectorEntry.RowData.Add(new InspectorKeyValuePair(headerName, cellValue));
                }
            }
            internalDataList.Add(currentInternalEntry);
            inspectorDataList.Add(inspectorEntry);
            consoleLogBuilder.AppendLine("");
        }
    }

    // ===== PARSEA UNA LÍNEA DE CSV, MANEJANDO CAMPOS ENTRECOMILLADOS Y COMILLAS DOBLES ESCAPADAS =====
    private List<string> ParseCsvLine(string line)
    {
        List<string> fields = new List<string>();
        StringBuilder currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Length = 0;
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }
        fields.Add(currentField.ToString().Trim());
        //Para pruebas nomás:
        /*
        for (int i = 0; i < fields.Count; i++)
        {
            string field = fields[i];
            if (field.Length >= 2 && field[0] == '"' && field[field.Length - 1] == '"')
            {
                fields[i] = field.Substring(1, field.Length - 2).Replace("\"\"", "\"");
            }
        }
        */
        return fields;
    }

    //==== DEVUELVE LOS DATOS CARGADOS DEL ARCHIVO CSV DEL GUION PRINCIPAL (LLAMADO DESDE GAMECONTROLLER) ====
    public List<Dictionary<string, string>> GetMainGuionData() { return allGuionEntries; }

    //==== DEVUELVE LOS DATOS CARGADOS DEL ARCHIVO CSV DE DIÁLOGOS (LLAMADO DESDE GAMECONTROLLER) ====
    public List<Dictionary<string, string>> GetDialogData() { return allDialogEntries; }

    //==== DEVUELVE LOS DATOS CARGADOS DEL ARCHIVO CSV DE EVENTOS (LLAMADO DESDE GAMECONTROLLER) ====  // <--- NUEVO
    public List<Dictionary<string, string>> GetEventData() { return allEventEntries; }

    // ===== Método para que otros scripts obtengan los datos de decisiones =====
    public List<Dictionary<string, string>> GetDecisionData() { return allDecisionEntries; }


    //==== BUSCA Y DEVUELVE UNA ENTRADA ESPECÍFICA POR ID DEL GUION PRINCIPAL (LLAMADO DESDE GAMECONTROLLER) ====
    public Dictionary<string, string> GetGuionEntryByID(string idToFind)
    {
        return FindEntryByID(idToFind, guionHeaders, allGuionEntries);
    }

    //==== BUSCA Y DEVUELVE UNA ENTRADA ESPECÍFICA POR ID DE LOS DIÁLOGOS (LLAMADO DESDE GAMECONTROLLER) ====
    public Dictionary<string, string> GetDialogEntryByID(string idToFind)
    {
        return FindEntryByID(idToFind, dialogHeaders, allDialogEntries);
    }
    //==== BUSCA Y DEVUELVE UNA ENTRADA ESPECÍFICA POR ID DE LOS EVENTOS (LLAMADO DESDE GAMECONTROLLER) ==== // <--- NUEVO
    public Dictionary<string, string> GetEventEntryByID(string idToFind)
    {
        return FindEntryByID(idToFind, eventHeaders, allEventEntries);
    }
    // ===== NUEVO: Método para buscar una decisión específica por su ID =====
    public Dictionary<string, string> GetDecisionEntryByID(string idToFind)
    {
        return FindEntryByID(idToFind, decisionHeaders, allDecisionEntries);
    }

    // ===== FUNCIÓN AUXILIAR INTERNA PARA BUSCAR UNA ENTRADA POR ID EN UNA LISTA DE DATOS =====
    private Dictionary<string, string> FindEntryByID(string idToFind, string[] headers, List<Dictionary<string, string>> dataList)
    {
        if (headers == null || dataList == null) return null;
        string idHeaderKey = null;
        foreach (string h in headers)
        {
            if (h.Equals("Id", System.StringComparison.OrdinalIgnoreCase) || h.Equals("ID", System.StringComparison.OrdinalIgnoreCase))
            {
                idHeaderKey = h; break;
            }
        }
        if (idHeaderKey == null)
        {
            Debug.LogWarning("No se puede buscar por ID, columna 'Id'/'ID' no encontrada en cabeceras."); return null;
        }

        foreach (Dictionary<string, string> entry in dataList)
        {
            if (entry.ContainsKey(idHeaderKey) && entry[idHeaderKey].Equals(idToFind, System.StringComparison.OrdinalIgnoreCase)) return entry;
        }
        return null;
    }
    //Me da pereza comentar xd
    //¿Qué haces revisando los scripts? x2 :) 
}