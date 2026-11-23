// FileSaveStorage - keeps saves as files in one folder. Used in the Editor and desktop builds.
// NOT for WebGL: a browser game can't write to the player's disk (BrowserSaveStorage does WebGL).

using System.IO;
using System.Text;
using UnityEngine;

namespace Bootstrap
{
    public sealed class FileSaveStorage : ISaveStorage
    {
        // UTF-8 without the invisible "BOM" mark at the start, so the files open cleanly in any editor
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public string Folder { get; }

        public FileSaveStorage(string folder)
        {
            Folder = Path.GetFullPath(folder);
            Directory.CreateDirectory(Folder);
        }

        public bool Exists(string name) => File.Exists(PathFor(name));

        public string Read(string name)
        {
            string path = PathFor(name);
            return File.Exists(path) ? File.ReadAllText(path, Utf8) : null;
        }

        public void Write(string name, string text)
        {
            Directory.CreateDirectory(Folder);
            string path = PathFor(name);

            // write to a temporary file first, then swap it in. If the game is closed halfway through writing,
            // the old save is still there in one piece instead of half a file
            string temp = path + ".tmp";
            File.WriteAllText(temp, text, Utf8);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public void Delete(string name)
        {
            string path = PathFor(name);
            if (File.Exists(path)) File.Delete(path);
        }

        public string Describe() => Folder;

        private string PathFor(string name) => Path.Combine(Folder, name);
    }
}