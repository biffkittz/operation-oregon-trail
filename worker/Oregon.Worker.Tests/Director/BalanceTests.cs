using System.Globalization;
using Oregon.Contracts.Model;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Director;

/// <summary>
/// Guards game balance: an "average player" (sensible choices, judge verdicts drawn from a realistic spread)
/// should usually win on Easy, about half the time on Normal and rarely on Hard. Failing this after a rules
/// change means the tuning in DifficultyProfile drifted.
/// </summary>
public sealed class BalanceTests
{
    private const int Games = 60;

    [Theory]
    [InlineData(Difficulty.Easy, 0.70, 0.98)]
    [InlineData(Difficulty.Normal, 0.40, 0.80)]
    [InlineData(Difficulty.Hard, 0.10, 0.50)]
    public async Task VictoryRate_StaysWithinIntendedBand(Difficulty difficulty, double minRate, double maxRate)
    {
        int victories = 0;
        List<int> days = [];
        Dictionary<string, int> defeats = [];
        for (int seed = 1000; seed < 1000 + Games; seed++)
        {
            Random judge = new(seed * 7);
            CannedStoryteller teller = new() { JudgeOverride = _ => AveragePlayerVerdict(judge) };
            (GameDirector director, FixedClock clock, _) = TestGames.NewDirector(seed, difficulty, teller);
            Random bot = new(seed);
            await director.InitializeAsync();
            for (int step = 0; step < 600 && director.State.Status == GameStatus.Active; step++)
            {
                await Bot.StepAsync(director, clock, bot);
            }

            if (director.State.Status == GameStatus.Victory)
            {
                victories++;
                days.Add(director.State.Day);
            }
            else
            {
                string reason = director.State.Finale!.Epilogue.Split(':').Last().Trim();
                reason = reason.Length > 40 ? reason[..40] : reason;
                defeats[reason] = defeats.GetValueOrDefault(reason) + 1;
            }
        }

        double rate = victories / (double)Games;
        string report =
            $"{difficulty}: {victories}/{Games} victories ({rate.ToString("P0", CultureInfo.InvariantCulture)}), " +
            $"avg days {(days.Count == 0 ? 0 : days.Average()).ToString("F1", CultureInfo.InvariantCulture)}; defeats: " +
            string.Join("; ", defeats.Select(d => $"{d.Value}x {d.Key}"));
        TestContext.Current.TestOutputHelper?.WriteLine(report);
        Assert.InRange(rate, minRate, maxRate);

        // Tuning aid: BALANCE_REPORT=1 makes every case fail so the runner prints all three reports.
        Assert.False(Environment.GetEnvironmentVariable("BALANCE_REPORT") == "1", report);
    }

    /// <summary>15% triumph, 35% success, 25% mixed, 18% setback, 7% disaster.</summary>
    private static EvaluationResult AveragePlayerVerdict(Random random)
    {
        double roll = random.NextDouble();
        Verdict verdict = roll switch
        {
            < 0.15 => Verdict.CriticalSuccess,
            < 0.50 => Verdict.Success,
            < 0.75 => Verdict.Partial,
            < 0.93 => Verdict.Failure,
            _ => Verdict.CriticalFailure,
        };
        return new EvaluationResult(verdict, 50, "simulated", "Something happens.", string.Empty, ManipulationDetected: false);
    }
}
