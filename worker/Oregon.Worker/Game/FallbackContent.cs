using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game;

/// <summary>
/// Deterministic content used when an LLM activity exhausts its retries, so a Gemini outage degrades the
/// prose instead of freezing a save. Everything here is pure and safe to call from workflow code.
/// </summary>
internal static class FallbackContent
{
    private static readonly Dictionary<GameTone, (string Title, string Seed)[]> Seeds = new()
    {
        [GameTone.Whimsical] =
        [
            ("The Great Goat Migration", "A traveling circus of zero-gravity goats has been granted a homestead at Willamette Landing, provided they arrive before the spring equinox. You are their reluctant ringmaster."),
            ("Pies for the Frontier", "Your grandmother's legendary pie recipe is the only thing standing between the Reach colonies and a very sad harvest festival. Deliver it, and the ovens, intact."),
        ],
        [GameTone.Hopeful] =
        [
            ("Seeds of Willamette", "You carry the last seed vault of a dying orbital farm. Somewhere past the Cascade Rift, Willamette Landing promises soil, sky and a second chance."),
            ("The Teacher's Wagon", "A schoolhouse bolted to a hauler, thirty children, and a promise that every one of them will see a real sunrise on a new world."),
        ],
        [GameTone.Gritty] =
        [
            ("Debts and Dust", "You mortgaged everything for this rust-bucket wagon. The bank wants its money, the claim-jumpers want your cargo, and Willamette Landing is a long way off."),
            ("The Ore Rush", "Rumors of a sapphire vein near Willamette Landing emptied the station overnight. You're late, low on fuel and out of patience."),
        ],
        [GameTone.Morose] =
        [
            ("Ashes to the Reach", "Your home ring burned. What remains of its families fits in one wagon, and the trail ahead has swallowed better crews than yours."),
            ("The Long Quiet", "The radio went silent three weeks ago. You fly on toward Willamette Landing because there is nowhere left to turn back to."),
        ],
    };

    public static StorySeedResult StorySeed(StorySeedRequest request, Random random) => StorySeed(request.Tone, random);

    public static StorySeedResult StorySeed(GameTone tone, Random random)
    {
        (string title, string seed)[] options = Seeds.TryGetValue(tone, out (string, string)[]? found) ? found : Seeds[GameTone.Gritty];
        (string title, string seed) = options[random.Next(options.Length)];
        return new StorySeedResult(title, seed);
    }

    public static WorldFlavor World(WorldGenerationRequest request) => new(
        Prologue(request.Context.CaptainName, request.Context.ShipName, request.Context.StorySeed),
        [.. request.Nodes.Select(n => new NodeFlavor(n.Id, n.PlaceholderName, NameBank.PlaceholderDescription(n.Kind)))]);

    public static string Prologue(string captain, string ship, string storySeed) =>
        $"Independence Ring, day one. {captain} walks the length of the {ship} one last time before departure. " +
        $"{storySeed} Beyond the docking clamps the Oregon Reach waits, enormous and indifferent.";

    public static ChallengeContent Challenge(ChallengeRequest request) =>
        Challenge(request.Kind, request.Situation, request.Context.LocationName);

    public static ChallengeContent Challenge(ChallengeKind kind, string situationText, string location)
    {
        string situation = Capitalize(situationText);
        (string title, string npc, string line) = kind switch
        {
            ChallengeKind.Persuasion => ("A Hard Sell", "Elder Prudence", "We don't feed strangers for free. Convince me."),
            ChallengeKind.Morality => ("A Question of Justice", "Marshal Hobbs", "Well, captain? What's it going to be?"),
            ChallengeKind.Ingenuity => ("Sealed Tight", string.Empty, string.Empty),
            ChallengeKind.Creativity => ("An Audience of Light", string.Empty, string.Empty),
            ChallengeKind.Haggle => ("The Smith's Price", "Old Marta", "Price is the price, captain. Unless you've got a reason it shouldn't be."),
            _ => ("Trouble in the Black", "First Mate Ruiz", "Captain! Orders!"),
        };
        return new ChallengeContent(title, $"{situation}. At {location}, every eye turns to you.", npc, line);
    }

    public static EvaluationResult Judge(EvaluationRequest request) => HeuristicJudge.Judge(request);

    public static DilemmaContent Dilemma(DilemmaRequest request) =>
        Dilemma(request.Situation, [.. request.Options.Select(o => (o.Id, o.Archetype))]);

    public static DilemmaContent Dilemma(string situation, IReadOnlyList<(string Id, string Archetype)> options) => new(
        "Fork in the Trail",
        $"{Capitalize(situation)}. There are three ways forward, and none of them are free.",
        [.. options.Select(o =>
        {
            DilemmaArchetype archetype = Enum.TryParse(o.Archetype, out DilemmaArchetype parsed) ? parsed : DilemmaArchetype.Cautious;
            return new DilemmaOptionContent(o.Id, DilemmaRules.DefaultLabel(archetype), DilemmaRules.DefaultHint(archetype));
        })]);

    public static NarrationResult Narrate(NarrationRequest request) => Narrate(request.Purpose, request.Facts);

    public static NarrationResult Narrate(string purpose, string facts) => new(HeadlineFor(purpose), facts);

    private static string HeadlineFor(string purpose)
    {
        string lower = purpose.ToLowerInvariant();
        if (lower.Contains("victory", StringComparison.Ordinal))
        {
            return "Journey's End";
        }

        if (lower.Contains("defeat", StringComparison.Ordinal))
        {
            return "The Trail Ends Here";
        }

        if (lower.Contains("raider", StringComparison.Ordinal) || lower.Contains("skirmish", StringComparison.Ordinal))
        {
            return "Raiders!";
        }

        return lower.Contains("arrival", StringComparison.Ordinal) ? "Safe Harbor" : "The Trail Goes On";
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
