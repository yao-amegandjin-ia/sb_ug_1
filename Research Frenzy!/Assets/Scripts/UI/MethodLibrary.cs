// MethodLibrary - loads the list of research methods from Resources/methodologies.json
// so the codex, start screen and manage firm all use the same names.
// ids follow MethodologiesEnum (1 = Basic ... 13 = Longitudinal).

using System;
using System.Collections.Generic;
using UnityEngine;


//TODO: May need to change and make more efficient
[Serializable]
public class MethodEntry
{
    public int id;
    public string name;
    public string description;
}

[Serializable]
public class MethodEntryList
{
    public List<MethodEntry> methods;
}

public static class MethodLibrary
{
    static List<MethodEntry> methods;

    // ReSharper disable Unity.PerformanceAnalysis
    public static List<MethodEntry> GetAll()
    {
        if (methods == null)
        {
            TextAsset file = Resources.Load<TextAsset>("methodologies");
            if (!file)
            {
                Debug.LogError("methodologies.json not found in Resources");
                methods = new List<MethodEntry>();
            }
            else
            {
                methods = JsonUtility.FromJson<MethodEntryList>(file.text).methods;
            }
        }
        return methods;
    }

    public static string GetName(int id)
    {
        foreach (MethodEntry m in GetAll())
        {
            if (m.id == id)
                return m.name;
        }
        return "Unknown";
    }
    
    // the whole entry (name + description), or null
    public static MethodEntry Get(int id)
    {
        foreach (MethodEntry m in GetAll())
        {
            if (m.id == id)
                return m;
        }
        return null;
    }
}
