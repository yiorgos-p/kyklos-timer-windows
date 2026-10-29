namespace Kyklos.Engine;

/// <summary>Today's finished focus sessions as dots: one per session in the current round, filling up toward a long break.</summary>
public static class SessionDots
{
    /// <summary>How many dots are filled. A completed round shows them all until the next session starts a new one.</summary>
    public static int Filled(int completed, int perCycle)
    {
        if (completed <= 0)
        {
            return 0;
        }

        var remainder = completed % perCycle;
        return remainder == 0 ? perCycle : remainder;
    }
}
