using Calcutta.Models;
using Calcutta.Services;

// ─── Parse command-line arguments ───────────────────────────────────────────

string? teamsFile   = null;
string? payoutsFile = null;
double  potSize     = 0;
double  stdDev      = 11.0;
bool    showHelp    = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "--teams":   teamsFile   = NextArg(args, ref i, "--teams");   break;
        case "--payouts": payoutsFile = NextArg(args, ref i, "--payouts"); break;
        case "--pot":     potSize     = ParsePositiveDouble(NextArg(args, ref i, "--pot"), "--pot"); break;
        case "--stddev":  stdDev      = ParsePositiveDouble(NextArg(args, ref i, "--stddev"), "--stddev"); break;
        case "--help":
        case "-h":        showHelp = true; break;
        default:
            // Positional args: teams, payouts, pot
            if (teamsFile   == null) teamsFile   = args[i];
            else if (payoutsFile == null) payoutsFile = args[i];
            else if (potSize == 0) potSize = ParsePositiveDouble(args[i], "pot");
            break;
    }
}

if (showHelp || teamsFile == null || payoutsFile == null)
{
    PrintUsage();
    return showHelp ? 0 : 1;
}

if (potSize <= 0)
{
    Console.Error.WriteLine("Error: pot size must be a positive number. Use --pot <amount>.");
    return 1;
}

// ─── Load data ───────────────────────────────────────────────────────────────

List<Team> teams;
List<RoundPayout> payouts;
try
{
    teams   = CsvParser.ParseTeams(teamsFile);
    payouts = CsvParser.ParsePayouts(payoutsFile);
}
catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
{
    Console.Error.WriteLine($"Error loading data: {ex.Message}");
    return 1;
}

// ─── Run simulation ───────────────────────────────────────────────────────────

List<BidRecommendation> recs;
try
{
    var calc = new TournamentCalculator(new ProbabilityCalculator { GameStdDev = stdDev });
    recs = calc.Calculate(teams, payouts, potSize);
}
catch (InvalidDataException ex)
{
    Console.Error.WriteLine($"Error simulating tournament: {ex.Message}");
    return 1;
}

// ─── Display results ─────────────────────────────────────────────────────────

// Sort payouts by round number for column headers.
var sortedPayouts = payouts.OrderBy(p => p.Round).ToList();

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║        NCAA Tournament Calcutta Auction Bid Advisor          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"  Teams loaded : {teams.Count}");
Console.WriteLine($"  Pot size     : ${potSize:N0}");
Console.WriteLine($"  KenPom σ     : {stdDev} points");
Console.WriteLine();

// Print payout structure.
Console.WriteLine("  Payout Structure:");
foreach (var p in sortedPayouts)
    Console.WriteLine($"    Round {p.Round} ({p.Name,-14}): {p.PayoutPercent,5:0.##}% per win");
Console.WriteLine();

// Build abbreviated column headers from payout round names.
const int probW   = 6;
var roundNames = sortedPayouts
    .Select(p => AbbreviateRound(p.Name))
    .ToArray();

// Column widths.
const int rankW   = 4;
const int seedW   = 4;
const int teamW   = 22;
const int regionW = 9;
const int evW     = 8;
const int bidW    = 11;

string divider =
    new string('─', rankW + 2) + "┼" +
    new string('─', seedW + 2) + "┼" +
    new string('─', teamW + 2) + "┼" +
    new string('─', regionW + 2) + "┼" +
    string.Join("┬", roundNames.Select(_ => new string('─', probW + 2))) + "┼" +
    new string('─', evW + 2) + "┼" +
    new string('─', bidW + 2);

string header =
    " " + "Rank".PadLeft(rankW) + " │ " +
    "Seed".PadRight(seedW) + " │ " +
    "Team".PadRight(teamW) + " │ " +
    "Region".PadRight(regionW) + " │ " +
    string.Join(" │ ", roundNames.Select(n => n.PadLeft(probW))) + " │ " +
    "ExpVal%".PadLeft(evW) + " │ " +
    "Bid".PadLeft(bidW);

Console.WriteLine(header);
Console.WriteLine(divider);

int rank = 1;
foreach (var rec in recs)
{
    var t = rec.Team;

    string teamName = t.Name.Length > teamW ? t.Name[..teamW] : t.Name;
    string region   = t.Region.Length > regionW ? t.Region[..regionW] : t.Region;

    var probCols = rec.WinProbabilities
        .Take(sortedPayouts.Count)
        .Select(p => $"{p * 100,5:0.0}%")
        .ToArray();

    Console.WriteLine(
        " " + rank.ToString().PadLeft(rankW) + " │ " +
        t.Seed.ToString().PadRight(seedW) + " │ " +
        teamName.PadRight(teamW) + " │ " +
        region.PadRight(regionW) + " │ " +
        string.Join(" │ ", probCols.Select(c => c.PadLeft(probW))) + " │ " +
        $"{rec.ExpectedValuePercent,7:0.00}%" + " │ " +
        $"${rec.RecommendedBid,9:N2}");

    rank++;
}

Console.WriteLine();
Console.WriteLine("  Win probabilities are per-round (probability of winning that specific game).");
Console.WriteLine("  Recommended bid = (Expected Value %) × Pot Size.");
Console.WriteLine("  Bidding at or below Recommended Bid is break-even assuming perfect KenPom accuracy.");
Console.WriteLine();

return 0;

// ─── Local functions ─────────────────────────────────────────────────────────

static string NextArg(string[] args, ref int i, string flag)
{
    if (++i >= args.Length)
        throw new ArgumentException($"Flag '{flag}' requires a value.");
    return args[i];
}

static double ParsePositiveDouble(string s, string name)
{
    if (double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double v) && v > 0)
        return v;
    throw new ArgumentException($"'{name}' must be a positive number, got: '{s}'.");
}

static void PrintUsage()
{
    Console.WriteLine(@"
NCAA Tournament Calcutta Auction Bid Advisor
============================================
Usage:
  calcutta --teams <teams.csv> --payouts <payouts.csv> --pot <amount> [--stddev <σ>]

Arguments:
  --teams   <file>    CSV file with tournament team data (team, seed, region, AdjEM, ...)
  --payouts <file>    CSV file with payout structure (round, name, payoutpercent)
  --pot     <amount>  Total auction pot in dollars  (e.g. 10000)
  --stddev  <σ>       KenPom game standard deviation in points (default: 11)
  --help              Show this message

Teams CSV columns (case-insensitive, extra columns ignored):
  Team / Name         Team name
  Seed                Seed number (1–16)
  Region              Region name (East, West, South, Midwest) or number (1–4)
  AdjEM               KenPom Adjusted Efficiency Margin
  AdjO  (optional)    KenPom Adjusted Offensive Efficiency
  AdjD  (optional)    KenPom Adjusted Defensive Efficiency

Payouts CSV columns:
  Round               Round number (1–6)
  Name  (optional)    Round name
  PayoutPercent       Percentage of pot paid for each win in this round

Sample files are included in the Data/ directory.

Examples:
  calcutta --teams Data/sample_teams.csv --payouts Data/sample_payouts.csv --pot 10000
  calcutta Data/sample_teams.csv Data/sample_payouts.csv 5000
");
}

static string AbbreviateRound(string name)
{
    return name.ToUpperInvariant() switch
    {
        "ROUND OF 64"    or "R64"  or "FIRST ROUND"           => " R64",
        "ROUND OF 32"    or "R32"  or "SECOND ROUND"          => " R32",
        "SWEET 16"       or "S16"  or "SWEET SIXTEEN"         => " S16",
        "ELITE 8"        or "E8"   or "ELITE EIGHT"           => "  E8",
        "FINAL FOUR"     or "FF"                              => "  FF",
        "CHAMPIONSHIP"   or "NC"   or "NATIONAL CHAMPIONSHIP" => "  NC",
        _ => name.Length > probW ? name[..probW] : name.PadLeft(probW),
    };
}
