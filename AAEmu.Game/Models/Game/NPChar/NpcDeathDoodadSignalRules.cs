using AAEmu.Game.Models.Game.Skills.Templates;

namespace AAEmu.Game.Models.Game.NPChar;

public static class NpcDeathDoodadSignalRules
{
    // Death skills can carry both area signals and summon/combat effects. Signal only their
    // DoodadFuncSkillHit listeners; never replay the skill's effects or cast through Zone.
    public static bool ShouldSignal(bool zoneAuthority, bool isZoneMirror, bool isDungeonCopy,
        SkillTemplate template) =>
        zoneAuthority && isZoneMirror && isDungeonCopy && template != null &&
        template.Plot == null && !template.PlotOnly &&
        template.TargetAreaRadius > 0;
}
