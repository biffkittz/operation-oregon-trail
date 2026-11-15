using Oregon.Contracts;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Llm.Wire;

namespace Oregon.Worker.Llm;

/// <summary>
/// Converts raw model output into activity results. Everything is sanitized and length-bounded here, and
/// structurally invalid output raises a transient <see cref="LlmException"/> so Temporal asks for a fresh sample.
/// </summary>
internal static class LlmOutputMapper
{
    private const int ShortText = 80;
    private const int NameLength = 48;
    private const int LineLength = 320;
    private const int ParagraphLength = 1200;
    private const int PrologueLength = 1600;

    public static StorySeedResult ToStorySeed(WireStorySeed wire)
    {
        string title = TextSanitizer.Clean(wire.Title, ShortText);
        string seed = TextSanitizer.Clean(wire.Seed, GameLimits.MaxStorySeedLength);
        return title.Length == 0 || seed.Length == 0 ? throw Malformed("story seed") : new StorySeedResult(title, seed);
    }

    public static WorldFlavor ToWorld(WireWorld wire, WorldGenerationRequest request)
    {
        string prologue = TextSanitizer.Clean(wire.Prologue, PrologueLength);
        if (prologue.Length == 0)
        {
            throw Malformed("world prologue");
        }

        var byId = (wire.Nodes ?? [])
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .GroupBy(n => n.Id!.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Unknown IDs are ignored and missing ones keep their placeholder names, so a sloppy answer degrades gracefully.
        List<NodeFlavor> nodes = [.. request.Nodes.Select(sketch =>
        {
            byId.TryGetValue(sketch.Id, out WireNode? node);
            string name = TextSanitizer.Clean(node?.Name, NameLength);
            string description = TextSanitizer.Clean(node?.Description, LineLength);
            return new NodeFlavor(sketch.Id, name.Length == 0 ? sketch.PlaceholderName : name, description);
        })];
        return new WorldFlavor(prologue, nodes);
    }

    public static ChallengeContent ToChallenge(WireChallenge wire)
    {
        string title = TextSanitizer.Clean(wire.Title, ShortText);
        string description = TextSanitizer.Clean(wire.Description, ParagraphLength);
        return title.Length == 0 || description.Length == 0
            ? throw Malformed("challenge")
            : new ChallengeContent(title, description, TextSanitizer.Clean(wire.NpcName, NameLength), TextSanitizer.Clean(wire.NpcLine, LineLength));
    }

    public static EvaluationResult ToEvaluation(WireEvaluation wire)
    {
        Contracts.Model.Verdict verdict = VerdictText.FromWireName(wire.Verdict) ?? throw Malformed("evaluation verdict");
        string narrative = TextSanitizer.Clean(wire.Narrative, ParagraphLength);
        return narrative.Length == 0
            ? throw Malformed("evaluation narrative")
            : new EvaluationResult(
                verdict,
                Math.Clamp(wire.Score ?? 0, 0, 100),
                TextSanitizer.Clean(wire.Reasoning, LineLength),
                narrative,
                TextSanitizer.Clean(wire.NpcReply, LineLength),
                wire.ManipulationDetected ?? false);
    }

    public static DilemmaContent ToDilemma(WireDilemma wire)
    {
        string title = TextSanitizer.Clean(wire.Title, ShortText);
        string description = TextSanitizer.Clean(wire.Description, ParagraphLength);
        if (title.Length == 0 || description.Length == 0)
        {
            throw Malformed("dilemma");
        }

        List<DilemmaOptionContent> options = [.. (wire.Options ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o.Id))
            .Select(o => new DilemmaOptionContent(o.Id!.Trim().ToUpperInvariant(), TextSanitizer.Clean(o.Label, ShortText), TextSanitizer.Clean(o.Hint, ShortText)))];
        return new DilemmaContent(title, description, options);
    }

    public static NarrationResult ToNarration(WireNarration wire)
    {
        string text = TextSanitizer.Clean(wire.Text, ParagraphLength);
        string headline = TextSanitizer.Clean(wire.Headline, ShortText);
        return text.Length == 0 ? throw Malformed("narration") : new NarrationResult(headline.Length == 0 ? "The Trail Goes On" : headline, text);
    }

    private static LlmException Malformed(string what) => new($"The model returned an incomplete {what}.", isTransient: true);
}
