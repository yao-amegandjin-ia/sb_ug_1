// MethodologyConfig - turns methodologies.csv into the list of MethodEntry the game uses.
//
// Columns (any order, extra columns are ignored):
//   ID          a whole number, 1 or more, never reused. Researchers and saves remember methods by this number.
//   Name        what the player sees, e.g. "Field Research"
//   Description shown on the match screen and in the codex
//   Also Called optional other names the requests file may use, e.g. "RCT; Randomized Controlled Trial"
//
// Adding a methodology = adding a row. No code changes.

using System.Collections.Generic;

namespace Config
{
    public static class MethodologyConfig
    {
        public const string FileName = "ResearchFrenzyMethodologies.csv";

        static readonly string[] IdColumn = { "ID", "Number", "Method ID" };
        static readonly string[] NameColumn = { "Name", "Method", "Methodology" };
        static readonly string[] DescriptionColumn = { "Description", "Explanation", "Info" };
        static readonly string[] AliasColumn = { "Also Called", "Other Names", "Aliases", "AKA" };

        /// <summary>Reads the text of methodologies.csv. Rows with problems are skipped and listed in the report.</summary>
        public static List<MethodEntry> Parse(string csvText, ConfigReport report)
        {
            List<MethodEntry> methods = new List<MethodEntry>();
            CsvTable table = CsvTable.Parse(csvText, FileName);

            if (!table.HasColumn(IdColumn) || !table.HasColumn(NameColumn))
            {
                report.Add(FileName, "the first row must have an \"ID\" column and a \"Name\" column.");
                return methods;
            }

            HashSet<int> usedIds = new HashSet<int>();
            HashSet<string> usedNames = new HashSet<string>();

            for (int r = 0; r < table.RowCount; r++)
            {
                int row = table.SpreadsheetRow(r);
                string idText = table.Get(r, IdColumn);
                string name = table.Get(r, NameColumn);

                if (!int.TryParse(idText, out int id) || id <= 0)
                {
                    report.Add(FileName, row, $"ID \"{idText}\" must be a whole number of 1 or more. Row skipped.");
                    continue;
                }
                if (!usedIds.Add(id))
                {
                    report.Add(FileName, row, $"ID {id} is already used by another methodology. Row skipped.");
                    continue;
                }
                if (name.Length == 0)
                {
                    report.Add(FileName, row, $"methodology {id} has no name. Row skipped.");
                    continue;
                }
                if (!usedNames.Add(CsvTable.Normalize(name)))
                    report.Add(FileName, row, $"the name \"{name}\" is used twice. Requests will only match the first one.");

                string description = table.Get(r, DescriptionColumn);
                if (description.Length == 0)
                    report.Add(FileName, row, $"\"{name}\" has no description (it still loads).");

                methods.Add(new MethodEntry
                {
                    id = id,
                    name = name,
                    description = description,
                    aliases = CsvTable.SplitList(table.Get(r, AliasColumn))
                });
            }

            if (methods.Count == 0)
                report.Add(FileName, "no methodologies were loaded.");

            // sorted by ID so lists in the UI don't depend on row order
            methods.Sort((a, b) => a.id.CompareTo(b.id));
            return methods;
        }

        /// <summary>
        /// Finds a methodology from what someone typed in the requests file. Forgiving on purpose:
        /// "Field Research", "field research", "Field" and "FieldResearch" all find Field Research,
        /// "RCT" works if it's in the Also Called column, and the ID number ("4") works too.
        /// Returns null if nothing matches.
        /// </summary>
        public static MethodEntry Find(IEnumerable<MethodEntry> methods, string typed)
        {
            string key = CsvTable.Normalize(typed);
            if (key.Length == 0 || methods == null) return null;

            bool isNumber = int.TryParse(typed.Trim(), out int id);
            string shortKey = WithoutResearchWord(key);
            MethodEntry looseMatch = null;

            foreach (MethodEntry m in methods)
            {
                if (isNumber && m.id == id) return m;
                if (CsvTable.Normalize(m.name) == key) return m;
                if (m.aliases != null)
                    foreach (string alias in m.aliases)
                        if (CsvTable.Normalize(alias) == key) return m;

                // "Field" for "Field Research", or "Systematic Review" for "Systematic Reviews"
                if (looseMatch == null && WithoutResearchWord(CsvTable.Normalize(m.name)) == shortKey)
                    looseMatch = m;
            }
            return looseMatch;
        }

        // "fieldresearch" -> "field", "systematicreviews" -> "systematicreview"
        private static string WithoutResearchWord(string normalized)
        {
            string s = normalized;
            if (s.EndsWith("research") && s.Length > "research".Length) s = s.Substring(0, s.Length - "research".Length);
            if (s.EndsWith("s") && s.Length > 1) s = s.Substring(0, s.Length - 1);
            return s;
        }
    }
}
