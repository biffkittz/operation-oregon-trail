using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.Rules;

/// <summary>Human-readable verdict labels.</summary>
internal static class VerdictText
{
    public static string Describe(Verdict verdict) => verdict switch
    {
        Verdict.CriticalSuccess => "a triumph",
        Verdict.Success => "a success",
        Verdict.Partial => "a mixed result",
        Verdict.Failure => "a setback",
        _ => "a disaster",
    };

    public static string Headline(Verdict verdict) => verdict switch
    {
        Verdict.CriticalSuccess => "Triumph!",
        Verdict.Success => "Success",
        Verdict.Partial => "Mixed Fortunes",
        Verdict.Failure => "Setback",
        _ => "Disaster",
    };

    public static string WireName(Verdict verdict) => verdict switch
    {
        Verdict.CriticalSuccess => "critical_success",
        Verdict.Success => "success",
        Verdict.Partial => "partial",
        Verdict.Failure => "failure",
        _ => "critical_failure",
    };

    public static Verdict? FromWireName(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "critical_success" => Verdict.CriticalSuccess,
        "success" => Verdict.Success,
        "partial" => Verdict.Partial,
        "failure" => Verdict.Failure,
        "critical_failure" => Verdict.CriticalFailure,
        _ => null,
    };
}
