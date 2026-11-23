// ISaveStorage - WHERE a save's text is kept. LocalSaveSystem decides WHAT goes in the save (the JSON), and hands
// the text to one of these to keep it. Splitting it this way means the same save code works in two places:
//
//   FileSaveStorage    - a .json file on disk (the Editor and desktop builds)
//   BrowserSaveStorage - the browser's localStorage (WebGL builds, where a game can't write files to your computer)
//
// LocalSaveSystem.StorageForThisPlatform() picks the right one. The future API save is a different thing: it
// replaces the whole save system (SaveSystem.cs talks to the server), not just the storage.

namespace Bootstrap
{
    public interface ISaveStorage
    {
        /// <summary>True if something is stored under this name.</summary>
        bool Exists(string name);

        /// <summary>The stored text, or null if there's nothing under this name.</summary>
        string Read(string name);

        /// <summary>Stores the text under this name, replacing what was there. Throws if it can't.</summary>
        void Write(string name, string text);

        /// <summary>Removes it. Does nothing if there's nothing under this name.</summary>
        void Delete(string name);

        /// <summary>Where things are kept, for the Console ("C:/.../LocalSaves", "browser localStorage").</summary>
        string Describe();
    }
}