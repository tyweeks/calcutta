# Calcutta

NCAA Tournament Calcutta Auction Bid Advisor — a C# console app that helps you determine how much to spend on each team in an NCAA tournament Calcutta auction draft.

## What it does

Given a CSV of team KenPom ratings and a CSV of your auction's payout structure, the app:

1. **Simulates the full 64-team bracket** using each team's KenPom Adjusted Efficiency Margin (AdjEM) to compute win probabilities for every possible matchup.
2. **Calculates the probability** that each team wins in each of the 6 tournament rounds.
3. **Computes an expected value** for each team based on your payout structure.
4. **Recommends a maximum bid** for each team (= expected value × pot size).

### Win probability model

Uses the standard KenPom formula for neutral-court games:

```
P(A beats B) = Φ((AdjEM_A − AdjEM_B) / σ)
```

where `Φ` is the standard normal CDF and `σ ≈ 11` points (the conventional standard deviation for college basketball game margins). You can override `σ` with `--stddev`.

---

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

### Build

```bash
dotnet build src/Calcutta/Calcutta.csproj
```

### Run with sample data

```bash
dotnet run --project src/Calcutta/Calcutta.csproj -- \
  --teams src/Calcutta/Data/sample_teams.csv \
  --payouts src/Calcutta/Data/sample_payouts.csv \
  --pot 10000
```

Or using positional arguments:

```bash
dotnet run --project src/Calcutta/Calcutta.csproj -- \
  src/Calcutta/Data/sample_teams.csv \
  src/Calcutta/Data/sample_payouts.csv \
  10000
```

---

## CSV File Formats

### Teams CSV

Must contain these columns (names are case-insensitive; extra columns are ignored):

| Column | Required | Description |
|--------|----------|-------------|
| `Team` or `Name` | ✅ | Team name |
| `Seed` | ✅ | Seed number (1–16) |
| `Region` | ✅ | `East`, `West`, `South`, or `Midwest` (or 1–4) |
| `AdjEM` | ✅ | KenPom Adjusted Efficiency Margin |
| `AdjO` | optional | KenPom Adjusted Offensive Efficiency |
| `AdjD` | optional | KenPom Adjusted Defensive Efficiency |

**Example:**

```csv
Team,Seed,Region,AdjEM,AdjO,AdjD
Duke,1,East,31.2,123.4,92.2
Alabama,2,East,24.7,119.8,95.1
...
```

### Payouts CSV

| Column | Required | Description |
|--------|----------|-------------|
| `Round` | ✅ | Round number (1–6) |
| `Name` | optional | Human-readable round name |
| `PayoutPercent` | ✅ | % of pot paid for each win in this round |

**Example:**

```csv
Round,Name,PayoutPercent
1,Round of 64,1
2,Round of 32,2
3,Sweet 16,4
4,Elite 8,8
5,Final Four,16
6,Championship,32
```

> **Note:** Payout percentages can be any non-negative values and do not need to sum to 100. A typical structure totals `192%` of the pot across all 63 games.

---

## Command-line Options

```
Usage:
  calcutta --teams <teams.csv> --payouts <payouts.csv> --pot <amount> [--stddev <σ>]

  --teams   <file>    CSV file with tournament team data
  --payouts <file>    CSV file with payout structure
  --pot     <amount>  Total auction pot in dollars (e.g. 10000)
  --stddev  <σ>       KenPom game std dev in points (default: 11)
  --help              Show usage
```

---

## Bracket Structure

The app uses the standard 64-team NCAA bracket:

| Round | Name | Teams |
|-------|------|-------|
| 1 | Round of 64 | 1v16, 8v9, 5v12, 4v13, 6v11, 3v14, 7v10, 2v15 (per region) |
| 2 | Round of 32 | Pod winners |
| 3 | Sweet 16 | Regional semi-finals |
| 4 | Elite 8 | Regional finals |
| 5 | Final Four | National semi-finals (East/West vs South/Midwest) |
| 6 | Championship | National final |

Regions are placed in the bracket as: **East** (slots 0–15), **West** (16–31), **South** (32–47), **Midwest** (48–63). Final Four matchups: East/West winner vs South/Midwest winner.

---

## Project Structure

```
calcutta.slnx
src/
└── Calcutta/
    ├── Calcutta.csproj
    ├── Program.cs                      # CLI entry point
    ├── Models/
    │   ├── Team.cs                     # Team data
    │   ├── RoundPayout.cs              # Per-round payout rule
    │   └── BidRecommendation.cs        # Output: probabilities + bid
    ├── Services/
    │   ├── CsvParser.cs                # Flexible CSV loader
    │   ├── ProbabilityCalculator.cs    # KenPom win-probability model
    │   └── TournamentCalculator.cs     # Bracket simulation engine
    └── Data/
        ├── sample_teams.csv            # 64-team sample bracket
        └── sample_payouts.csv          # Sample payout structure
```
