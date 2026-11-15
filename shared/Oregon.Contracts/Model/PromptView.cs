namespace Oregon.Contracts.Model;

/// <summary>
/// What the player can do right now. Exactly the member matching <see cref="GameView.Phase"/> is populated,
/// except <see cref="Outpost"/>, which accompanies <see cref="Routes"/> while docked at an outpost.
/// </summary>
/// <remarks>A flat record of nullable members avoids polymorphic JSON across Temporal, HTTP and SignalR.</remarks>
/// <param name="Narrative">Card to acknowledge (phase Narrative).</param>
/// <param name="Routes">Next legs (phase ChoosingRoute).</param>
/// <param name="Outpost">Outpost services (phase ChoosingRoute at an outpost).</param>
/// <param name="Challenge">Free-text challenge (phase AwaitingResponse).</param>
/// <param name="Dilemma">Three-way choice (phase AwaitingChoice).</param>
/// <param name="Skirmish">Click battle (phase Skirmish).</param>
/// <param name="EmergencyFuel">Hauler offer (phase ChoosingRoute, stranded at a stop without a fuel depot).</param>
public sealed record PromptView(
    NarrativePromptView? Narrative = null,
    IReadOnlyList<RouteOptionView>? Routes = null,
    OutpostPromptView? Outpost = null,
    ChallengePromptView? Challenge = null,
    DilemmaPromptView? Dilemma = null,
    SkirmishPromptView? Skirmish = null,
    EmergencyFuelOfferView? EmergencyFuel = null);
