using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Skills.Effects.Enums;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Core.Packets;

namespace AAEmu.UnitTests.Game.Models.Game.NPChar;

public class NpcDeathDoodadSignalRulesTests
{
    [Test]
    public async Task MixedDeathSignal_DoesNotReplayRewardOrOtherEffects()
    {
        var reward = new RecordingEffect();
        var template = new SkillTemplate
        {
            Id = 48817, TargetAreaRadius = 200, TargetAreaCount = 100,
            Effects = [new SkillEffect
            {
                Template = reward, ApplicationMethod = SkillEffectApplicationMethod.SourceOnce,
                StartLevel = 1, EndLevel = 99, Chance = 100, Friendly = true, NonFriendly = true
            }]
        };
        var caster = new Unit { ObjId = 1 };
        var skill = new Skill(template);

        skill.SignalAreaDoodads(caster);

        await Assert.That(reward.Applications).IsEqualTo(0);
        await Assert.That(skill.HitTypes.Count).IsEqualTo(0);
    }

    private sealed class RecordingEffect : EffectTemplate
    {
        public int Applications { get; private set; }
        public override bool OnActionTime => false;
        public override void Apply(BaseUnit caster, SkillCaster casterObj, BaseUnit target,
            SkillCastTarget targetObj, CastAction castObj, EffectSource source,
            SkillObject skillObject, DateTime time, CompressedGamePackets packetBuilder = null) => Applications++;
    }

    [Test]
    public async Task GuardianDeath_EffectlessAreaSignal_IsHandled()
    {
        var template = new SkillTemplate { Id = 48892, TargetAreaRadius = 100, TargetAreaCount = 100 };
        await Assert.That(NpcDeathDoodadSignalRules.ShouldSignal(true, true, true, template)).IsTrue();
    }

    [Test]
    public async Task DeathSkillWithSpawnEffect_StillDeliversItsIndependentDoodadSignal()
    {
        var template = new SkillTemplate
        {
            TargetAreaRadius = 100,
            Effects = [new SkillEffect { Template = new NpcSpawnerSpawnEffect() }]
        };
        await Assert.That(NpcDeathDoodadSignalRules.ShouldSignal(true, true, true, template)).IsTrue();
    }

    [Test]
    public async Task OtherOwnershipOrWorld_DoesNotReplayDeath()
    {
        var template = new SkillTemplate { TargetAreaRadius = 100 };
        await Assert.That(NpcDeathDoodadSignalRules.ShouldSignal(false, true, true, template)).IsFalse();
        await Assert.That(NpcDeathDoodadSignalRules.ShouldSignal(true, false, true, template)).IsFalse();
        await Assert.That(NpcDeathDoodadSignalRules.ShouldSignal(true, true, false, template)).IsFalse();
    }

    [Test]
    public async Task PlotOnlyOrNonAreaSkill_IsNotReplayed()
    {
        await Assert.That(NpcDeathDoodadSignalRules.ShouldSignal(true, true, true,
            new SkillTemplate { TargetAreaRadius = 100, PlotOnly = true })).IsFalse();
        await Assert.That(NpcDeathDoodadSignalRules.ShouldSignal(true, true, true,
            new SkillTemplate())).IsFalse();
    }
}
