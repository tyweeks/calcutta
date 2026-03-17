namespace Calcutta.Services;

/// <summary>
/// Calculates head-to-head win probabilities using KenPom Adjusted Efficiency Margin (AdjEM).
///
/// The model treats a neutral-court game margin as normally distributed with:
///   mean  = AdjEM_A - AdjEM_B
///   stdev = <see cref="GameStdDev"/> (default 11 points, the conventional KenPom constant)
///
/// P(A beats B) = Φ((AdjEM_A − AdjEM_B) / stdev)
/// where Φ is the standard normal CDF.
/// </summary>
public class ProbabilityCalculator
{
    /// <summary>
    /// Standard deviation of a college-basketball game's point margin.
    /// The conventional KenPom value is 11. Adjust to calibrate aggressiveness.
    /// </summary>
    public double GameStdDev { get; set; } = 11.0;

    /// <summary>
    /// Returns P(teamA beats teamB) on a neutral court using their KenPom AdjEM values.
    /// </summary>
    public double WinProbability(double adjEmA, double adjEmB)
    {
        double z = (adjEmA - adjEmB) / GameStdDev;
        return NormalCdf(z);
    }

    // --- Normal distribution helpers ---

    /// <summary>Standard normal CDF via the error function: Φ(x) = 0.5*(1 + erf(x/√2)).</summary>
    public static double NormalCdf(double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));

    /// <summary>
    /// Error function approximation (Abramowitz &amp; Stegun, maximum error 1.5×10⁻⁷).
    /// </summary>
    public static double Erf(double x)
    {
        // erf is an odd function
        double sign = x < 0 ? -1.0 : 1.0;
        x = Math.Abs(x);

        const double a1 =  0.254829592;
        const double a2 = -0.284496736;
        const double a3 =  1.421413741;
        const double a4 = -1.453152027;
        const double a5 =  1.061405429;
        const double p  =  0.3275911;

        double t = 1.0 / (1.0 + p * x);
        double poly = t * (a1 + t * (a2 + t * (a3 + t * (a4 + t * a5))));
        return sign * (1.0 - poly * Math.Exp(-x * x));
    }
}
