namespace Calcutta.Models;

public class BidRecommendation
{
    public Team Team { get; set; } = null!;

    /// <summary>Probability (0–1) that the team wins in each round. Index 0 = Round 1, Index 5 = Championship.</summary>
    public double[] WinProbabilities { get; set; } = Array.Empty<double>();

    /// <summary>Expected payout as a percentage of the total pot.</summary>
    public double ExpectedValuePercent { get; set; }

    /// <summary>Recommended maximum bid in dollars (ExpectedValuePercent / 100 * pot size).</summary>
    public double RecommendedBid { get; set; }
}
