using AAEmu.Game;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.World.Core.Network;
using AAEmu.World.Core.Packets.Wz;
using AAEmu.World.Core.Zone;
using NLog;

namespace AAEmu.World.Core.Relay;

internal static class ScheduleSpawnerRearm
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    internal static IReadOnlyList<uint> SelectPlacements(
        IEnumerable<ZoneSpawnerPlacementCatalog.SpawnerPlacement> placements,
        IReadOnlySet<uint> openedTypes) =>
        placements.Where(p => openedTypes.Contains(p.SpawnerType))
            .Select(p => p.PlacementId).Distinct().ToArray();

    public static void Apply(ZoneConnection zone, IReadOnlySet<uint> openedTypes)
    {
        var placements = ZoneSpawnerPlacementCatalog.GetAll(zone.ZoneId)
            .Where(p => openedTypes.Contains(p.SpawnerType) && !NpcScheduleGate.IsClosed(p.SpawnerType) &&
                        !TowerDefGameData.Instance.IsTowerDefEventSpawner(p.SpawnerType));
        var targets = SelectPlacements(placements, openedTypes);
        if (targets.Count == 0)
            return;

        var world = WorldManager.Instance.GetWorld(zone.InstanceId);
        BaseUnit creator = world?.GetAllCharacters().FirstOrDefault(ch =>
            ch.Transform.ZoneId == zone.ZoneId && zone.Units.Contains(ch.ObjId));
        creator ??= world?.GetAllNpcs().FirstOrDefault(npc =>
            !npc.IsDead && npc.Transform.ZoneId == zone.ZoneId && zone.Units.Contains(npc.ObjId));
        if (creator == null)
        {
            // Never substitute a whole-zone activate: it revives completed script spawners.
            // A later player enter still performs the ordinary initial population.
            Logger.Warn("Schedule re-arm deferred: no live creator zone={0} instance={1} placements={2}",
                zone.ZoneId, zone.InstanceId, targets.Count);
            return;
        }

        foreach (var placementId in targets)
            zone.SendPacket(new WZNpcSpawnerEventPacket(CreateRequest(creator, placementId)));
        Logger.Info("Schedule re-arm zone={0} instance={1} placements={2}",
            zone.ZoneId, zone.InstanceId, targets.Count);
    }

    internal static WorldNpcSpawnerEventRequest CreateRequest(BaseUnit creator, uint placementId) =>
        new(creator.ObjId, creator is Character ? BaseUnitType.Character : BaseUnitType.Npc,
            creator is Character ch ? ch.Id : 0UL, 0L, creator.TemplateId,
            creator is Npc npc ? npc.OwnerId : 0UL,
            creator is Npc n ? n.UnitStateFlag : (byte)0,
            placementId, NpcSpawnerEvent.Activate, NpcSpawnerEventType.Default, 0f, false, false);
}
