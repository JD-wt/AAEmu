using AAEmu.Game.Models.Game.Items;

namespace AAEmu.UnitTests.Game.Models.Game.Items;

public class ItemEvolvingRulesTests
{
    [Test]
    public async Task Advance_UsesCurrentGradeCostForReportedDagger()
    {
        ulong experience = 6923 + 18092;
        await Assert.That(ItemEvolvingRules.TryAdvance(5, 6, 12, 11151, ref experience)).IsTrue();
        await Assert.That(experience).IsEqualTo(13864ul);
        await Assert.That(ItemEvolvingRules.TryAdvance(6, 7, 12, 14940, ref experience)).IsFalse();
        await Assert.That(experience).IsEqualTo(13864ul);
    }

    [Test]
    public async Task Advance_CarriesAcrossMultipleGradesAndStopsAtTop()
    {
        ulong experience = 11151ul + 14940 + 20000;
        await Assert.That(ItemEvolvingRules.TryAdvance(5, 6, 7, 11151, ref experience)).IsTrue();
        await Assert.That(ItemEvolvingRules.TryAdvance(6, 7, 7, 14940, ref experience)).IsTrue();
        await Assert.That(experience).IsEqualTo(20000ul);
        await Assert.That(ItemEvolvingRules.TryAdvance(7, 8, 7, 10000, ref experience)).IsFalse();
    }

    [Test]
    public async Task Advance_ExactBarAdvancesWithNoRemainder()
    {
        ulong experience = 11151;
        await Assert.That(ItemEvolvingRules.TryAdvance(5, 6, 12, 11151, ref experience)).IsTrue();
        await Assert.That(experience).IsEqualTo(0ul);
    }

    [Test]
    public async Task Advance_LargeFeedDoesNotWrap()
    {
        ulong experience = (ulong)uint.MaxValue + 100;
        await Assert.That(ItemEvolvingRules.TryAdvance(10, 11, 12, uint.MaxValue, ref experience)).IsTrue();
        await Assert.That(experience).IsEqualTo(100ul);
    }

    [Test]
    public async Task TryPurchase_RejectsWhenTheLadderIsFull()
    {
        await Assert.That(ItemEvolvingRules.TryPurchase(50, 0, out var purchased)).IsFalse();
        await Assert.That(purchased).IsEqualTo(0u);
    }

    [Test]
    public async Task TryPurchase_TakesOnlyTheRemainingRoom()
    {
        await Assert.That(ItemEvolvingRules.TryPurchase(80, 25, out var purchased)).IsTrue();
        await Assert.That(purchased).IsEqualTo(25u);
    }

    [Test]
    public async Task TryPurchase_KeepsAFeedThatFits()
    {
        await Assert.That(ItemEvolvingRules.TryPurchase(40, 100, out var purchased)).IsTrue();
        await Assert.That(purchased).IsEqualTo(40u);
    }

    [Test]
    public async Task TryTakeFeed_StopsBeforeASlotThatWouldBePureOverflow()
    {
        await Assert.That(ItemEvolvingRules.TryTakeFeed([50, 50], 40, out var purchased, out var takeCount))
            .IsTrue();
        await Assert.That(purchased).IsEqualTo(40u);
        await Assert.That(takeCount).IsEqualTo(1);
    }

    [Test]
    public async Task TryTakeFeed_KeepsTheLastSlotThatStillBuysRoom()
    {
        await Assert.That(ItemEvolvingRules.TryTakeFeed([30, 30], 50, out var purchased, out var takeCount))
            .IsTrue();
        await Assert.That(purchased).IsEqualTo(50u);
        await Assert.That(takeCount).IsEqualTo(2);
    }

    [Test]
    public async Task TryTakeFeed_RejectsAFullLadder()
    {
        await Assert.That(ItemEvolvingRules.TryTakeFeed([40, 40], 0, out var purchased, out var takeCount))
            .IsFalse();
        await Assert.That(purchased).IsEqualTo(0u);
        await Assert.That(takeCount).IsEqualTo(0);
    }
}
