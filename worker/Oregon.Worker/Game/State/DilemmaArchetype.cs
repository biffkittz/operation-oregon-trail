namespace Oregon.Worker.Game.State;

/// <summary>
/// Mechanical archetype behind a dilemma option. The engine picks archetypes (and therefore consequences);
/// the LLM only writes in-context labels for them, so story branching stays balanced and testable.
/// </summary>
internal enum DilemmaArchetype
{
    /// <summary>Risky push forward (Resilience roll).</summary>
    Bold,

    /// <summary>Safe but costly in time and food.</summary>
    Cautious,

    /// <summary>Help others at a cost; may gain an ally.</summary>
    Compassionate,

    /// <summary>Turn the situation into profit (Diplomacy roll).</summary>
    Mercantile,

    /// <summary>Engineer a way through (Ingenuity roll).</summary>
    Inventive,

    /// <summary>An unorthodox, imaginative gambit (Creativity roll).</summary>
    Daring,
}
