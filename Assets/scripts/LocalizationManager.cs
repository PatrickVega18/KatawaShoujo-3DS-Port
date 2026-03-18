using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;
    public string currentLanguage = "ES";

    private Dictionary<string, string> es = new Dictionary<string, string>();
    private Dictionary<string, string> en = new Dictionary<string, string>();
    
    private Dictionary<int, string> sceneCache = new Dictionary<int, string>();
    private Dictionary<int, string> actSceneCache = new Dictionary<int, string>();
    private string cachedAct = "";
    private string cachedLang = "";

    private static readonly string[] mesesES = { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
    private static readonly string[] mesesEN = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitDictionaries();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitDictionaries()
    {
        es["inicio"] = "Inicio";
        es["cargar"] = "Cargar";
        es["guardar"] = "Guardar";
        es["opciones"] = "Opciones";
        es["creditos"] = "Créditos";
        es["volver"] = "Volver";
        es["si"] = "Sí";
        es["no"] = "No";
        es["espanol"] = "Español";
        es["ingles"] = "Inglés";
        es["tiempo_jugado"] = "Tiempo de juego:";
        es["confirmar_carga"] = "¿Está seguro de querer desechar su progreso?";
        es["confirmar_carga_menu"] = "¿Está seguro de querer cargar esta partida?";
        es["confirmar_borrado"] = "¿Está seguro de querer borrar este estado?";
        es["confirmar_sobreescribir"] = "¿Está seguro de querer sobrescribir su estado?";
        es["guardado_ok"] = "Progreso guardado con éxito.";
        es["slot_vacio"] = "Vacío";
        es["crear_nuevo"] = "Crear nuevo estado";
        es["confirmar_menu"] = "¿Está seguro de querer regresar al menú principal?";
        es["continuar"] = "Continuar";
        es["guardado_continuar"] = "Continuar";
        es["menu_principal"] = "Menú Principal";
        es["menu"] = "Menú";
        es["escena_actual"] = "Escena Actual:";
        es["musica_actual"] = "Pista de música actual:";
        es["creditos_texto"] = "Programador y todo eso:";

        en["inicio"] = "Start";
        en["cargar"] = "Load";
        en["guardar"] = "Save";
        en["opciones"] = "Options";
        en["creditos"] = "Credits";
        en["volver"] = "Back";
        en["si"] = "Yes";
        en["no"] = "No";
        en["espanol"] = "Spanish";
        en["ingles"] = "English";
        en["tiempo_jugado"] = "Time played:";
        en["confirmar_carga"] = "Are you sure you want to discard your progress?";
        en["confirmar_carga_menu"] = "Are you sure you want to load this saved game?";
        en["confirmar_borrado"] = "Are you sure you want to delete this save?";
        en["confirmar_sobreescribir"] = "Are you sure you want to overwrite your save?";
        en["guardado_ok"] = "Progress saved successfully.";
        en["slot_vacio"] = "Empty";
        en["crear_nuevo"] = "Create new save";
        en["confirmar_menu"] = "Are you sure you want to return to the main menu?";
        en["continuar"] = "Continue";
        en["guardado_continuar"] = "Continue";
        en["menu_principal"] = "Main Menu";
        en["menu"] = "Menu";
        en["escena_actual"] = "Current scene:";
        en["musica_actual"] = "Current music track:";
        en["creditos_texto"] = "Programmer and all that:";
    }

    public void SetLanguage(string lang)
    {
        if (currentLanguage != lang)
        {
            currentLanguage = lang;
            sceneCache.Clear();
            actSceneCache.Clear();
            cachedAct = "";
            cachedLang = "";
        }
    }

    public string Get(string key)
    {
        Dictionary<string, string> dict = currentLanguage == "EN" ? en : es;
        string val;
        if (dict.TryGetValue(key, out val)) return val;
        return key;
    }

    public string GetSceneName(string actName, int sceneHash, string fallback)
    {
        if (sceneHash == 0) return fallback;

        // Si cambiamos de acto o idioma, invalidamos la caché de lectura de archivos
        if (cachedAct != actName || cachedLang != currentLanguage)
        {
            sceneCache.Clear();
            cachedAct = actName;
            cachedLang = currentLanguage;
        }

        if (actSceneCache.ContainsKey(sceneHash)) return actSceneCache[sceneHash];
        if (sceneCache.ContainsKey(sceneHash)) return sceneCache[sceneHash];

        string fileName = GetActoEscenaFileName(actName);
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path)) return fallback;

        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int hash = br.ReadInt32();
                    
                    if (hash == sceneHash)
                    {
                        string actoName = br.ReadString();
                        string localizedName = br.ReadString();
                        sceneCache[sceneHash] = localizedName;
                        return localizedName;
                    }
                    else
                    {
                        br.ReadString();
                        br.ReadString();
                    }
                }
            }
            // Limpiamos los strings temporales generados tras la búsqueda
            System.GC.Collect();
        }
        catch { }

        return fallback;
    }

    public void LoadActSceneCache(string actName)
    {
        if (string.IsNullOrEmpty(actName)) return;
        
        string fileName = GetActoEscenaFileName(actName);
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path)) return;

        actSceneCache.Clear();
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int hash = br.ReadInt32();
                    br.ReadString(); // actName (not needed in cache)
                    string localizedName = br.ReadString();
                    actSceneCache[hash] = localizedName;
                }
            }
            // Tras cargar el acto entero, forzamos limpieza para que los strings descartados no fragmenten la RAM
            System.GC.Collect();
        }
        catch { }
    }

    public string GetDialogueFileName(string actName)
    {
        return actName + "_dialogos_" + currentLanguage + ".dat";
    }

    public string GetDecisionFileName(string actName)
    {
        return actName + "_decisiones_" + currentLanguage + ".dat";
    }

    public string GetActoEscenaFileName(string actName)
    {
        return actName + "_actoescena_" + currentLanguage + ".dat";
    }

    public string GetLocalizedDate()
    {
        System.DateTime now = System.DateTime.Now;
        string[] months = currentLanguage == "EN" ? mesesEN : mesesES;
        string month = months[now.Month - 1];
        return string.Format("{0} {1:00}, {2:00}:{3:00}", month, now.Day, now.Hour, now.Minute);
    }

    public string GetLocalizedLabel(string key, string lang)
    {
        Dictionary<string, string> dict = lang == "EN" ? en : es;
        string val;
        if (dict.TryGetValue(key, out val)) return val;
        return key;
    }

    public void ClearCaches()
    {
        sceneCache.Clear();
        actSceneCache.Clear();
        cachedAct = "";
        cachedLang = "";
    }
}
