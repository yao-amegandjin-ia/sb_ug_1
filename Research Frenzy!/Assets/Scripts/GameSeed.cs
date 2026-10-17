using System;

/// <summary>
/// Generates the seed for a game session. Set at the start of a new game, or can be set from a save file.
/// </summary>
public static class GameSeed
{
    public static bool IsSet { get; private set; }
    public static int Seed { get; private set; }
    
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsSet = false;
        Seed = 0;
    }


    /// <summary>Sets the seed explicitly (new game with a chosen seed, or loading a save).</summary>
    public static void Set(int seed)
    {
        Seed = seed;
        IsSet = true;
    }

    /// <summary>Returns the seed, creating a random one first if none has been set yet.</summary>
    public static int EnsureInitialized()
    {
        if (!IsSet)
            Set(Environment.TickCount);
        return Seed;
    }

    /// <summary>
    /// Creates an independent, deterministic random stream. Give each system its own
    /// streamId so changing how much one system draws doesn't shift another's results.
    /// </summary>
    public static System.Random CreateRandom(int streamId)
    {
        EnsureInitialized();
        //Unchecked exists for overflows. Instead of overflowing, it just wraps around
        //Using a streamId helps maintain consistency and keeps everything deterministic.
        return new System.Random(unchecked(Seed * 397) ^ streamId);
    }
    
    /// <summary>
    /// A stream for one item out of many, e.g. CreateRandom(clientStreamId, requestId) or
    /// CreateRandom(dayStreamId, day). Use this when the key is a counter or ID (1, 2, 3...).
    /// The three numbers are hashed together first, because System.Random gives patterned results
    /// for neighbouring seeds. With CreateRandom(streamId + requestId), neighbouring requests came out
    /// as pairs like 0 6 0 6 11 5 11 5.
    /// </summary>
    public static System.Random CreateRandom(int streamId, int key)
    {
        unchecked
        {
            uint h = Mix((uint)EnsureInitialized() + 0x9E3779B9u);
            h = Mix(h ^ ((uint)streamId * 0x85EBCA6Bu));
            h = Mix(h ^ (uint)key);
            return new System.Random((int)h);
        }
    }
 
    // MurmurHash3 finaliser: a small change in the input flips about half of the output bits.
    private static uint Mix(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }

}