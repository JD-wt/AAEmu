using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using AAEmu.World.Core.Relay;

namespace AAEmu.UnitTests.World.Core.Relay;

[NotInParallel]
public class ZoneNpcSpawnerCatalogTests
{
    private const uint ZoneId = 900398;
    private const uint InstanceId = 900100;
    private string _emptyListOverride;

    [Before(Test)]
    public void Setup()
    {
        _emptyListOverride = Environment.GetEnvironmentVariable("AAEMU_WZ_EMPTY_SPAWNER_LIST");
        Environment.SetEnvironmentVariable("AAEMU_WZ_EMPTY_SPAWNER_LIST", null);
        var cache = (ConcurrentDictionary<uint, IReadOnlyList<ZoneNpcSpawnerCatalog.PersistentSpawnerEntry>>)
            typeof(ZoneNpcSpawnerCatalog).GetField("CatalogCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        cache[ZoneId] = [new(1, 10, 0), new(2, 20, 1)];
    }

    [After(Test)]
    public void Cleanup()
    {
        ZoneNpcSpawnerCatalog.Invalidate(ZoneId);
        Environment.SetEnvironmentVariable("AAEMU_WZ_EMPTY_SPAWNER_LIST", _emptyListOverride);
    }

    [Test]
    public async Task FreshCopy_BootstrapsActiveSpawnersButDoesNotRetireEventWaiters()
    {
        var restored = ZoneNpcSpawnerCatalog.GetPersistentSpawners(ZoneId, InstanceId);
        await Assert.That(restored.Count).IsEqualTo(1);
        await Assert.That(restored[0].Id).IsEqualTo(2u);
        await Assert.That(restored[0].State).IsEqualTo((byte)1);
    }

    [Test]
    public async Task ObservedInactive_IsRestoredAlongsideInitiallyActiveSpawners()
    {
        Save(InstanceId, 1, 10, 0);
        var restored = ZoneNpcSpawnerCatalog.GetPersistentSpawners(ZoneId, InstanceId);
        await Assert.That(restored.Count).IsEqualTo(2);
        await Assert.That(restored[0].Id).IsEqualTo(1u);
        await Assert.That(restored[0].State).IsEqualTo((byte)0);
    }

    [Test]
    public async Task ObservedActive_IsRestoredOnlyToItsOwnCopy()
    {
        Save(InstanceId, 1, 10, 1);
        var restored = ZoneNpcSpawnerCatalog.GetPersistentSpawners(ZoneId, InstanceId);
        await Assert.That(restored.Count).IsEqualTo(2);
        await Assert.That(restored[0].State).IsEqualTo((byte)1);
        var otherCopy = ZoneNpcSpawnerCatalog.GetPersistentSpawners(ZoneId, InstanceId + 1);
        await Assert.That(otherCopy.Count).IsEqualTo(1);
        await Assert.That(otherCopy[0].Id).IsEqualTo(2u);
    }

    [Test]
    public async Task RemovedCopy_DoesNotReuseItsSavedState()
    {
        Save(InstanceId, 1, 10, 0);
        ZoneNpcSpawnerCatalog.RemoveInstance(InstanceId);
        var restored = ZoneNpcSpawnerCatalog.GetPersistentSpawners(ZoneId, InstanceId);
        await Assert.That(restored.Count).IsEqualTo(1);
        await Assert.That(restored[0].Id).IsEqualTo(2u);
    }

    [Test]
    public async Task ChangedTemplate_DoesNotRestoreStaleState()
    {
        Save(InstanceId, 1, 99, 0);
        var restored = ZoneNpcSpawnerCatalog.GetPersistentSpawners(ZoneId, InstanceId);
        await Assert.That(restored.Count).IsEqualTo(1);
        await Assert.That(restored[0].Id).IsEqualTo(2u);
    }

    private static void Save(uint instanceId, uint placementId, uint type, byte state)
    {
        var catalog = typeof(ZoneNpcSpawnerCatalog);
        var keyType = catalog.GetNestedType("PersistentSpawnerKey", BindingFlags.NonPublic)!;
        var stateType = catalog.GetNestedType("PersistentSpawnerState", BindingFlags.NonPublic)!;
        var states = (IDictionary)catalog.GetField("InstanceStates", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        states[Activator.CreateInstance(keyType, instanceId, ZoneId, placementId)!] =
            Activator.CreateInstance(stateType, type, state)!;
    }
}
