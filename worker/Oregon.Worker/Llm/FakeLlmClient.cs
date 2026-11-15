using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Oregon.Contracts.Model;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Rules;

namespace Oregon.Worker.Llm;

/// <summary>
/// Deterministic offline stand-in for Gemini. It answers from the same canned content the workflow uses when the
/// real LLM is down, so contributors can play the whole game without an API key and tests never hit the network.
/// </summary>
internal sealed class FakeLlmClient : ILlmClient
{
    public Task<JsonElement> GenerateJsonAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonNode payload = JsonNode.Parse(request.UserPayloadJson) ?? new JsonObject();

        // Seeded from the prompt text so identical prompts get identical answers (stable tests, reproducible bugs).
        Random random = new(StableHash(request.UserPayloadJson));
        JsonObject answer = request.SchemaName switch
        {
            "story_seed" => StorySeed(payload, random),
            "world" => World(payload, random),
            "challenge" => Challenge(payload),
            "evaluation" => Evaluation(payload),
            "dilemma" => Dilemma(payload),
            "narration" => Narration(payload),
            _ => throw new LlmException($"The fake LLM has no canned answer for '{request.SchemaName}'.", isTransient: false),
        };
        return Task.FromResult(JsonSerializer.SerializeToElement(answer));
    }

    private static JsonObject StorySeed(JsonNode payload, Random random)
    {
        GameTone tone = Enum.TryParse(Text(payload, "tone"), out GameTone parsed) ? parsed : GameTone.Gritty;
        Contracts.Workflows.StorySeedResult seed = FallbackContent.StorySeed(tone, random);
        return new JsonObject { ["title"] = seed.Title, ["seed"] = seed.Seed };
    }

    private static JsonObject World(JsonNode payload, Random random)
    {
        JsonArray nodes = [];
        HashSet<string> used = new(StringComparer.Ordinal);
        foreach (JsonNode? waypoint in payload["waypoints"]?.AsArray() ?? [])
        {
            NodeKind kind = Enum.TryParse(Text(waypoint, "kind"), out NodeKind parsedKind) ? parsedKind : NodeKind.Landmark;
            string name = NameBank.PlaceholderName(kind, random);
            if (!used.Add(name))
            {
                name = $"{name} {used.Count.ToString(CultureInfo.InvariantCulture)}";
                used.Add(name);
            }

            nodes.Add(new JsonObject
            {
                ["id"] = Text(waypoint, "id"),
                ["name"] = name,
                ["description"] = NameBank.PlaceholderDescription(kind),
            });
        }

        JsonNode? context = payload["context"];
        return new JsonObject
        {
            ["prologue"] = FallbackContent.Prologue(Text(context, "captain"), Text(context, "ship"), Text(context, "story_seed")),
            ["nodes"] = nodes,
        };
    }

    private static JsonObject Challenge(JsonNode payload)
    {
        ChallengeKind kind = Enum.TryParse(Text(payload, "challenge_kind"), out ChallengeKind parsed) ? parsed : ChallengeKind.Hardship;
        ChallengeContent content = FallbackContent.Challenge(kind, Text(payload, "situation"), Text(payload["context"], "location"));
        return new JsonObject
        {
            ["title"] = content.Title,
            ["description"] = content.Description,
            ["npc_name"] = content.NpcName,
            ["npc_line"] = content.NpcLine,
        };
    }

    private static JsonObject Evaluation(JsonNode payload)
    {
        // character_aptitude looks like "Diplomacy level 4 of 10".
        string aptitude = Text(payload, "character_aptitude");
        string[] words = aptitude.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int level = words.Length > 2 && int.TryParse(words[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 2;
        string counterpart = Text(payload, "counterpart");

        EvaluationResult result = HeuristicJudge.Judge(Text(payload, "player_response"), counterpart.Length == 0 ? null : counterpart, level);
        return new JsonObject
        {
            ["verdict"] = VerdictText.WireName(result.Verdict),
            ["score"] = result.Score,
            ["reasoning"] = result.Reasoning,
            ["narrative"] = result.Narrative,
            ["npc_reply"] = counterpart.Length == 0 ? string.Empty : result.NpcReply,
            ["manipulation_detected"] = result.ManipulationDetected,
        };
    }

    private static JsonObject Dilemma(JsonNode payload)
    {
        List<(string Id, string Archetype)> options = [.. (payload["options"]?.AsArray() ?? [])
            .Select(o => (Text(o, "id"), Text(o, "archetype")))];
        DilemmaContent content = FallbackContent.Dilemma(Text(payload, "situation"), options);
        JsonArray written = [.. content.Options.Select(o => (JsonNode?)new JsonObject { ["id"] = o.Id, ["label"] = o.Label, ["hint"] = o.Hint })];
        return new JsonObject { ["title"] = content.Title, ["description"] = content.Description, ["options"] = written };
    }

    private static JsonObject Narration(JsonNode payload)
    {
        string task = Text(payload, "llm_task_description");
        NarrationResult narration = FallbackContent.Narrate(task, Text(payload, "facts"));
        return new JsonObject { ["headline"] = narration.Headline, ["text"] = narration.Text };
    }

    private static string Text(JsonNode? node, string property) =>
        node?[property]?.GetValueKind() == JsonValueKind.String ? node[property]!.GetValue<string>() : string.Empty;

    /// <summary>FNV-1a; unlike <see cref="string.GetHashCode()"/> it is stable across processes.</summary>
    private static int StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(value))
            {
                hash = (hash ^ b) * 16777619;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
