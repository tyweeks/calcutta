namespace Calcutta.Models;

public class Team
{
    public string Name { get; set; } = string.Empty;
    public int Seed { get; set; }
    public string Region { get; set; } = string.Empty;

    /// <summary>KenPom Adjusted Efficiency Margin (offensive efficiency minus defensive efficiency).</summary>
    public double AdjEM { get; set; }

    /// <summary>KenPom Adjusted Offensive Efficiency (points scored per 100 possessions, adjusted for opponent).</summary>
    public double AdjO { get; set; }

    /// <summary>KenPom Adjusted Defensive Efficiency (points allowed per 100 possessions, adjusted for opponent).</summary>
    public double AdjD { get; set; }

    /// <summary>Zero-based position in the 64-team bracket (0–63), assigned based on region and seed.</summary>
    public int BracketPosition { get; set; }
}
