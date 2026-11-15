using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game;

/// <summary>
/// Tuning numbers for one difficulty level. All balance knobs live here; BalanceTests guards the resulting
/// victory rates so tweaks cannot silently make the game trivial or impossible.
/// </summary>
internal sealed record DifficultyProfile(
    Difficulty Difficulty,
    int StartFuel,
    int StartRations,
    int StartScrip,
    int StartCrew,
    int Columns,
    double TravelEventChance,
    double PenaltyMultiplier,
    double RewardMultiplier,
    int ChallengeSeconds,
    int HardshipSeconds,
    int? DilemmaSeconds,
    int SkirmishTargets,
    int SkirmishSeconds,
    double SkirmishSpeed,
    int SkirmishTargetSize,
    int RepairSeconds,
    int PricePercent,
    int FuelPerDay,
    int HullWearPerDay,
    int MoraleDriftPerDay)
{
    private static readonly DifficultyProfile EasyProfile = new(
        Difficulty: Difficulty.Easy,
        StartFuel: 50,
        StartRations: 55,
        StartScrip: 90,
        StartCrew: 4,
        Columns: 7,
        TravelEventChance: 0.35,
        PenaltyMultiplier: 0.8,
        RewardMultiplier: 1.2,
        ChallengeSeconds: 300,
        HardshipSeconds: 180,
        DilemmaSeconds: null,
        SkirmishTargets: 12,
        SkirmishSeconds: 25,
        SkirmishSpeed: 0.8,
        SkirmishTargetSize: 56,
        RepairSeconds: 30,
        PricePercent: 100,
        FuelPerDay: 5,
        HullWearPerDay: 3,
        MoraleDriftPerDay: 1);

    private static readonly DifficultyProfile NormalProfile = new(
        Difficulty: Difficulty.Normal,
        StartFuel: 55,
        StartRations: 55,
        StartScrip: 80,
        StartCrew: 5,
        Columns: 8,
        TravelEventChance: 0.4,
        PenaltyMultiplier: 1.0,
        RewardMultiplier: 0.9,
        ChallengeSeconds: 180,
        HardshipSeconds: 120,
        DilemmaSeconds: 150,
        SkirmishTargets: 14,
        SkirmishSeconds: 22,
        SkirmishSpeed: 1.0,
        SkirmishTargetSize: 48,
        RepairSeconds: 45,
        PricePercent: 130,
        FuelPerDay: 6,
        HullWearPerDay: 4,
        MoraleDriftPerDay: 2);

    private static readonly DifficultyProfile HardProfile = new(
        Difficulty: Difficulty.Hard,
        StartFuel: 45,
        StartRations: 45,
        StartScrip: 60,
        StartCrew: 5,
        Columns: 9,
        TravelEventChance: 0.5,
        PenaltyMultiplier: 1.35,
        RewardMultiplier: 0.85,
        ChallengeSeconds: 120,
        HardshipSeconds: 75,
        DilemmaSeconds: 90,
        SkirmishTargets: 16,
        SkirmishSeconds: 20,
        SkirmishSpeed: 1.3,
        SkirmishTargetSize: 40,
        RepairSeconds: 60,
        PricePercent: 150,
        FuelPerDay: 6,
        HullWearPerDay: 4,
        MoraleDriftPerDay: 3);

    // Retuned travel for runs with emergency fuel (Workflow.Patched "emergency-fuel"). The hauler removed the
    // stranding defeats that much of Easy's and Normal's difficulty used to come from (the simulated average player
    // went from 90/65% to 100/88% victories), so the black itself is harsher instead; BalanceTests is back at
    // ~90/67/30%. Easy wears the hull faster than Normal on purpose: its repairs are cheaper and faster and its
    // events pay more, so the extra wear is pressure a player can recover from. Start values are not retuned:
    // they are fixed before a run consults the patch.
    private static readonly DifficultyProfile EasyRevised = EasyProfile with { HullWearPerDay = 7, TravelEventChance = 0.5, MoraleDriftPerDay = 2 };

    private static readonly DifficultyProfile NormalRevised = NormalProfile with { HullWearPerDay = 6, TravelEventChance = 0.5 };

    /// <summary>The original profile for a difficulty (start values, and every rule in pre-patch runs).</summary>
    public static DifficultyProfile For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => EasyProfile,
        Difficulty.Hard => HardProfile,
        _ => NormalProfile,
    };

    /// <summary>The profile a running game's rules use: retuned when the run has emergency fuel.</summary>
    public static DifficultyProfile For(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.EmergencyFuelEnabled)
        {
            return For(state.Settings.Difficulty);
        }

        return state.Settings.Difficulty switch
        {
            Difficulty.Easy => EasyRevised,
            Difficulty.Hard => HardProfile,
            _ => NormalRevised,
        };
    }
}
