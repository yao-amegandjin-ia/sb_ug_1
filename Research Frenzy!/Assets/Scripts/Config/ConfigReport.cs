// ConfigReport - collects the problems found while reading the config files, in plain words,
// with the file name and spreadsheet row, so whoever wrote the file can find and fix them.
// The loaders only add to this. DataManager prints it to the Console once everything is loaded.

using System.Collections.Generic;
using System.Text;

namespace Config
{
    public class ConfigReport
    {
        private readonly List<string> problems = new List<string>();

        public IReadOnlyList<string> Problems => problems;
        public bool HasProblems => problems.Count > 0;

        /// <summary>A problem with one row, e.g. "requests.csv, row 12: ..."</summary>
        public void Add(string fileName, int row, string message)
        {
            problems.Add(row > 0 ? $"{fileName}, row {row}: {message}" : $"{fileName}: {message}");
        }

        /// <summary>A problem with the whole file (missing file, missing column...).</summary>
        public void Add(string fileName, string message)
        {
            Add(fileName, -1, message);
        }

        /// <summary>Everything in one block of text, one problem per line.</summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string p in problems)
                sb.AppendLine("  - " + p);
            return sb.ToString();
        }
    }
}