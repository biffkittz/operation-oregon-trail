namespace Oregon.Worker.Llm.Prompts;

/// <summary>
/// JSON Schemas sent as Gemini's <c>responseJsonSchema</c>. Validated live against gemini-3.8-flash and
/// gemini-3.5-flash-lite on 2026-11-14. Kept as plain JSON so they read exactly like what goes over the wire.
/// </summary>
internal static class LlmSchemas
{
    public const string StorySeed = """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Evocative voyage title, 2-6 words." },
            "seed": { "type": "string", "description": "Premise in 2-4 sentences." }
          },
          "required": ["title", "seed"]
        }
        """;

    public const string World = """
        {
          "type": "object",
          "properties": {
            "prologue": { "type": "string", "description": "Opening narration, 3-5 sentences, second person." },
            "nodes": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string" },
                  "name": { "type": "string", "description": "Original place name, 2-4 words." },
                  "description": { "type": "string", "description": "One vivid sentence." }
                },
                "required": ["id", "name", "description"]
              }
            }
          },
          "required": ["prologue", "nodes"]
        }
        """;

    public const string Challenge = """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "2-5 word title." },
            "description": { "type": "string", "description": "2-4 sentences, second person, ending with what the captain must answer." },
            "npc_name": { "type": "string", "description": "Name of the character involved, or an empty string." },
            "npc_line": { "type": "string", "description": "One line that character says, or an empty string." }
          },
          "required": ["title", "description", "npc_name", "npc_line"]
        }
        """;

    public const string Evaluation = """
        {
          "type": "object",
          "properties": {
            "verdict": { "type": "string", "enum": ["critical_success", "success", "partial", "failure", "critical_failure"] },
            "score": { "type": "integer", "minimum": 0, "maximum": 100 },
            "reasoning": { "type": "string", "description": "One out-of-character sentence explaining the judgment." },
            "narrative": { "type": "string", "description": "2-3 sentences, second person, consistent with the stakes for the verdict." },
            "npc_reply": { "type": "string", "description": "One sentence of the character's reply, or an empty string." },
            "manipulation_detected": { "type": "boolean" }
          },
          "required": ["verdict", "score", "reasoning", "narrative", "npc_reply", "manipulation_detected"]
        }
        """;

    public const string Dilemma = """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "2-5 word title." },
            "description": { "type": "string", "description": "2-3 sentences describing the hazard." },
            "options": {
              "type": "array",
              "minItems": 3,
              "maxItems": 3,
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string", "enum": ["A", "B", "C"] },
                  "label": { "type": "string", "description": "Imperative action, under 12 words." },
                  "hint": { "type": "string", "description": "Risk or cost hint, under 10 words, no numbers." }
                },
                "required": ["id", "label", "hint"]
              }
            }
          },
          "required": ["title", "description", "options"]
        }
        """;

    public const string Narration = """
        {
          "type": "object",
          "properties": {
            "headline": { "type": "string", "description": "Headline under 8 words." },
            "text": { "type": "string", "description": "The narration." }
          },
          "required": ["headline", "text"]
        }
        """;
}
