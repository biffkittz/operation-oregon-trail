using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.State;

/// <summary>Ship supplies. Fuel, rations, hull and morale are 0-100; scrip and crew are unbounded counts.</summary>
internal sealed class ResourcePool
{
    public int Fuel { get; set; }

    public int Rations { get; set; }

    public int Hull { get; set; } = 100;

    public int Morale { get; set; } = 70;

    public int Scrip { get; set; }

    public int Crew { get; set; }

    public ResourcesView ToView() => new(Fuel, Rations, Hull, Morale, Scrip, Crew);
}
