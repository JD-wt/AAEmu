using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills;

public static class InstanceStartAreaTargets
{
    // Candidates have already passed the cast's radius, relation and shape filters.
    // 49021 caps effects at one (the source), but its nearby entrance listens for this skill.
    public static (List<BaseUnit> EffectTargets, List<Doodad> DoodadHits) Select(
        List<BaseUnit> candidates, BaseUnit origin, int targetCount, bool signalDoodads)
    {
        var hits = signalDoodads ? candidates.OfType<Doodad>().Distinct().ToList() : [];
        var targets = targetCount > 0 && candidates.Count > targetCount
            ? candidates.OrderBy(t => t.GetDistanceTo(origin)).Take(targetCount).ToList()
            : candidates;
        return (targets, hits);
    }
}
