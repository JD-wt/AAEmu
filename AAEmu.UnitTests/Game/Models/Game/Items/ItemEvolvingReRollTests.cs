using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

namespace AAEmu.UnitTests.Game.Models.Game.Items;

public class ItemEvolvingReRollTests
{
    [Test]
    public async Task SelectableStoneUsesSecondExtraValueFromReportedPacket()
    {
        var extras = new SkillObjectExtraValues { Values = [0, 0x67] };
        var actual = ItemEvolvingReRollBase.ResolveReplacement(true, extras,
            id => id == 103, () => throw new InvalidOperationException("Must not roll a selected effect"));
        await Assert.That(actual).IsEqualTo(103u);
        await Assert.That(ItemEvolvingReRollBase.ResolveSelectedSlot([130, 117], extras)).IsEqualTo(0);
    }

    [Test]
    public async Task SelectableStoneReadsSecondValueInPaddedNetworkObject()
    {
        var extras = new SkillObjectExtraValues();
        extras.Values[1] = 103;
        await Assert.That(ItemEvolvingReRollBase.ResolveReplacement(true, extras, id => id == 103,
            () => throw new InvalidOperationException("Must not roll"))).IsEqualTo(103u);
    }

    [Test]
    public async Task InvalidSelectedGroupNeverFallsBackToRandom()
    {
        var extras = new SkillObjectExtraValues { Values = [0, 103] };
        await Assert.That(ItemEvolvingReRollBase.ResolveReplacement(true, extras, _ => false,
            () => throw new InvalidOperationException("Must not roll"))).IsEqualTo(0u);
    }

    [Test]
    public async Task MissingSelectionNeverFallsBackToRandom()
    {
        foreach (var values in new int[][] { [], [0], [0, 0], [0, -1] })
        {
            var extras = new SkillObjectExtraValues { Values = values };
            await Assert.That(ItemEvolvingReRollBase.ResolveReplacement(true, extras,
                _ => throw new InvalidOperationException("No valid group to validate"),
                () => throw new InvalidOperationException("Must not roll"))).IsEqualTo(0u);
        }
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    [Arguments(5)]
    public async Task InvalidOrEmptySelectedSlotIsRejected(int slot)
    {
        var extras = new SkillObjectExtraValues { Values = [slot, 103] };
        await Assert.That(ItemEvolvingReRollBase.ResolveSelectedSlot([130, 0], extras)).IsEqualTo(-1);
    }

    [Test]
    public async Task OrdinaryStoneStillRollsRegardlessOfSelectionField()
    {
        var extras = new SkillObjectExtraValues { Values = [0, 103] };
        var calls = 0;
        var actual = ItemEvolvingReRollBase.ResolveReplacement(false, extras,
            _ => throw new InvalidOperationException("Ordinary stones do not select"),
            () => { calls++; return 135; });
        await Assert.That(actual).IsEqualTo(135u);
        await Assert.That(calls).IsEqualTo(1);
    }

    private static Item Stone(uint skillId = 32060) => new(1,
        new ItemTemplate { Id = 46682, UseSkillId = skillId, UseSkillAsReagent = false }, 1)
        { SlotType = SlotType.Inventory };

    private static ItemSet Allowed() => new()
    {
        Items = new() { [767] = new ItemSetItem { ItemId = 46682, Count = 1 } }
    };

    [Test]
    [Arguments((ushort)0)]
    [Arguments((ushort)5)]
    public async Task StoneWorksWithoutSpendingEarnedAttempts(ushort chances)
    {
        await Assert.That(ItemEvolvingReRollBase.TryResolvePayment(true, chances, 32060,
            Stone(), Allowed(), out var remaining)).IsTrue();
        await Assert.That(remaining).IsEqualTo(chances);
    }

    [Test]
    public async Task EarnedAttemptConsumesOneChance()
    {
        await Assert.That(ItemEvolvingReRollBase.TryResolvePayment(false, 5, 39836,
            null, null, out var remaining)).IsTrue();
        await Assert.That(remaining).IsEqualTo((ushort)4);
    }

    [Test]
    public async Task NoEarnedAttemptsRejectsWithoutStone()
    {
        await Assert.That(ItemEvolvingReRollBase.TryResolvePayment(false, 0, 39836,
            null, null, out var remaining)).IsFalse();
        await Assert.That(remaining).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task MissingStoneDoesNotFallBackToEarnedAttempts()
    {
        await Assert.That(ItemEvolvingReRollBase.TryResolvePayment(true, 5, 32060,
            null, Allowed(), out var remaining)).IsFalse();
        await Assert.That(remaining).IsEqualTo((ushort)5);
    }

    [Test]
    public async Task StoneMustBelongToTargetsAllowedSet()
    {
        await Assert.That(ItemEvolvingReRollBase.TryResolvePayment(true, 5, 32060,
            Stone(), new ItemSet(), out _)).IsFalse();
    }

    [Test]
    public async Task SourceItemMustProvideTheCastSkill()
    {
        await Assert.That(ItemEvolvingReRollBase.TryResolvePayment(true, 5, 32060,
            Stone(39836), Allowed(), out _)).IsFalse();
    }

    [Test]
    public async Task EmptyStoneStackIsRejected()
    {
        var stone = Stone();
        stone.Count = 0;
        await Assert.That(ItemEvolvingReRollBase.TryResolvePayment(true, 5, 32060,
            stone, Allowed(), out _)).IsFalse();
    }
}
