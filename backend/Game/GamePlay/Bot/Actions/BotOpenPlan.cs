using Shared;

namespace Game.GamePlay;

public readonly record struct BotOpenPlan(IReadOnlyList<Position> Seeds, int Gain, string Reason);
