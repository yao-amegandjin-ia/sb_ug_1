using System;
using System.Collections.Generic;
using Clients;
using UnityEngine;

/// <summary>
/// Puts clients into the world. Client data comes from the ClientGenerator; this creates the
/// GameObject for each one (new clients each morning, returning clients, or clients restored from a save).
///  Also knows where the entrance and the waiting room seats are. Clients appear at the
/// entrance one at a time, and TakeSeat / FreeSeat hand out waiting room seats so two clients never sit on the same one.
/// </summary>
public class ClientSpawner : MonoBehaviour
{
    [Tooltip("Prefab with a Client component. If empty, a bare GameObject is created (handy for testing).")]
    [SerializeField] private Client clientPrefab;
    [SerializeField] private Transform spawnParent;

    [Header("Placement")]
    [Tooltip("The entrance: where every client appears and waits to be greeted. If empty, this object's position.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Gap between waiting room spots when there are more people waiting than Waiting Seats below.")]
    [SerializeField] private Vector3 spacing = new Vector3(1.5f, 0f, 0f);
    
    [Header("Waiting room")]
    [Tooltip("One empty GameObject per seat in the waiting room. Clients sent to wait walk to the first free one.")]
    [SerializeField] private Transform[] waitingSeats = new Transform[0];
    [Tooltip("Used when every seat above is taken (a Couch upgrade gave more seats than there are spots): extra " +
             "clients line up from here, Spacing apart. If empty, they line up from the last seat.")]
    [SerializeField] private Transform waitingOverflow;
    [Tooltip("How fast clients walk to their seat.")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 4f;

    private readonly List<Client> activeClients = new List<Client>();
    private readonly Dictionary<Client, int> seatOf = new Dictionary<Client, int>();  

    /// <summary>How fast clients walk to their seat (Client reads this).</summary>
    public float WalkSpeed => walkSpeed; 

    /// <summary>Client bodies currently in the world.</summary>
    public IReadOnlyList<Client> ActiveClients => activeClients;

    /// <summary>Raised for every client that appears in the world.</summary>
    public event Action<Client> ClientSpawned;

    /// <summary>Creates a body for each client. Null entries are skipped.</summary>
    public List<Client> Spawn(IEnumerable<ClientData> clients)
    {
        List<Client> spawned = new List<Client>();
        if (clients == null) return spawned;

        foreach (ClientData data in clients)
        {
            Client client = Spawn(data);
            if (client != null) spawned.Add(client);
        }
        return spawned;
    }

    /// <summary>Creates a body for one client (null if the data is null).</summary>
    public Client Spawn(ClientData data)
    {
        if (data == null) return null;

        activeClients.RemoveAll(c => c == null); // drop bodies destroyed elsewhere
        
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 position = origin.position;

        Client instance;
        if (clientPrefab != null)
        {
            instance = Instantiate(clientPrefab, position, origin.rotation, spawnParent);
        }
        else
        {
            GameObject go = new GameObject("Client");
            go.transform.position = position;
            go.transform.rotation = origin.rotation;
            if (spawnParent != null) go.transform.SetParent(spawnParent);
            instance = go.AddComponent<Client>();
        }

        instance.Bind(data);
        activeClients.Add(instance);
        ClientSpawned?.Invoke(instance);
        return instance;
    }

    /// <summary>Finds the body of a client by client ID, or null.</summary>
    public Client GetById(int clientId)
    {
        foreach (Client c in activeClients)
            if (c != null && c.Data != null && c.Data.Id == clientId) return c;
        return null;
    }

    /// <summary>Removes one client's body (their data is untouched).</summary>
    public void Remove(Client client)
    {
        if (client == null) return;
        FreeSeat(client); 
        activeClients.Remove(client);
        Destroy(client.gameObject);
    }

    /// <summary>Destroys all client bodies, e.g. at the end of the day or before loading a save.</summary>
    public void Clear()
    {
        foreach (Client c in activeClients)
            if (c != null) Destroy(c.gameObject);
        activeClients.Clear();
        seatOf.Clear();  
    }
    

    /// <summary>
    /// Gives this client the first free seat and returns where it is. A client who already has a seat keeps it.
    /// Returns null when there's nowhere set up for them to go (no seats and no overflow spot): they stay put.
    /// FirmController decides IF they may sit (WaitingSeats, the Couch upgrade). This only decides WHERE.
    /// </summary>
    public Vector3? TakeSeat(Client client)
    {
        if (client == null) return null;
        if (!seatOf.TryGetValue(client, out int seat))
        {
            seat = 0;
            while (seatOf.ContainsValue(seat)) seat++;   // the lowest number nobody is sitting on
            seatOf[client] = seat;
        }
        return SeatPosition(seat);
    }

    /// <summary>The client got up (helped, sent home, left). Their seat is free for the next one.</summary>
    public void FreeSeat(Client client)
    {
        if (client != null) seatOf.Remove(client);
    }

    private Vector3? SeatPosition(int seat)
    {
        if (waitingSeats != null && seat < waitingSeats.Length && waitingSeats[seat] != null)
            return waitingSeats[seat].position;

        // more people than seat objects: line them up from the overflow spot (or from the last seat)
        int extra = seat - (waitingSeats != null ? waitingSeats.Length : 0);
        if (waitingOverflow != null) return waitingOverflow.position + spacing * extra;
        if (waitingSeats != null && waitingSeats.Length > 0 && waitingSeats[waitingSeats.Length - 1] != null)
            return waitingSeats[waitingSeats.Length - 1].position + spacing * (extra + 1);
        return null;
    }
}