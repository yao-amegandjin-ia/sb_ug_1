// ConfigFiles - where the config spreadsheets live and how they're read.
// The only config script that touches Unity (Resources, Debug). The parsers stay plain C#.
//
// Folder: Assets/Resources/Config/
// Unity imports a .csv file as a TextAsset, and everything in a folder named Resources is packed into the build.
// So the spreadsheets travel inside the game itself (including WebGL) and are read instantly with Resources.Load:
// no download, no waiting. The trade-off: after editing a spreadsheet, the game has to be built again.

using UnityEngine;

namespace Config
{
    public static class ConfigFiles
    {
        /// <summary>The folder inside Assets/Resources that holds the spreadsheets.</summary>
        public const string ResourcesFolder = "Config";

        /// <summary>Where to find the files in the project, for messages.</summary>
        public static string Folder => "Assets/Resources/" + ResourcesFolder;

        /// <summary>
        /// The whole file as text, or null (with a report entry) if it isn't there.
        /// Resources.Load takes the path inside Resources WITHOUT the extension: "Config/requests", not "requests.csv".
        /// </summary>
        public static string Read(string fileName, ConfigReport report)
        {
            string path = ResourcesFolder + "/" + System.IO.Path.GetFileNameWithoutExtension(fileName);
            TextAsset file = Resources.Load<TextAsset>(path);
            if (file == null)
            {
                report.Add(fileName, $"not found. It should be at {Folder}/{fileName}");
                return null;
            }
            return file.text;   // CsvTable removes Excel's invisible BOM marker if it's still at the start
        }

        /// <summary>Prints the report: one warning listing every problem, or nothing if there were none.</summary>
        public static void Log(ConfigReport report)
        {
            if (report == null || !report.HasProblems) return;
            Debug.LogWarning($"Config files: {report.Problems.Count} thing(s) to fix in {Folder}\n{report}");
        }
    }
}
