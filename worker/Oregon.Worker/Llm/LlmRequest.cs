namespace Oregon.Worker.Llm;

/// <summary>A structured-output request.</summary>
/// <param name="Purpose">Model profile.</param>
/// <param name="SchemaName">Logical response type (used for logging and by the fake client).</param>
/// <param name="SystemInstruction">Game-master rules and tone.</param>
/// <param name="UserPayloadJson">JSON-formatted prompt (task description, context, untrusted player text).</param>
/// <param name="ResponseSchemaJson">JSON Schema the response must match.</param>
/// <param name="Temperature">Sampling temperature.</param>
/// <param name="MaxOutputTokens">Upper bound on response tokens.</param>
public sealed record LlmRequest(
    LlmPurpose Purpose,
    string SchemaName,
    string SystemInstruction,
    string UserPayloadJson,
    string ResponseSchemaJson,
    double Temperature,
    int MaxOutputTokens);
