using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Globalization;

[System.Serializable]
public class SaveIndex
{
    public List<SaveSlotData> allSlotsData = new List<SaveSlotData>();
}

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }
    private static NovelaSaveData dataToLoadAfterSceneChange = null;
    public bool IsLoadingGame { get; private set; }

    public const int MAX_SAVES = 15;
    private List<SaveSlotData> saveSlots;
    private string indexFileName = "save_index.bin";

    private string savePath;

    private Camera upperCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            IsLoadingGame = false;
            LoadIndex();
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public List<SaveSlotData> GetSaveSlots()
    {
        return saveSlots;
    }

    public IEnumerator SaveGame(int slotIndex)
    {
        Debug.Log("Iniciando guardado (con ruta de fondo) en el slot " + slotIndex);

        MenuController mc_ui = FindObjectOfType<MenuController>();
        if (mc_ui != null && mc_ui.mainContainerCanvasGroup != null)
        {
            mc_ui.mainContainerCanvasGroup.alpha = 0f;
        }

        // Solo esperamos un frame para asegurar que todo esté actualizado.
        yield return new WaitForEndOfFrame();

        if (mc_ui != null && mc_ui.mainContainerCanvasGroup != null)
        {
            mc_ui.mainContainerCanvasGroup.alpha = 1f;
        }

        GameController gc = FindObjectOfType<GameController>();
        MusicController mc = FindObjectOfType<MusicController>();
        SFXController sfxC = FindObjectOfType<SFXController>();
        BackgroundController bc = FindObjectOfType<BackgroundController>();
        SpritesController sc = FindObjectOfType<SpritesController>();
        FacesController fc = FindObjectOfType<FacesController>();
        HeadsController hc = FindObjectOfType<HeadsController>();
        LeftArmController lac = FindObjectOfType<LeftArmController>();
        RightArmController rac = FindObjectOfType<RightArmController>();
        if (gc == null || bc == null) 
        { 
            Debug.LogError("SaveLoadManager: No se encontró GameController o BackgroundController. Guardado cancelado.");
            yield break; 
        }

        NovelaSaveData data = new NovelaSaveData();
        data.sceneName = SceneManager.GetActiveScene().name;
        data.currentNodeID = gc.currentNodeID;
        data.sceneDescriptionNodeID = gc.sceneDescriptionNodeID;

        if (GameStateManager.Instance != null)
        {
            data.playtimeInSeconds = GameStateManager.Instance.PlaytimeInSeconds;
            data.sumVariables = GameStateManager.Instance.GetAllSums();
            data.flagVariables = GameStateManager.Instance.GetAllFlags();
        }
        if (mc != null) data.musicState = mc.GetCurrentState();
        if (sfxC != null) data.sfxState = sfxC.GetCurrentState(); 
        if (bc != null) data.backgroundState = bc.GetCurrentState();

        if (sc != null) 
        {
            data.spriteStates = sc.GetCurrentStates();
        }
        if (fc != null)
        {
            data.faceStates = fc.GetCurrentStates();
        }
        if (hc != null) 
        {
            data.headStates = hc.GetCurrentStates();
        }
        if (lac != null)
        {
            data.leftArmStates = lac.GetCurrentStates();
        }
        if (rac != null)
        {
            data.rightArmStates = rac.GetCurrentStates();
        }
        //Complejidad ciclomática? qué es eso? Xd
        BinaryFormatter formatter = new BinaryFormatter();
        string path = GetSavePath(slotIndex);
        FileStream file = File.Create(path);
        formatter.Serialize(file, data);
        file.Close();

        SaveSlotData slotData = new SaveSlotData();
        slotData.isSlotInUse = true;
        slotData.playtimeInSeconds = GameStateManager.Instance.PlaytimeInSeconds;
        slotData.realWorldDateTime = DateTime.Now.ToString("MMM dd, HH:mm", CultureInfo.InvariantCulture);

        slotData.sceneDescription = gc.currentSceneDescription;

        if (bc.GetCurrentState().principalIsActive)
        {
            slotData.backgroundPath = bc.GetCurrentState().principalPath;
        }
        else if (bc.GetCurrentState().secundarioIsActive)
        {
            slotData.backgroundPath = bc.GetCurrentState().secundarioPath;
        }
        else
        {
            slotData.backgroundPath = null;
        }

        saveSlots[slotIndex] = slotData;
        SaveIndex();

        Debug.Log("Juego guardado (con ruta de fondo) correctamente en el slot " + slotIndex);
    }


    public void LoadGame(int slotIndex)
    {
        if (!saveSlots[slotIndex].isSlotInUse)
        {
            Debug.LogWarning("Se intentó cargar un slot vacío: " + slotIndex);
            return;
        }

        string path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            Debug.Log("Cargando partida desde el slot " + slotIndex);
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            NovelaSaveData data = (NovelaSaveData)formatter.Deserialize(file);
            file.Close();

            string currentSceneName = SceneManager.GetActiveScene().name;
            if (data.sceneName != null && data.sceneName != currentSceneName)
            {
                Debug.Log("Escena guardada ("+ data.sceneName +") diferente a la actual ("+ currentSceneName +"). Cargando escena...");
                IsLoadingGame = true;
                dataToLoadAfterSceneChange = data;
                SceneManager.LoadScene(data.sceneName);
            }
            else
            {
                StartCoroutine(RestoreStateAfterFrame(data));
            }
        }
        else
        {
            Debug.LogError("Error: El índice dice que el slot " + slotIndex + " está en uso, pero no se encontró el archivo: " + path);
        }
    }
    public void DeleteSaveSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= saveSlots.Count)
        {
            Debug.LogError("Se intentó borrar un slot con un índice inválido: " + slotIndex);
            return;
        }

        saveSlots[slotIndex] = new SaveSlotData(); // Lo reemplazamos por uno nuevo y vacío.

        SaveIndex();

        //Borramos el archivo de guardado completo de ese slot.
        string path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Archivo de guardado borrado: " + path);
        }
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (dataToLoadAfterSceneChange != null)
        {
            StartCoroutine(RestoreStateAfterFrame(dataToLoadAfterSceneChange));
            dataToLoadAfterSceneChange = null;
        }
    }

    private IEnumerator RestoreStateAfterFrame(NovelaSaveData data)
    {
        yield return new WaitForEndOfFrame();
        Debug.Log("Aplicando estado guardado...");
        GameController gc = FindObjectOfType<GameController>();
        if (gc != null)
        {
            yield return StartCoroutine(gc.RestoreGameState(data));
        }
        IsLoadingGame = false;
        Debug.Log("Proceso de carga finalizado.");
    }

    private void LoadIndex()
    {
        string path = GetIndexFilePath();
        if (File.Exists(path))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            SaveIndex indexData = (SaveIndex)formatter.Deserialize(file);
            file.Close();
            saveSlots = indexData.allSlotsData;
            Debug.Log("Índice de guardado cargado. " + saveSlots.Count + " slots encontrados.");
        }
        else
        {
            Debug.Log("No se encontró archivo de índice. Creando uno nuevo.");
            saveSlots = new List<SaveSlotData>();
            for (int i = 0; i < MAX_SAVES; i++)
            {
                saveSlots.Add(new SaveSlotData());
            }
        }
    }

    private void SaveIndex()
    {
        SaveIndex indexData = new SaveIndex();
        indexData.allSlotsData = saveSlots;
        BinaryFormatter formatter = new BinaryFormatter();
        string path = GetIndexFilePath();
        FileStream file = File.Create(path);
        formatter.Serialize(file, indexData);
        file.Close();
        Debug.Log("Índice de guardado actualizado.");
    }
    //Sacado directamente del ejemplo de Unity para 3DS =w=
    private string GetIndexFilePath()
    {
    #if UNITY_EDITOR
        return Path.Combine(Application.persistentDataPath, indexFileName);
    #elif UNITY_N3DS
        return "data:/" + indexFileName;
    #else
        return Path.Combine(Application.persistentDataPath, indexFileName);
    #endif
    }

    private string GetSavePath(int slotIndex)
    {
        string fileName = "savefile_" + slotIndex + ".bin";
    #if UNITY_EDITOR
        return Path.Combine(Application.persistentDataPath, fileName);
    #elif UNITY_N3DS
        return "data:/" + fileName;
    #else
        return Path.Combine(Application.persistentDataPath, fileName);
    #endif
    }
}