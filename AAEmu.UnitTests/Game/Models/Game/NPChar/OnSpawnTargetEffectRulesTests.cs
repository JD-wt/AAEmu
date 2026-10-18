using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.NPChar;

namespace AAEmu.UnitTests.Game.Models.Game.NPChar;

public class OnSpawnTargetEffectRulesTests
{
    /// <summary>An event start script: plotless, plot_only, buff gated on the target's buff tag.</summary>
    private static OnSpawnTargetEffectRules.TargetEffect[] StartScriptEffects() =>
        [new OnSpawnTargetEffectRules.TargetEffect(TargetBuffTagId: 4181, TargetNpcTagId: 0, CheckTargetTagSrc: false)];

    [Test]
    public async Task EventStartScript_TargetBuffTag_Runs()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(
            zoneAuthority: true,
            isZoneMirror: true,
            isPriorityMirror: true,
            hasPlot: false,
            effects: StartScriptEffects())).IsTrue();
    }

    [Test]
    public async Task EventStartScript_WithPlot_IsLeftToThePlotPath()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(
            zoneAuthority: true,
            isZoneMirror: true,
            isPriorityMirror: true,
            hasPlot: true,
            effects: StartScriptEffects())).IsFalse();
    }

    [Test]
    public async Task SelfBuff_NoTargetTag_IsNotRun()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(
            zoneAuthority: true,
            isZoneMirror: true,
            isPriorityMirror: true,
            hasPlot: false,
            effects: [new OnSpawnTargetEffectRules.TargetEffect(0, 0, false)])).IsFalse();
    }

    [Test]
    public async Task ChainSkill_TagReadOffTheCaster_IsNotRun()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(
            zoneAuthority: true,
            isZoneMirror: true,
            isPriorityMirror: true,
            hasPlot: false,
            effects: [new OnSpawnTargetEffectRules.TargetEffect(4181, 0, CheckTargetTagSrc: true)])).IsFalse();
    }

    [Test]
    public async Task NpcTagTarget_Runs()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(
            zoneAuthority: true,
            isZoneMirror: true,
            isPriorityMirror: true,
            hasPlot: false,
            effects: [new OnSpawnTargetEffectRules.TargetEffect(0, 1234, false)])).IsTrue();
    }

    [Test]
    public async Task NoEffects_IsNotRun()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(
            zoneAuthority: true,
            isZoneMirror: true,
            isPriorityMirror: true,
            hasPlot: false,
            effects: [])).IsFalse();
    }

    [Test]
    public async Task NotAPriorityMirror_IsNotRun()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(
            zoneAuthority: true,
            isZoneMirror: true,
            isPriorityMirror: false,
            hasPlot: false,
            effects: StartScriptEffects())).IsFalse();
    }

    [Test]
    public async Task WithoutZoneAuthority_IsNotRun()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(
            zoneAuthority: false,
            isZoneMirror: true,
            isPriorityMirror: true,
            hasPlot: false,
            effects: StartScriptEffects())).IsFalse();
    }
    // 49021: two direct spawner effects and a source despawn, no plot or target tags.
    [Test]
    public async Task InstanceStart_DirectSpawnerWithoutTags_Runs()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(true, true, true, false,
            [new(0, 0, false, true), new(0, 0, false, true), new(0, 0, false)],
            isInstanceStartNpc: true)).IsTrue();
    }

    [Test]
    public async Task OtherEvent_DirectSpawnerWithoutTags_DoesNotRun()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(true, true, true, false,
            [new(0, 0, false, true)], isInstanceStartNpc: false)).IsFalse();
    }

    [Test]
    public async Task InstanceStart_WithPlot_RemainsOwnedByPlotPath()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(true, true, true, true,
            [new(0, 0, false, true)], isInstanceStartNpc: true)).IsFalse();
    }

    [Test]
    public async Task InstanceStart_WithoutSpawnerOrTargetTags_DoesNotRun()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(true, true, true, false,
            [new(0, 0, false)], isInstanceStartNpc: true)).IsFalse();
    }

    [Test]
    public async Task InstanceStart_WithoutZoneAuthority_DoesNotRun()
    {
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(false, true, true, false,
            [new(0, 0, false, true)], isInstanceStartNpc: true)).IsFalse();
    }
    [Test]
    public async Task DirectSpawner_IsRecognizedFromSkillTemplate()
    {
        var skill = new SkillTemplate
        {
            Effects = [new SkillEffect { Template = new NpcSpawnerSpawnEffect { SpawnerId = 236455 } },
                new SkillEffect { Template = new SpecialEffect() }]
        };
        var effects = OnSpawnTargetEffectRules.EffectsOf(skill);
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(true, true, true, false,
            effects, isInstanceStartNpc: true)).IsTrue();
        await Assert.That(OnSpawnTargetEffectRules.ShouldWorldRun(true, true, true, false,
            effects, isInstanceStartNpc: false)).IsFalse();
    }
}
