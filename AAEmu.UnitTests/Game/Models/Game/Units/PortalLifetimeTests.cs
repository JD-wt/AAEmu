using System.Reflection;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Tasks.World;

using ScheduledTask = AAEmu.Game.Models.Tasks.Task;

namespace AAEmu.UnitTests.Game.Models.Game.Units;

public class PortalLifetimeTests
{
    [Test]
    public async Task Timeout_KeepsBothEndsUntilDelayedRemoval_AndRunsOnlyOnce()
    {
        var scheduler = new RecordingScheduler();
        using var world = new WorldInstance(new WorldTemplate(), 0, true, 1);
        var entrance = CreatePortal(world, scheduler, 100);
        var exit = CreatePortal(world, scheduler, 101);
        entrance.LinkedPortal = exit;
        exit.LinkedPortal = entrance;

        new KillPortalTask(entrance).Execute();
        new KillPortalTask(exit).Execute();
        new KillPortalTask(entrance).Execute();

        await Assert.That(entrance.IsClosing).IsTrue();
        await Assert.That(exit.IsClosing).IsTrue();
        await Assert.That(entrance.Hp).IsEqualTo(0);
        await Assert.That(exit.Hp).IsEqualTo(0);
        await Assert.That(world.GetNpc(100)).IsEqualTo(entrance);
        await Assert.That(world.GetNpc(101)).IsEqualTo(exit);
        await Assert.That(scheduler.Tasks.Count).IsEqualTo(2);
        foreach (var (task, delay) in scheduler.Tasks)
        {
            await Assert.That(delay).IsEqualTo(TimeSpan.FromSeconds(2));
            task.Execute();
        }

        await Assert.That(world.GetNpc(100)).IsNull();
        await Assert.That(world.GetNpc(101)).IsNull();
    }

    [Test]
    public async Task ForcedDeletion_BeforeTimeout_DoesNotScheduleClosingOrLeavePartner()
    {
        var scheduler = new RecordingScheduler();
        using var world = new WorldInstance(new WorldTemplate(), 0, true, 1);
        var entrance = CreatePortal(world, scheduler, 100);
        var exit = CreatePortal(world, scheduler, 101);
        entrance.LinkedPortal = exit;
        exit.LinkedPortal = entrance;

        entrance.Delete();
        new KillPortalTask(entrance).Execute();
        new KillPortalTask(exit).Execute();
        entrance.Delete();

        await Assert.That(world.GetNpc(100)).IsNull();
        await Assert.That(world.GetNpc(101)).IsNull();
        await Assert.That(scheduler.Tasks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ClosingPortal_CannotTeleportWhileWaitingForRemoval()
    {
        var scheduler = new RecordingScheduler();
        using var world = new WorldInstance(new WorldTemplate(), 0, true, 1);
        var portal = CreatePortal(world, scheduler, 100);
        var character = new Character(new UnitCustomModelParams());
        SetWorld(character, world);
        character.Transform.Local.SetPosition(10, 20, 30);
        var before = character.Transform.World.Position;
        portal.Close();

        // No destination is assigned: the expired portal must return before using it.
        PortalManager.UsePortal(character, portal.ObjId);

        await Assert.That(character.Transform.World.Position).IsEqualTo(before);
        await Assert.That(world.GetNpc(100)).IsEqualTo(portal);
    }

    private static Portal CreatePortal(WorldInstance world, ITaskManager scheduler, uint id)
    {
        var portal = new Portal(scheduler) { ObjId = id, Hp = 1 };
        SetWorld(portal, world);
        world.AddObject(portal);
        return portal;
    }

    // Avoid the ParentWorld setter's global WorldManager lookup in these isolated tests.
    private static void SetWorld(GameObject obj, WorldInstance world) =>
        typeof(GameObject).GetField("_parentWorld", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(obj, world);

    private sealed class RecordingScheduler : ITaskManager
    {
        public List<(ScheduledTask Task, TimeSpan? Delay)> Tasks { get; } = [];
        public bool Schedule(ScheduledTask task, TimeSpan? startTime = null, TimeSpan? repeatInterval = null, int count = -1)
        {
            Tasks.Add((task, startTime));
            return true;
        }
        public void Initialize() { }
        public void Start() { }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public bool Cancel(ScheduledTask task) => false;
        public bool CronSchedule(ScheduledTask task, string cronExpression, TimeSpan? startDelay = null, int count = -1) => false;
    }
}
