using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Result of resolving a challenge, dilemma or skirmish.</summary>
internal sealed record Resolution(Verdict Verdict, List<ResourceChangeView> Changes, AllyState? AllyJoined, string Summary);
