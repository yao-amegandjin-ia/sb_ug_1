// LocalSaveSystem - saves the game on the player's own device, as JSON, until the API save is ready.
//   Editor:          Assets/LocalSaves/save.json   (easy to open and read while testing)
//   Desktop build:   <persistentDataPath>/Saves/save.json
//   WebGL build:     the browser's localStorage, key "ResearchFrenzy/save.json"
// WHERE it goes is the ISaveStorage (FileSaveStorage or BrowserSaveStorage). This class only turns the game into
// JSON and back, in the SAME format the API uses (ApiSaveDto + SaveDataMapper), so switching to the API later
// is a one-line change in GameInitializer (Save Backend -> Api) and the save data itself doesn't change.
//
// One save at a time: there's a single "save.json", and every save replaces it.
// Based on JsonFileSaveSystem (which stays in the project, unused): same file format, but it can also use the browser,
// it can delete the save, and a broken save is put aside instead of stopping the game from starting.

using System;
using System.Collections.Generic;
using System.IO;
using GameDataObjects;
using Interfaces;
using Newtonsoft.Json;
using UnityEngine;

namespace Bootstrap
{
    public sealed class LocalSaveSystem : BaseSaveSystem
    {
        private const string SaveName = "save.json";
        private const int SaveVersion = 1;   // bump if the format changes in a way old saves can't be read

        private readonly int initialSeed;
        private readonly ISaveStorage storage;

        /// <param name="seed">Seed for a brand new game, used when there's no save yet.</param>
        /// <param name="storage">Where to keep it. Usually LocalSaveSystem.StorageForThisPlatform().</param>
        public LocalSaveSystem(int seed, ISaveStorage storage) : base("local")
        {
            initialSeed = seed;
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Debug.Log($"LocalSaveSystem: saves are kept in {storage.Describe()}");
        }

        /// <summary>The right storage for where the game is running (see the top of this file).</summary>
        public static ISaveStorage StorageForThisPlatform()
        {
            // #if picks ONE of these when Unity builds the game, so a WebGL build never contains the file version
#if UNITY_WEBGL && !UNITY_EDITOR
            return new BrowserSaveStorage("ResearchFrenzy");
#elif UNITY_EDITOR
            return new FileSaveStorage(Path.Combine(Application.dataPath, "LocalSaves"));
#else
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));
#endif
        }

        public string Describe() => storage.Describe();

        // These methods do all their work straight away (no web request to wait for), but they still return Awaitable
        // so they fit the same ISaveSystem as the API version. "async without await" makes the compiler warn (CS1998),
        // and that warning is switched off for this file only.
#pragma warning disable CS1998

        public override async Awaitable<SaveData> LoadGame()
        {
            LocalSave saved = ReadSave();
            if (saved == null)
            {
                // no save: a new game on day 1 (IsFromSave stays false, so the start screen has no CONTINUE)
                return new SaveData
                {
                    Seed = initialSeed,
                    Player = new PlayerData { CurrentDay = 1 }
                };
            }

            Debug.Log($"LocalSaveSystem: loaded the save from after day {saved.Data.Player.CurrentDay} (saved {saved.SavedAtUtc:u}).");
            return SaveDataMapper.FromApiDto(saved.Data, saved.Seed);   // sets IsFromSave
        }

        public override async Awaitable SaveGame(SaveData saveData)
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));
            if (saveData.Player == null)
                throw new ArgumentException("Save data must contain a player.", nameof(saveData));

            storage.Write(SaveName, ToJson(new LocalSave
            {
                Version = SaveVersion,
                Seed = saveData.Seed,
                SavedAtUtc = DateTime.UtcNow,
                Data = SaveDataMapper.ToApiDto(saveData)
            }));
            Debug.Log($"LocalSaveSystem: saved after day {saveData.Player.CurrentDay} to {storage.Describe()}");
        }

        public override async Awaitable DeleteSave()
        {
            storage.Delete(SaveName);
            Debug.Log("LocalSaveSystem: save deleted.");
        }

        // kept the same as JsonFileSaveSystem, in case anything calls them. Each quiz gets its own file / key
        public override async Awaitable SaveQuizData(QuizType quizType, Dictionary<int, int> responses)
        {
            if (responses == null) throw new ArgumentNullException(nameof(responses));
            string name = quizType == QuizType.MidGame ? "mid-game.json" : "post-game.json";
            storage.Write(name, ToJson(new
            {
                saved_at_utc = DateTime.UtcNow,
                responses = new Dictionary<int, int>(responses)
            }));
        }

        public override async Awaitable EndGame(PlayerData finalPlayerState)
        {
            ApiPlayerDto finalPlayer = finalPlayerState == null ? null : SaveDataMapper.ToApiPlayer(finalPlayerState);
            storage.Write("end-game.json", ToJson(new
            {
                ended_at_utc = DateTime.UtcNow,
                player = finalPlayer
            }));
        }

#pragma warning restore CS1998

        // null when there's no save. A save that can't be read is renamed to save.broken.json (so it isn't lost,
        // and can be looked at) and the game starts fresh, instead of the whole game failing to start
        private LocalSave ReadSave()
        {
            string json = storage.Read(SaveName);
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                LocalSave saved = JsonConvert.DeserializeObject<LocalSave>(json);
                if (saved == null || saved.Version != SaveVersion || saved.Data?.Player == null)
                    throw new InvalidDataException("missing data or an unknown version");
                return saved;
            }
            catch (Exception e)
            {
                Debug.LogError($"LocalSaveSystem: the save couldn't be read ({e.Message}). " +
                               "It was renamed to save.broken.json and a new game starts.");
                storage.Write("save.broken.json", json);
                storage.Delete(SaveName);
                return null;
            }
        }

        private static string ToJson(object value) => JsonConvert.SerializeObject(value, Formatting.Indented);

        // the whole file: a little header (version, seed, when) around the same data the API gets
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class LocalSave
        {
            [JsonProperty("version")] public int Version { get; set; } = SaveVersion;
            [JsonProperty("seed")] public int Seed { get; set; }
            [JsonProperty("saved_at_utc")] public DateTime SavedAtUtc { get; set; }
            [JsonProperty("data")] public ApiSaveDto Data { get; set; }
        }
    }
}
