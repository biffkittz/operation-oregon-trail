namespace Oregon.Contracts.Model;

/// <summary>Use a service while docked at an outpost.</summary>
/// <param name="Service">Which service.</param>
/// <param name="Quantity">Units of ten for purchases; ignored for repairs and haggling.</param>
public sealed record OutpostCommand(OutpostService Service, int Quantity = 1);
