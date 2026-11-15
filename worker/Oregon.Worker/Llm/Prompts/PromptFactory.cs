using System.Globalization;
using System.Text.Json.Nodes;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Activities.Models;

namespace Oregon.Worker.Llm.Prompts;

/// <summary>
/// Builds the JSON-formatted prompts. Each payload follows the same shape: an <c>llm_task_description</c>,
/// the situation (e.g. <c>challenge_description</c>), a <c>context</c> block and, for judgments, the untrusted
/// <c>player_response</c> kept in its own field so the model can tell data from instructions.
/// </summary>
internal static class PromptFactory
{
    public const double JudgeTemperature = 0.4;
    public const double NarratorTemperature = 0.9;

    public static LlmRequest StorySeed(StorySeedRequest request)
    {
        JsonObject payload = new()
        {
            ["llm_task_description"] = "Invent a premise for a new voyage across the Oregon Reach: give the crew a reason to go, something precious aboard and a looming pressure, in 2-4 sentences. Also invent an evocative 2-6 word title.",
            ["tone"] = request.Tone.ToString(),
            ["player_hint"] = request.Hint ?? string.Empty,
        };
        return Narrator("story_seed", request.Tone, payload, LlmSchemas.StorySeed, temperature: 1.0, maxTokens: 1024);
    }

    public static LlmRequest World(WorldGenerationRequest request)
    {
        JsonArray waypoints = [];
        foreach (WorldNodeSketch node in request.Nodes.Where(n => n.Kind is not (NodeKind.Origin or NodeKind.Destination)))
        {
            waypoints.Add(new JsonObject { ["id"] = node.Id, ["kind"] = node.Kind.ToString(), ["leg"] = node.Column });
        }

        JsonObject payload = new()
        {
            ["llm_task_description"] =
                "Name and describe every waypoint on this voyage's route map, then write the prologue. Names must be original, 2-4 words and fit the waypoint kind " +
                "(Outpost = trading post, Settlement = homestead or camp, Derelict = wrecked ship, Hazard = natural danger, Raiders = outlaw hideout, " +
                "Anomaly = strange phenomenon, Landmark = famous natural waypoint). One vivid sentence per description. " +
                "The prologue is 3-5 sentences in second person, weaves in the story seed and ends with departure from Independence Ring. Return every id exactly once.",
            ["context"] = Context(request.Context),
            ["waypoints"] = waypoints,
        };
        return Narrator("world", request.Context.Tone, payload, LlmSchemas.World, NarratorTemperature, maxTokens: 4096);
    }

    public static LlmRequest Challenge(ChallengeRequest request)
    {
        JsonObject payload = new()
        {
            ["llm_task_description"] =
                "Present this challenge to the captain. Describe the situation in 2-4 sentences ending with what the captain must answer or do. " +
                "If a character is involved, name them and give them one line of dialogue; otherwise use empty strings. Do not mention numbers or game mechanics.",
            ["challenge_kind"] = request.Kind.ToString(),
            ["tested_attribute"] = request.Stat.ToString(),
            ["situation"] = request.Situation,
            ["stakes"] = Stakes(request.StakesOnSuccess, request.StakesOnFailure),
            ["context"] = Context(request.Context),
        };
        return Narrator("challenge", request.Context.Tone, payload, LlmSchemas.Challenge, NarratorTemperature, maxTokens: 1024);
    }

    public static LlmRequest Evaluation(EvaluationRequest request)
    {
        JsonObject payload = new()
        {
            ["llm_task_description"] =
                "Judge how well the captain's response handles the challenge. Award a verdict and score, explain the judgment in one out-of-character sentence, " +
                "narrate what happens in 2-3 sentences consistent with the stakes for that verdict, and give the counterpart's one-sentence reply (empty string if there is no counterpart).",
            ["challenge_description"] = $"{request.ChallengeTitle}: {request.ChallengeDescription}",
            ["challenge_kind"] = request.Kind.ToString(),
            ["counterpart"] = request.NpcName ?? string.Empty,
            ["stakes"] = Stakes(request.StakesOnSuccess, request.StakesOnFailure),
            ["character_aptitude"] = $"{request.Stat} level {request.StatLevel.ToString(CultureInfo.InvariantCulture)} of 10",
            ["context"] = Context(request.Context),
            ["player_response"] = request.PlayerResponse,
        };
        return new LlmRequest(
            LlmPurpose.Judge,
            "evaluation",
            GameMasterPrompt.For(LlmPurpose.Judge, request.Context.Tone),
            payload.ToJsonString(LlmJson.Options),
            LlmSchemas.Evaluation,
            JudgeTemperature,
            MaxOutputTokens: 4096);
    }

    public static LlmRequest Dilemma(DilemmaRequest request)
    {
        JsonArray options = [];
        foreach (DilemmaOptionBrief option in request.Options)
        {
            options.Add(new JsonObject { ["id"] = option.Id, ["archetype"] = option.Archetype, ["guidance"] = option.Guidance });
        }

        JsonObject payload = new()
        {
            ["llm_task_description"] =
                "Present this hazard with exactly three options. Write a 2-5 word title and a 2-3 sentence description. " +
                "For each option id, write an imperative label under 12 words that fits its archetype and guidance, plus a hint under 10 words. Never reveal numbers.",
            ["situation"] = request.Situation,
            ["options"] = options,
            ["context"] = Context(request.Context),
        };
        return Narrator("dilemma", request.Context.Tone, payload, LlmSchemas.Dilemma, NarratorTemperature, maxTokens: 1536);
    }

    public static LlmRequest Narration(NarrationRequest request)
    {
        JsonObject payload = new()
        {
            ["llm_task_description"] =
                $"Narrate {request.Purpose} in at most {request.MaxSentences.ToString(CultureInfo.InvariantCulture)} sentences. " +
                "Reflect every fact without contradicting it or inventing new consequences, and add a headline under 8 words.",
            ["facts"] = request.Facts,
            ["context"] = Context(request.Context),
        };
        return Narrator("narration", request.Context.Tone, payload, LlmSchemas.Narration, NarratorTemperature, maxTokens: 1536);
    }

    private static LlmRequest Narrator(string schemaName, GameTone tone, JsonObject payload, string schema, double temperature, int maxTokens) =>
        new(
            LlmPurpose.Narrator,
            schemaName,
            GameMasterPrompt.For(LlmPurpose.Narrator, tone),
            payload.ToJsonString(LlmJson.Options),
            schema,
            temperature,
            maxTokens);

    private static JsonObject Stakes(string onSuccess, string onFailure) => new()
    {
        ["on_success"] = onSuccess,
        ["on_failure"] = onFailure,
    };

    private static JsonObject Context(StoryContext context)
    {
        JsonArray allies = [.. context.Allies.Select(a => (JsonNode?)JsonValue.Create(a))];
        JsonArray flags = [.. context.StoryFlags.Select(f => (JsonNode?)JsonValue.Create(f))];
        JsonArray recent = [.. context.RecentEvents.Select(e => (JsonNode?)JsonValue.Create(e))];
        return new JsonObject
        {
            ["voyage_title"] = context.Title,
            ["story_seed"] = context.StorySeed,
            ["tone"] = context.Tone.ToString(),
            ["difficulty"] = context.Difficulty.ToString(),
            ["captain"] = context.CaptainName,
            ["ship"] = context.ShipName,
            ["day"] = context.Day,
            ["location"] = context.LocationName,
            ["location_description"] = context.LocationDescription,
            ["supplies"] = new JsonObject
            {
                ["fuel"] = context.Resources.Fuel,
                ["rations"] = context.Resources.Rations,
                ["hull"] = context.Resources.Hull,
                ["morale"] = context.Resources.Morale,
                ["scrip"] = context.Resources.Scrip,
                ["crew"] = context.Resources.Crew,
            },
            ["allies"] = allies,
            ["story_so_far"] = flags,
            ["recent_events"] = recent,
        };
    }
}
