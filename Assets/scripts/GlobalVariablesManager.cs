using UnityEngine;
using System.Collections.Generic;

public class GlobalVariablesManager : MonoBehaviour
{
    public static GlobalVariablesManager Instance;

    private Dictionary<string, int> counters = new Dictionary<string, int>();
    private Dictionary<string, bool> flags = new Dictionary<string, bool>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ResetState()
    {
        counters.Clear();
        flags.Clear();
    }

    public void ApplyAction(string actionCode)
    {
        if (string.IsNullOrEmpty(actionCode)) return;

        string code = actionCode.Trim().ToLower();
        if (code == "none") return;
        
        if (code.Contains("-plus-"))
        {
            ProcessCounter(code, "-plus-", 1);
        }
        else if (code.Contains("-minus-"))
        {
            ProcessCounter(code, "-minus-", -1);
        }
        else if (code.EndsWith("-on"))
        {
            string flagName = code.Substring(0, code.Length - 3).Trim();
            SetFlag(flagName, true);
        }
        else if (code.EndsWith("-off"))
        {
            string flagName = code.Substring(0, code.Length - 4).Trim();
            SetFlag(flagName, false);
        }
    }

    private void ProcessCounter(string code, string separator, int multiplier)
    {
        string[] parts = code.Split(new string[] { separator }, System.StringSplitOptions.None);
        if (parts.Length < 2) return;

        string varsPart = parts[0];
        string valPart = parts[1];

        int value = 0;
        int.TryParse(valPart, out value);
        value *= multiplier;

        string[] variables = varsPart.Split(new string[] { "-AND-" }, System.StringSplitOptions.None);
        
        foreach (string varName in variables)
        {
            ModifyCounter(varName.Trim(), value);
        }
    }

    public void ModifyCounter(string name, int amount)
    {
        if (!counters.ContainsKey(name)) 
        {
            counters[name] = 0;
        }
        counters[name] += amount;
    }

    public void SetFlag(string name, bool state)
    {
        flags[name] = state;
    }

    public int GetCounter(string name)
    {
        if (counters.ContainsKey(name))
            return counters[name];
        else
            return 0;
    }

    public bool GetFlag(string name)
    {
        if (flags.ContainsKey(name))
            return flags[name];
        else
            return false;
    }

    public List<string> GetCounterKeys() { return new List<string>(counters.Keys); }
    public List<int> GetCounterValues() { return new List<int>(counters.Values); }
    public List<string> GetFlagKeys() { return new List<string>(flags.Keys); }
    public List<bool> GetFlagValues() { return new List<bool>(flags.Values); }

    public bool CheckCondition(string variable, string condition)
    {
        string key = variable.ToLower().Trim();
        string cond = condition.Trim();

        if (cond == "on") 
            return GetFlag(key);
            
        if (cond == "off") 
            return !GetFlag(key);

        int currentVal = GetCounter(key);
        int checkVal = 0;

        if (cond.StartsWith(">="))
        {
            int.TryParse(cond.Substring(2), out checkVal);
            return currentVal >= checkVal;
        }
        else if (cond.StartsWith("<="))
        {
            int.TryParse(cond.Substring(2), out checkVal);
            return currentVal <= checkVal;
        }
        else if (cond.StartsWith(">"))
        {
            int.TryParse(cond.Substring(1), out checkVal);
            return currentVal > checkVal;
        }
        else if (cond.StartsWith("<"))
        {
            int.TryParse(cond.Substring(1), out checkVal);
            return currentVal < checkVal;
        }
        else if (cond.StartsWith("="))
        {
            int.TryParse(cond.Substring(1), out checkVal);
            return currentVal == checkVal;
        }
        else 
        {
            int.TryParse(cond, out checkVal);
            return currentVal == checkVal;
        }
    }
}