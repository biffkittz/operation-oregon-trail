namespace Oregon.Contracts.Model;

/// <summary>Ship supplies and condition. All values except <paramref name="Scrip"/> and <paramref name="Crew"/> are 0-100.</summary>
/// <param name="Fuel">Fuel cells remaining.</param>
/// <param name="Rations">Food remaining.</param>
/// <param name="Hull">Hull integrity percentage.</param>
/// <param name="Morale">Crew morale.</param>
/// <param name="Scrip">Money.</param>
/// <param name="Crew">People aboard, including the captain and allies.</param>
public sealed record ResourcesView(int Fuel, int Rations, int Hull, int Morale, int Scrip, int Crew);
