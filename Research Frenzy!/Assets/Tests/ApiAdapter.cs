using Assets.Scripts.Bootstrap;
using Assets.Scripts.GameDataObjects;
using Assets.Scripts.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// Play Mode integration tests. Requires the Spring Boot API and an isolated test database.
// Assumes DTOs have been aligned with the Postman API contract.
public class SaveSystemIntegrationTests
{
    private const string BaseUrl = "http://localhost:8080";
    private SaveSystem adapter;
    private JObject collection;
    private string sessionId;

    [SetUp]
    public void SetUp()
    {
        TextAsset asset = Resources.Load<TextAsset>("ResearchFrenzyPostman");
        Assert.That(asset, Is.Not.Null,
            "Copy the Postman collection to Assets/Resources/ResearchFrenzyPostman.json");
        collection = JObject.Parse(asset.text);
        adapter = new SaveSystem(BaseUrl, null, 0);
    }

    [Test]
    public async Task SaveAndLoad_DayOne_RoundTripsAllEntities()
    {
        await StartNewSession();
        JObject fixture = Fixture("4. Save day 1");

        await adapter.SaveGame(ToSaveData(fixture));
        SaveData loaded = await adapter.LoadGame();
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.Researchers, Has.Count.EqualTo(10));
        Assert.That(loaded.Clients, Has.Count.EqualTo(3));

        AssertContainsFixture(fixture, JObject.FromObject(loaded), "adapter load");
        AssertContainsFixture(fixture, await GetJson($"/sessions/{sessionId}/save"), "database load");
    }

    [Test]
    public async Task SaveGame_DayThree_ReplacesResearcherAndClientState()
    {
        await StartNewSession();
        await adapter.SaveGame(ToSaveData(Fixture("4. Save day 1")));

        JObject dayThree = Fixture("6. Save day 3");
        await adapter.SaveGame(ToSaveData(dayThree));

        SaveData loaded = await adapter.LoadGame();
        Assert.That(loaded.Researchers, Has.Count.EqualTo(10));
        Assert.That(loaded.Clients, Has.Count.EqualTo(3));
        AssertContainsFixture(dayThree, JObject.FromObject(loaded), "adapter load");
        AssertContainsFixture(dayThree, await GetJson($"/sessions/{sessionId}/save"), "database load");
    }

    [Test]
    public async Task SaveQuizData_MidGame_MergesAnswers()
    {
        await StartNewSession();
        await adapter.SaveGame(ToSaveData(Fixture("6. Save day 3")));

        await adapter.SaveQuizData(QuizType.MidGame,
            QuizResponses("8. Mid-game quiz answers"));
        await adapter.SaveQuizData(QuizType.MidGame,
            QuizResponses("9. Mid-game quiz - one more answer"));

        JObject collected = await GetJson($"/sessions/{sessionId}/data-collection");
        JObject answers = (JObject)collected["mid_game_quiz_responses"];
        Assert.That(answers, Is.Not.Null);
        Assert.That(answers.Count, Is.EqualTo(5));
        Assert.That(answers["4"].Value<int>(), Is.EqualTo(2));
        Assert.That(answers["1"].Value<int>(), Is.EqualTo(3));
        Assert.That(answers["7"].Value<int>(), Is.EqualTo(1));
        Assert.That(answers["2"].Value<int>(), Is.EqualTo(4));
        Assert.That(answers["11"].Value<int>(), Is.EqualTo(5));
        Assert.That(collected["score"].Value<int>(), Is.EqualTo(1900));
    }

    [Test]
    public async Task EndGame_PersistsFinalPlayer_ClearsEntities_AllowsPostQuiz()
    {
        await StartNewSession();
        await adapter.SaveGame(ToSaveData(Fixture("6. Save day 3")));
        JObject finalPlayer = Fixture("11. End game with final player state")["player"] as JObject;
        Assert.That(finalPlayer, Is.Not.Null);

        await adapter.EndGame(finalPlayer.ToObject<PlayerData>());
        JObject saved = await GetJson($"/sessions/{sessionId}/save");
        AssertContainsFixture(finalPlayer, saved["player"], "final player");
        Assert.That(saved["researchers"], Is.Empty);
        Assert.That(saved["clients"], Is.Empty);

        await adapter.SaveQuizData(QuizType.PostGame,
            QuizResponses("15. Post-game quiz after end"));
        JObject collected = await GetJson($"/sessions/{sessionId}/data-collection");
        Assert.That(collected["score"].Value<int>(), Is.EqualTo(6400));
        Assert.That(collected["post_game_quiz_responses"], Has.Count.EqualTo(4));
        Assert.That(collected["post_game_quiz_responses"]["9"].Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public async Task SaveGame_AfterEnd_RejectsWithConflict()
    {
        await StartNewSession();
        SaveData dayOne = ToSaveData(Fixture("4. Save day 1"));
        await adapter.SaveGame(dayOne);
        await adapter.EndGame(null);

        Exception failure = null;
        try { await adapter.SaveGame(dayOne); }
        catch (Exception ex) { failure = ex; }

        Assert.That(failure, Is.Not.Null, "Saving an ended session must fail");
        StringAssert.Contains("409", failure.Message);
    }

    private async Task StartNewSession()
    {
        // Current adapter creates a session, then GET /save returns 404 until first PUT.
        // Once LoadGame handles the initial 404 gracefully, remove this catch.
        try { await adapter.LoadGame(); }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("404")) throw;
        }

        FieldInfo field = typeof(SaveSystem).GetField("_sessionId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        sessionId = field.GetValue(adapter) as string;
        Assert.That(sessionId, Is.Not.Null.And.Not.Empty,
            "LoadGame did not establish a session");
    }

    private static SaveData ToSaveData(JObject body)
    {
        return new SaveData
        {
            Player = body["player"].ToObject<PlayerData>(),
            Researchers = body["researchers"].ToObject<List<ResearcherDataObject>>(),
            Clients = body["clients"].ToObject<List<ClientDataObject>>()
        };
    }

    private Dictionary<int, int> QuizResponses(string prefix)
    {
        return Fixture(prefix)["responses"].ToObject<Dictionary<int, int>>();
    }

    private JObject Fixture(string namePrefix)
    {
        JObject item = FindItem((JArray)collection["item"], namePrefix);
        Assert.That(item, Is.Not.Null, "Missing Postman request: " + namePrefix);
        string raw = item["request"]?["body"]?["raw"]?.Value<string>();
        Assert.That(raw, Is.Not.Null.And.Not.Empty);
        return JObject.Parse(raw);
    }

    private static JObject FindItem(JArray items, string prefix)
    {
        foreach (JObject item in items)
        {
            if (item["name"]?.Value<string>()?.StartsWith(prefix,
                    StringComparison.Ordinal) == true && item["request"] != null)
                return item;
            if (item["item"] is JArray nested)
            {
                JObject found = FindItem(nested, prefix);
                if (found != null) return found;
            }
        }
        return null;
    }

    // Verify every field in the fixture, allowing additional response/DTO fields.
    // Arrays must match in length and order.
    private static void AssertContainsFixture(JToken expected, JToken actual, string path)
    {
        Assert.That(actual, Is.Not.Null, $"Missing {path}");
        if (expected is JObject expectedObject)
        {
            Assert.That(actual, Is.TypeOf<JObject>(), path);
            foreach (JProperty property in expectedObject.Properties())
                AssertContainsFixture(property.Value, actual[property.Name], path + "." + property.Name);
        }
        else if (expected is JArray expectedArray)
        {
            Assert.That(actual, Is.TypeOf<JArray>(), path);
            Assert.That(actual.Count(), Is.EqualTo(expectedArray.Count()), path);
            for (int i = 0; i < expectedArray.Count; i++)
                AssertContainsFixture(expectedArray[i], actual[i], path + "[" + i + "]");
        }
        else
        {
            Assert.That(JToken.DeepEquals(expected, actual), Is.True,
                $"{path}: expected {expected}, got {actual}");
        }
    }

    private static async Task<JObject> GetJson(string path)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(BaseUrl + path))
        {
            await request.SendWebRequest();
            Assert.That(request.responseCode, Is.EqualTo(200),
                request.downloadHandler.text);
            return JObject.Parse(request.downloadHandler.text);
        }
    }
}
