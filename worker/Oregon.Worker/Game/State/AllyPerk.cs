namespace Oregon.Worker.Game.State;

/// <summary>Mechanical bonus granted by an ally.</summary>
internal enum AllyPerk
{
    /// <summary>Restores hull after each leg.</summary>
    Mechanic,

    /// <summary>Reduces ration consumption.</summary>
    Quartermaster,

    /// <summary>Reduces fuel consumption.</summary>
    Navigator,

    /// <summary>Prevents the next crew loss.</summary>
    Medic,

    /// <summary>Adds a level to Diplomacy checks.</summary>
    Envoy,

    /// <summary>Improves skirmish scoring.</summary>
    Gunner,
}
