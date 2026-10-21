using System.Collections.Generic;
using Assets.Scripts.GameDataObjects;
using Clients;
using UnityEngine;
using Random = System.Random;

/// <summary>
///
/// This generates the clients for the day. It generates ClientData, which is solely data.
/// The game object is created with the ClientSpawner.
/// Results depend on game seed, day number and upgrade bonus.
/// We track used requests, which are requests that have been answered or ones that are returning.
///
/// This is done by shuffling requestIds using the game seed in a 'deck'.
/// The start of a round deals the first N requests in the deck that aren't in the used list.
/// Looks come from the seed and request ID, so returning clients can be built off of those.
/// </summary>
public class ClientGenerator : MonoBehaviour
{
    // Random stream IDs (see GameSeed.CreateRandom). Keep these unique across the project.
    private const int DeckStreamId = 2000;
    private const int ClientStreamId = 2001;   // used with the request ID as the key
 
    [Header("New clients per day")]
    [Tooltip("New clients on day 1, before upgrades. Returning clients are NOT counted here, they come on top.")]
    [SerializeField, Min(0)] private int startingClients = 3;
    [Tooltip("New clients added for each day after day 1. With 3 and 2: day 1 = 3, day 2 = 5, day 3 = 7...")]
    [SerializeField, Min(0)] private int clientsAddedPerDay = 2;
 
    [Header("Patience")]
    [Tooltip("Patience in seconds. The same for every client.")]
    [SerializeField, Min(1f)] private float patience = 30f;
 
    [Header("Appearance (number of assets available per slot)")]
    [SerializeField, Min(1)] private int headCount = 12;
    [SerializeField, Min(1)] private int bodyCount = 6;
    [SerializeField, Min(1)] private int legCount = 8;
 
    private readonly Dictionary<int, RequestData> requestsById = new Dictionary<int, RequestData>();
    private readonly List<int> sortedIds = new List<int>();
 
    [SerializeField]private List<int> deck;     // request IDs in shuffled order
    private int deckSeed;       // the seed the deck was built with
 
    public int TotalRequests => sortedIds.Count;
 
    public bool HasRequest(int requestId) => requestsById.ContainsKey(requestId);
 
    /// <summary>Call once at startup with the requests from the DataManager (RequestList.Requests).</summary>
    public void Initialize(IEnumerable<RequestData> requests)
    {
        requestsById.Clear();
        sortedIds.Clear();
        deck = null;
 
        if (requests == null)
        {
            Debug.LogError("ClientGenerator: no requests given (did the requests.json load?).");
            return;
        }
 
        foreach (RequestData request in requests)
        {
            if (request == null) continue;
 
            if (requestsById.ContainsKey(request.RequestId))
            {
                Debug.LogWarning($"ClientGenerator: duplicate RequestId {request.RequestId} ignored.");
                continue;
            }
            if (request.RequestId <= 0)
                Debug.LogWarning($"ClientGenerator: RequestId {request.RequestId} is not positive. " +
                                 "Client IDs are the request IDs, and 0 means 'no client' elsewhere. Start IDs at 1.");
 
            requestsById[request.RequestId] = request;
            sortedIds.Add(request.RequestId);
        }
 
        // Sorted first so the deck doesn't depend on the order requests appear in the JSON file.
        sortedIds.Sort();
    }
 
    // Generating
 
    /// <summary>
    ///
    /// Creates new clients for a day.
    /// usedRequestIds are requests that will not show again. Includes answered or returning clients.
    /// To get the same clients at the start of a load, pass the used list from the START of that day
    /// </summary>
    public List<ClientData> GenerateDay(int day, int upgradeBonus = 0, IEnumerable<int> usedRequestIds = null)
    {
        List<ClientData> result = new List<ClientData>();
 
        if (day < 1)
        {
            Debug.LogError($"ClientGenerator: day must be 1 or more (got {day}).");
            return result;
        }
 
        EnsureDeck();
        HashSet<int> used = usedRequestIds != null ? new HashSet<int>(usedRequestIds) : new HashSet<int>();
 
        int wanted = CalculateClientCount(day, upgradeBonus);
        foreach (int requestId in deck)
        {
            if (result.Count >= wanted) break;
            if (used.Contains(requestId)) continue;
            result.Add(CreateClientForRequest(requestId));
        }
 
        if (result.Count < wanted)
            Debug.LogWarning($"ClientGenerator: only {result.Count} unused requests left for day {day} (wanted {wanted}).");
 
        return result;
    }
 
    /// <summary>
    /// Builds the client for one request. Always the same result for the same seed and request,
    /// whatever the day. Use this to bring back a returning client, or to rebuild any client
    /// from just their request ID. Returns null if the request doesn't exist.
    /// </summary>
    public ClientData CreateClientForRequest(int requestId)
    {
        RequestData request;
        if (!requestsById.TryGetValue(requestId, out request))
        {
            Debug.LogError($"ClientGenerator: no request with ID {requestId}.");
            return null;
        }
 
        Random rng = GameSeed.CreateRandom(ClientStreamId, requestId);
 
        Vector3Int look = new Vector3Int(
            rng.Next(Mathf.Max(1, headCount)),
            rng.Next(Mathf.Max(1, bodyCount)),
            rng.Next(Mathf.Max(1, legCount)));
 
        ClientData client = new ClientData();
        client.Init(requestId, request, patience, look);   // client ID = request ID (unique, needs no saving)
        return client;
    }
 
    /// <summary>How many requests have not been used yet.</summary>
    public int CountRemaining(IEnumerable<int> usedRequestIds)
    {
        HashSet<int> used = usedRequestIds != null ? new HashSet<int>(usedRequestIds) : new HashSet<int>();
        int remaining = 0;
        foreach (int id in sortedIds)
            if (!used.Contains(id)) remaining++;
        return remaining;
    }
 
    /// <summary>
    /// Calculutes the client count for a day.
    /// upgrade bonus comes from upgrades.
    /// This does not include returning clients, these are done seperately.
    /// Day 3 with 1 upgrade bonus client and 2 returning clients is 7 + 1 + 2 = 10 in total.
    /// </summary>
    public int CalculateClientCount(int day, int upgradeBonus = 0)
    {
        int count = startingClients + clientsAddedPerDay * Mathf.Max(0, day - 1) + upgradeBonus;
        return Mathf.Max(0, count);
    }
 
    // ---------- Internals ----------
    
    //Shuffles the deck
    private void EnsureDeck()
    {
        int seed = GameSeed.EnsureInitialized();
        if (deck != null && deckSeed == seed) return;
 
        deck = new List<int>(sortedIds);
        Shuffle(deck, GameSeed.CreateRandom(DeckStreamId));
        deckSeed = seed;
    }
 
    // Shuffles with the seed to randomize
    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}

