// MethodLibrary - the one list of research methods the whole game uses (codex, match screen, hire cards,
// researcher generator, day summary). It comes from Assets/Resources/Config/methodologies.csv, which is packed
// into the build. DataManager loads it at startup; if anything asks earlier, it loads itself the first time.
// The IDs are the numbers in the ID column of methodologies.csv.

using System;
using System.Collections.Generic;
using Config;

namespace GameDataObjects
{
    [Serializable]
    public class MethodEntry
    {
        public int id;
        public string name;
        public string description;
        public List<string> aliases = new List<string>();   // the "Also Called" column
    }

    public static class MethodLibrary
    {
        /// <summary>The ID used when no methodology was picked (e.g. a rejected request).</summary>
        public const int NoneId = 0;

        static List<MethodEntry> methods;   // null until loaded

        /// <summary>Every methodology, sorted by ID.</summary>
        public static List<MethodEntry> GetAll()
        {
            if (methods == null)
                LoadFromConfigFolder(null);
            return methods;
        }

        /// <summary>The IDs of every methodology, e.g. for the ResearcherGenerator.</summary>
        public static List<int> GetAllIds()
        {
            List<int> ids = new List<int>();
            foreach (MethodEntry m in GetAll()) ids.Add(m.id);
            return ids;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// (Re)reads methodologies.csv. Problems go into the report if one is given, otherwise they're logged.
        /// </summary>
        public static void LoadFromConfigFolder(ConfigReport report)
        {
            bool logHere = report == null;
            if (logHere) report = new ConfigReport();

            string text = ConfigFiles.Read(MethodologyConfig.FileName, report);
            Set(text != null ? MethodologyConfig.Parse(text, report) : new List<MethodEntry>());

            if (logHere) ConfigFiles.Log(report);
        }

        /// <summary>Replaces the list directly (for tests, or loading from somewhere else).</summary>
        public static void Set(List<MethodEntry> newMethods)
        {
            methods = newMethods ?? new List<MethodEntry>();
        }

        /// <summary>The whole entry (name + description), or null.</summary>
        public static MethodEntry Get(int id)
        {
            foreach (MethodEntry m in GetAll())
            {
                if (m.id == id)
                    return m;
            }
            return null;
        }

        public static string GetName(int id)
        {
            MethodEntry m = Get(id);
            return m != null ? m.name : "Unknown";
        }

        /// <summary>Finds a methodology by name, other name or ID number, the same forgiving way requests.csv is read.</summary>
        public static MethodEntry Find(string nameOrId)
        {
            return MethodologyConfig.Find(GetAll(), nameOrId);
        }
    }
}