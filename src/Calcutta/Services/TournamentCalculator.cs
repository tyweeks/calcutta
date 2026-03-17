using Calcutta.Models;

namespace Calcutta.Services;

/// <summary>
/// Simulates the 64-team NCAA tournament bracket using forward-propagation
/// and returns bid recommendations for a Calcutta auction.
///
/// Bracket layout (0-based positions 0–63):
///   Positions  0–15  → Region 0 (e.g. East)
///   Positions 16–31  → Region 1 (e.g. West)
///   Positions 32–47  → Region 2 (e.g. South)
///   Positions 48–63  → Region 3 (e.g. Midwest)
///
/// Within each region (16 teams), seeds occupy positions in standard
/// NCAA bracket order: 1,16,8,9,5,12,4,13,6,11,3,14,7,10,2,15.
///
/// Round structure (1-indexed):
///   Round 1  – Round of 64   (pod size  2)
///   Round 2  – Round of 32   (pod size  4)
///   Round 3  – Sweet 16      (pod size  8)
///   Round 4  – Elite 8       (pod size 16)
///   Round 5  – Final Four    (pod size 32)
///   Round 6  – Championship  (pod size 64)
///
/// Algorithm:
///   reach[i][r]  = P(team i has won all games prior to round r; reaches round r)
///   reach[i][1]  = 1  for all i
///   reach[i][r+1]= reach[i][r] * Σ_{j in opposing pod r} reach[j][r] * P(i beats j)
///
///   Because teams i and j are in separate pods for rounds 1..r-1, their
///   advancement probabilities are independent, making this calculation exact.
///
///   The payout earned in round r  ∝  reach[i][r+1]  (= P that the team won that game).
/// </summary>
public class TournamentCalculator
{
    private const int TotalTeams = 64;
    private const int TotalRounds = 6;

    // Standard NCAA bracket seed order within a 16-team region.
    private static readonly int[] SeedToRegionPosition = BuildSeedPositionMap();

    // Supported region names mapped to region index 0-3.
    private static readonly Dictionary<string, int> RegionIndex =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["east"]    = 0,
            ["west"]    = 1,
            ["south"]   = 2,
            ["midwest"] = 3,
            // Numeric fallbacks
            ["1"] = 0, ["2"] = 1, ["3"] = 2, ["4"] = 3,
        };

    private readonly ProbabilityCalculator _prob;

    public TournamentCalculator(ProbabilityCalculator? probabilityCalculator = null)
    {
        _prob = probabilityCalculator ?? new ProbabilityCalculator();
    }

    /// <summary>
    /// Assigns bracket positions to each team, simulates all 6 rounds, and
    /// returns bid recommendations sorted by expected value (descending).
    /// </summary>
    /// <param name="teams">All 64 tournament teams (region + seed must be unique).</param>
    /// <param name="payouts">Per-round payout percentages (rounds 1–6).</param>
    /// <param name="potSize">Total auction pot in dollars.</param>
    public List<BidRecommendation> Calculate(List<Team> teams, List<RoundPayout> payouts, double potSize)
    {
        ValidateInputs(teams, payouts);
        AssignBracketPositions(teams);

        // Build ordered array indexed by bracket position.
        var byPosition = new Team[TotalTeams];
        foreach (var t in teams)
            byPosition[t.BracketPosition] = t;

        // reach[i][r] = P(team at bracket position i reaches round r), r is 1-based (index 0 = round 1).
        // reach[i][0] = 1.0 for everyone.
        var reach = new double[TotalTeams, TotalRounds + 1];
        for (int i = 0; i < TotalTeams; i++)
            reach[i, 0] = 1.0;

        // payoutByRound[r] = payout percent for a win in round r+1 (0-based index).
        var payoutByRound = BuildPayoutArray(payouts);

        // Forward-propagate: compute reach[][r+1] from reach[][r].
        for (int round = 0; round < TotalRounds; round++)
        {
            int podSize    = 1 << (round + 1); // 2, 4, 8, 16, 32, 64
            int halfPod    = podSize / 2;

            for (int i = 0; i < TotalTeams; i++)
            {
                // Determine the start of team i's pod and which half they're in.
                int podStart     = (i / podSize) * podSize;
                bool inFirstHalf = (i - podStart) < halfPod;
                int oppStart     = inFirstHalf ? podStart + halfPod : podStart;
                int oppEnd       = oppStart + halfPod; // exclusive

                double winProb = 0.0;
                for (int j = oppStart; j < oppEnd; j++)
                {
                    double p = _prob.WinProbability(byPosition[i].AdjEM, byPosition[j].AdjEM);
                    winProb += reach[j, round] * p;
                }

                reach[i, round + 1] = reach[i, round] * winProb;
            }
        }

        // Build recommendations.
        var recommendations = new List<BidRecommendation>(teams.Count);
        foreach (var team in teams)
        {
            int pos = team.BracketPosition;
            var winProbs = new double[TotalRounds];
            double expectedValuePct = 0.0;

            for (int round = 0; round < TotalRounds; round++)
            {
                // P(team wins in round r) = reach[pos][round+1]
                winProbs[round] = reach[pos, round + 1];
                expectedValuePct += winProbs[round] * payoutByRound[round];
            }

            recommendations.Add(new BidRecommendation
            {
                Team                = team,
                WinProbabilities    = winProbs,
                ExpectedValuePercent = expectedValuePct,
                RecommendedBid      = potSize * expectedValuePct / 100.0,
            });
        }

        recommendations.Sort((a, b) => b.ExpectedValuePercent.CompareTo(a.ExpectedValuePercent));
        return recommendations;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Assigns BracketPosition based on region name and seed.</summary>
    private static void AssignBracketPositions(List<Team> teams)
    {
        // Validate that each (region, seed) pair is unique.
        var seen = new HashSet<(int region, int seed)>();

        foreach (var team in teams)
        {
            if (!RegionIndex.TryGetValue(team.Region.Trim(), out int regionIdx))
                throw new InvalidDataException(
                    $"Unknown region '{team.Region}' for team '{team.Name}'. " +
                    "Supported regions: East, West, South, Midwest (or 1–4).");

            if (team.Seed is < 1 or > 16)
                throw new InvalidDataException(
                    $"Seed {team.Seed} for team '{team.Name}' is out of range (expected 1–16).");

            var key = (regionIdx, team.Seed);
            if (!seen.Add(key))
                throw new InvalidDataException(
                    $"Duplicate team entry: region '{team.Region}', seed {team.Seed}.");

            int posWithinRegion = SeedToRegionPosition[team.Seed - 1]; // seed is 1-based
            team.BracketPosition = regionIdx * 16 + posWithinRegion;
        }
    }

    /// <summary>
    /// Builds the seed→position-within-region lookup (0-based result array: index = seed−1).
    /// Standard NCAA bracket order: 1,16,8,9,5,12,4,13,6,11,3,14,7,10,2,15.
    /// </summary>
    private static int[] BuildSeedPositionMap()
    {
        // seedOrder[pos] = the seed number that occupies bracket position pos (0-based within a region).
        int[] seedOrder = [1, 16, 8, 9, 5, 12, 4, 13, 6, 11, 3, 14, 7, 10, 2, 15];
        // result[seed - 1] = bracket position of that seed within the region.
        var result = new int[16];
        for (int pos = 0; pos < seedOrder.Length; pos++)
            result[seedOrder[pos] - 1] = pos;
        return result;
    }

    /// <summary>Builds a 6-element payout array (0-based round index) from the provided payouts list.</summary>
    private static double[] BuildPayoutArray(List<RoundPayout> payouts)
    {
        var arr = new double[TotalRounds];
        foreach (var p in payouts)
        {
            if (p.Round is >= 1 and <= TotalRounds)
                arr[p.Round - 1] = p.PayoutPercent;
        }
        return arr;
    }

    private static void ValidateInputs(List<Team> teams, List<RoundPayout> payouts)
    {
        if (teams.Count != TotalTeams)
            throw new InvalidDataException(
                $"Expected exactly {TotalTeams} teams, but found {teams.Count}. " +
                "The NCAA tournament bracket requires 64 teams.");

        if (payouts.Count == 0)
            throw new InvalidDataException("No payout rules provided.");

        if (payouts.Any(p => p.PayoutPercent < 0))
            throw new InvalidDataException("Payout percentages must be non-negative.");
    }
}
