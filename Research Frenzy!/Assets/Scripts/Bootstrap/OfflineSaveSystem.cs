// OfflineSaveSystem - a stand-in save system for when the save API can't be reached
// (most often: the API on localhost:8080 isn't running while you test in the Editor).
// It starts a brand new game on day 1 and doesn't save anything, so the game can still be played and tested.
// GameInitializer only uses it after the real SaveSystem has failed.

using System.Collections.Generic;
using GameDataObjects;
using Interfaces;
using UnityEngine;

namespace Bootstrap
{
    public class OfflineSaveSystem : BaseSaveSystem
    {
        private readonly int seed;

        public OfflineSaveSystem(int seed) : base("offline")
        {
            this.seed = seed;
        }

        // a new game, the same shape SaveSystem returns when the API has no save yet
        public override Awaitable<SaveData> LoadGame()
        {
            SaveData fresh = new SaveData
            {
                Seed = seed,
                Player = new PlayerData { CurrentDay = 1, CurrentMoney = 0, TotalMoneyEarned = 0 }
            };
            return FromResult(fresh);
        }

        public override Awaitable SaveGame(SaveData saveData)
        {
            Debug.Log("OfflineSaveSystem: not saving, the save API wasn't reachable when the game started.");
            return Done();
        }

        public override Awaitable EndGame(PlayerData finalPlayerState) => Done();

        public override Awaitable SaveQuizData(QuizType quizType, Dictionary<int, int> responses) => Done();

        // Awaitable has no "already finished" helper, so these two make one
        private static async Awaitable<T> FromResult<T>(T value)
        {
            await Awaitable.EndOfFrameAsync();
            return value;
        }

        private static async Awaitable Done()
        {
            await Awaitable.EndOfFrameAsync();
        }
    }
}