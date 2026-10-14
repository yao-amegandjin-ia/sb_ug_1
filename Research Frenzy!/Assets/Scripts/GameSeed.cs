using System;

/// <summary>
/// Generates the seed for a game session. Set at the start of a new game, or can be set from a save file.
/// </summary>
public static class GameSeed
{
    public static bool IsSet { get; private set; }
    public static int Seed { get; private set; }

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
    public static Random CreateRandom(int streamId)
    {
        EnsureInitialized();
        //Unchecked exists for overflows. Instead of overflowing, it just wraps around
        //Using a streamId helps maintain consistency and keeps everything deterministic.
        return new Random(unchecked(Seed * 397) ^ streamId);
    }
}