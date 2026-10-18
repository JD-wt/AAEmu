using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Indun.Events;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.UnitTests.Game.Models.Game.Indun;

public class IndunDoodadSpawnEventTests
{
    [Test]
    public async Task HiddenExitBootstrap_DoesNotCompleteRoom_ForAnyAuthoredActivePhase()
    {
        var world = new WorldInstance(new WorldTemplate(), 0, true, 100);
        var portal = PortalAtPhase(51370);
        var conditions = Enumerable.Range(51371, 5).Select(phase => new RecordingEvent
        {
            DoodadAlmightyId = 17510, DoodadFuncGroupId = (uint)phase
        }).ToArray();
        foreach (var condition in conditions)
            condition.Subscribe(world);

        world.Events.OnDoodadSpawn(world, new OnDoodadSpawnArgs { Doodad = portal });
        await Assert.That(conditions.Sum(condition => condition.Executions)).IsEqualTo(0);

        world.Events.OnDoodadPhaseChanged(world, new OnDoodadPhaseChangedArgs
        {
            Doodad = portal, FuncGroupId = 51374
        });
        await Assert.That(conditions.Sum(condition => condition.Executions)).IsEqualTo(1);
        await Assert.That(conditions[3].Executions).IsEqualTo(1);
    }

    [Test]
    public async Task SpawnInRequiredPhase_StillRunsCompletion()
    {
        var world = new WorldInstance(new WorldTemplate(), 0, true, 100);
        var condition = new RecordingEvent { DoodadAlmightyId = 17510, DoodadFuncGroupId = 51373 };
        condition.Subscribe(world);
        world.Events.OnDoodadSpawn(world, new OnDoodadSpawnArgs
        {
            Doodad = PortalAtPhase(51373)
        });
        await Assert.That(condition.Executions).IsEqualTo(1);
    }

    [Test]
    public async Task PhaseLessSpawnCondition_DoesNotFireOnSubsequentPhaseChanges()
    {
        var world = new WorldInstance(new WorldTemplate(), 0, true, 100);
        var condition = new RecordingEvent { DoodadAlmightyId = 17510 };
        var portal = new Doodad { TemplateId = 17510 };
        condition.Subscribe(world);
        world.Events.OnDoodadSpawn(world, new OnDoodadSpawnArgs { Doodad = portal });
        world.Events.OnDoodadPhaseChanged(world, new OnDoodadPhaseChangedArgs
        {
            Doodad = portal, FuncGroupId = 51373
        });
        await Assert.That(condition.Executions).IsEqualTo(1);
    }

    [Test]
    public async Task Unsubscribe_RemovesBothHandlers_AndOtherTemplatesNeverMatch()
    {
        var world = new WorldInstance(new WorldTemplate(), 0, true, 100);
        var condition = new RecordingEvent { DoodadAlmightyId = 17510, DoodadFuncGroupId = 51373 };
        condition.Subscribe(world);
        world.Events.OnDoodadPhaseChanged(world, new OnDoodadPhaseChangedArgs
        {
            Doodad = new Doodad { TemplateId = 17511 }, FuncGroupId = 51373
        });
        condition.UnSubscribe(world);
        var portal = PortalAtPhase(51373);
        world.Events.OnDoodadSpawn(world, new OnDoodadSpawnArgs { Doodad = portal });
        world.Events.OnDoodadPhaseChanged(world, new OnDoodadPhaseChangedArgs
        {
            Doodad = portal, FuncGroupId = 51373
        });
        await Assert.That(condition.Executions).IsEqualTo(0);
    }

    private static Doodad PortalAtPhase(uint phase)
    {
        // Seed only the event input; the live phase setter also persists and resolves manager data.
        var portal = new Doodad { TemplateId = 17510 };
        typeof(Doodad).GetField("_funcGroupId", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.SetValue(portal, phase);
        return portal;
    }

    private sealed class RecordingEvent : IndunEventDoodadSpawneds
    {
        public int Executions { get; private set; }
        protected override void ExecuteActions(WorldInstance world) => Executions++;
    }
}
