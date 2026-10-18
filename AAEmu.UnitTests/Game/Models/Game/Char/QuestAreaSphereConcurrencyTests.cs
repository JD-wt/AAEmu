using System.Reflection;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.UnitTests.Game.Models.Game.Char;

public class QuestAreaSphereConcurrencyTests
{
    [Test]
    public async Task ReconcileAndMountSync_UseTheSameCharacterLock()
    {
        var quests = new CharacterQuests(new Character(null));
        var sync = typeof(CharacterQuests).GetField("_questAreaSphereLock",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(quests)!;
        using var reconcileStarted = new ManualResetEventSlim();
        using var mountsStarted = new ManualResetEventSlim();
        Task reconcile;
        Task mounts;
        Monitor.Enter(sync);
        try
        {
            reconcile = Task.Run(() => { reconcileStarted.Set(); quests.ReconcileQuestAreaSpheres(); });
            mounts = Task.Run(() => { mountsStarted.Set(); quests.SyncSphereBuffsToOwnedMounts(); });
            if (!reconcileStarted.Wait(TimeSpan.FromSeconds(5)) || !mountsStarted.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Workers did not start");
            // Both calls must wait until the character's area-state transaction has finished.
            Thread.Sleep(50);
            if (reconcile.IsCompleted || mounts.IsCompleted)
                throw new InvalidOperationException("Area-state operation bypassed its character lock");
        }
        finally
        {
            Monitor.Exit(sync);
        }
        await Task.WhenAll(reconcile, mounts).WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(reconcile.IsCompletedSuccessfully && mounts.IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task Failure_AlwaysReleasesTheReentrantReconcileGuard()
    {
        var quests = new CharacterQuests(null);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var failed = false;
            try { quests.ReconcileQuestAreaSpheres(); }
            catch (NullReferenceException) { failed = true; }
            // A stale guard would silently skip the second call rather than enter the core again.
            await Assert.That(failed).IsTrue();
        }
    }
}
