using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.UnitTests.Game.Models.Game.Skills;

public class InstanceStartAreaTargetsTests
{
    [Test]
    public async Task StartScript_OneEffectTarget_StillSignalsEntrance()
    {
        var source = new Unit { ObjId = 1 };
        var entrance = new Doodad { ObjId = 2 };
        entrance.Transform.Local.SetPosition(10, 0, 0);
        var selected = InstanceStartAreaTargets.Select([source, entrance], source, 1, true);
        await Assert.That(selected.EffectTargets.Count).IsEqualTo(1);
        await Assert.That(selected.EffectTargets[0]).IsSameReferenceAs(source);
        await Assert.That(selected.DoodadHits.Count).IsEqualTo(1);
        await Assert.That(selected.DoodadHits[0]).IsSameReferenceAs(entrance);
    }

    [Test]
    public async Task OrdinaryCast_DoesNotSignalOutsideItsTargetCap()
    {
        var source = new Unit { ObjId = 1 };
        var entrance = new Doodad { ObjId = 2 };
        var selected = InstanceStartAreaTargets.Select([source, entrance], source, 1, false);
        await Assert.That(selected.EffectTargets.Count).IsEqualTo(1);
        await Assert.That(selected.DoodadHits.Count).IsEqualTo(0);
    }

    [Test]
    public async Task StartScript_DuplicateCandidate_IsSignalledOnce()
    {
        var source = new Unit { ObjId = 1 };
        var entrance = new Doodad { ObjId = 2 };
        var selected = InstanceStartAreaTargets.Select([source, entrance, entrance], source, 0, true);
        await Assert.That(selected.DoodadHits.Count).IsEqualTo(1);
    }
}
