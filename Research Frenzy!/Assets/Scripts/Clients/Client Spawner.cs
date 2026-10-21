using System;
using System.Collections.Generic;
using Clients;
using UnityEngine;

/// <summary>
/// Puts clients into the world. Client data comes from the ClientGenerator; this creates the
/// GameObject for each one (new clients each morning, returning clients, or clients restored from a save).
/// </summary>
public class ClientSpawner : MonoBehaviour
{
    [Tooltip("Prefab with a Client component. If empty, a bare GameObject is created (handy for testing).")]
    [SerializeField] private Client clientPrefab;
    [SerializeField] private Transform spawnParent;

    [Header("Placement")]
    [Tooltip("Where the first client appears (the door). If empty, this object's position.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Offset between clients that are in the lab at the same time.")]
    [SerializeField] private Vector3 spacing = new Vector3(1.5f, 0f, 0f);

    private readonly List<Client> activeClients = new List<Client>();

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
        Vector3 position = origin.position + spacing * activeClients.Count;

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
        activeClients.Remove(client);
        Destroy(client.gameObject);
    }

    /// <summary>Destroys all client bodies, e.g. at the end of the day or before loading a save.</summary>
    public void Clear()
    {
        foreach (Client c in activeClients)
            if (c != null) Destroy(c.gameObject);
        activeClients.Clear();
    }
}