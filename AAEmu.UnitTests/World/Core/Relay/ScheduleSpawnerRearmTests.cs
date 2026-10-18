using AAEmu.Game.Models.Game.NPChar;
using AAEmu.World.Core.Relay;

namespace AAEmu.UnitTests.World.Core.Relay;

public class ScheduleSpawnerRearmTests
{
    [Test]
    public async Task ScheduleOpening_DoesNotArmUnrelatedCompletedGuardianPlacements()
    {
        ZoneSpawnerPlacementCatalog.SpawnerPlacement[] placements =
        [
            new(236546, 24220, 400, 395, 152, 0),
            new(236455, 24221, 401, 359, 152, 0),
            new(900, 77, 400, 395, 152, 0),
            new(901, 77, 410, 395, 152, 0)
        ];
        var selected = ScheduleSpawnerRearm.SelectPlacements(placements, new HashSet<uint> { 77 });
        await Assert.That(selected.Count).IsEqualTo(2);
        await Assert.That(selected.Contains(900u)).IsTrue();
        await Assert.That(selected.Contains(901u)).IsTrue();
        await Assert.That(selected.Contains(236546u)).IsFalse();
        await Assert.That(selected.Contains(236455u)).IsFalse();
    }

    [Test]
    public async Task NoMatchingSchedulePlacements_IsANoOp_AndDuplicatePlacementsAreCollapsed()
    {
        ZoneSpawnerPlacementCatalog.SpawnerPlacement[] placements =
        [new(900, 77, 0, 0, 0, 0), new(900, 77, 0, 0, 0, 0)];
        await Assert.That(ScheduleSpawnerRearm.SelectPlacements(placements, new HashSet<uint> { 88 }).Count)
            .IsEqualTo(0);
        await Assert.That(ScheduleSpawnerRearm.SelectPlacements(placements, new HashSet<uint> { 77 }).Count)
            .IsEqualTo(1);
    }

    [Test]
    public async Task ActivationRequest_AddressesOnePlacement_WithValidNpcIdentity()
    {
        var creator = new Npc { ObjId = 123, TemplateId = 456 };
        var request = ScheduleSpawnerRearm.CreateRequest(creator, 900);
        await Assert.That(request.SpawnerId).IsEqualTo(900u);
        await Assert.That(request.CreatorObjId).IsEqualTo(123u);
        await Assert.That(request.CreatorTemplateId).IsEqualTo(456u);
        await Assert.That(request.Event).IsEqualTo(NpcSpawnerEvent.Activate);
        await Assert.That(request.DespawnOnCreatorDeath).IsFalse();
    }
}
