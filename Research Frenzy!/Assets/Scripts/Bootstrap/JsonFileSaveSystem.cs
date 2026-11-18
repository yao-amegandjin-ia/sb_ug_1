using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameDataObjects;
using Interfaces;
using Newtonsoft.Json;
using UnityEngine;

namespace Bootstrap
{
    /// <summary>
    /// This is a JSON based alternative saving to be used for the editor debugging! 
    /// This would not work with actual web gl because webgl cannot write to the file system!
    /// </summary>
    public sealed class JsonFileSaveSystem : BaseSaveSystem
    {
        private readonly int initialSeed;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public string DirectoryPath { get; }
        public string SavePath => Path.Combine(DirectoryPath, "save.json");

        public JsonFileSaveSystem(int seed, string directoryPath = null)
            : base("local-json")
        {
            initialSeed = seed;
            DirectoryPath = Path.GetFullPath(directoryPath ??
                Path.Combine(Application.persistentDataPath, "ResearchFrenzyJson"));
            Directory.CreateDirectory(DirectoryPath);
            Debug.Log($"JsonFileSaveSystem: files are stored in {DirectoryPath}");
        }

        public override async Awaitable<SaveData> LoadGame()
        {
            SaveData result;

            if (!File.Exists(SavePath))
            {
                result = new SaveData
                {
                    Seed = initialSeed,
                    Player = new PlayerData { CurrentDay = 1 }
                };
            }
            else
            {
                var saved = ReadSave();
                result = SaveDataMapper.FromApiDto(saved.Data, saved.Seed);
            }

            // Keep the same Awaitable contract as the existing adapters.
            // Small local JSON reads/writes run synchronously on the calling thread.
            await Awaitable.EndOfFrameAsync();
            return result;
        }

        public override async Awaitable SaveGame(SaveData saveData)
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));
            if (saveData.Player == null)
                throw new ArgumentException("Save data must contain a player.", nameof(saveData));

            WriteJson(SavePath, new LocalSave
            {
                Seed = saveData.Seed,
                SavedAtUtc = DateTime.UtcNow,
                Data = SaveDataMapper.ToApiDto(saveData)
            });

            Debug.Log($"JsonFileSaveSystem: saved day {saveData.Player.CurrentDay} to {SavePath}");
            await Awaitable.EndOfFrameAsync();
        }

        public override async Awaitable SaveQuizData(
            QuizType quizType, Dictionary<int, int> responses)
        {
            if (responses == null) throw new ArgumentNullException(nameof(responses));
            if (quizType != QuizType.MidGame && quizType != QuizType.PostGame)
                throw new ArgumentOutOfRangeException(nameof(quizType));

            string name = quizType == QuizType.MidGame ? "mid-game.json" : "post-game.json";
            WriteJson(Path.Combine(DirectoryPath, name), new
            {
                saved_at_utc = DateTime.UtcNow,
                responses = new Dictionary<int, int>(responses)
            });

            await Awaitable.EndOfFrameAsync();
        }

        public override async Awaitable EndGame(PlayerData finalPlayerState)
        {
            ApiPlayerDto finalPlayer = finalPlayerState == null
                ? null : SaveDataMapper.ToApiPlayer(finalPlayerState);

            // Match the API's behavior: a supplied final player replaces the
            // checkpoint's player, preserving its researchers, clients and seed.
            if (finalPlayer != null && File.Exists(SavePath))
            {
                var saved = ReadSave();
                saved.Data.Player = finalPlayer;
                saved.SavedAtUtc = DateTime.UtcNow;
                WriteJson(SavePath, saved);
            }

            WriteJson(Path.Combine(DirectoryPath, "end-game.json"), new
            {
                ended_at_utc = DateTime.UtcNow,
                player = finalPlayer
            });

            await Awaitable.EndOfFrameAsync();
        }

        private LocalSave ReadSave()
        {
            var saved = JsonConvert.DeserializeObject<LocalSave>(
                File.ReadAllText(SavePath, Utf8));

            // Fail visibly instead of silently replacing an invalid save.
            if (saved == null || saved.Version != 1 || saved.Data?.Player == null)
                throw new InvalidDataException($"Invalid or unsupported save: {SavePath}");

            return saved;
        }

        private void WriteJson(string path, object value)
        {
            Directory.CreateDirectory(DirectoryPath);
            string json = JsonConvert.SerializeObject(value, Formatting.Indented);
            File.WriteAllText(path, json, Utf8);
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class LocalSave
        {
            [JsonProperty("version")] public int Version { get; set; } = 1;
            [JsonProperty("seed")] public int Seed { get; set; }
            [JsonProperty("saved_at_utc")] public DateTime SavedAtUtc { get; set; }
            [JsonProperty("data")] public ApiSaveDto Data { get; set; }
        }
    }
}
