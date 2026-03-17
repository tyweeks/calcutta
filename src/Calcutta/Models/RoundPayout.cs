namespace Calcutta.Models;

public class RoundPayout
{
    /// <summary>Round number (1 = Round of 64, 2 = Round of 32, 3 = Sweet 16, 4 = Elite 8, 5 = Final Four, 6 = Championship).</summary>
    public int Round { get; set; }

    /// <summary>Human-readable name for this round (e.g. "Sweet 16").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Percentage of the total pot paid out for each win in this round.</summary>
    public double PayoutPercent { get; set; }
}
