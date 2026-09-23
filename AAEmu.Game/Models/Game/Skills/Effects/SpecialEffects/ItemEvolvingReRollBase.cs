using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

/// <summary>
/// Shared body of the two "swap synthesis effect" actions. Both replace one of an item's rolled
/// attributes with a fresh roll from the same pool; the only difference is whether the player got to
/// say which one.
/// </summary>
public abstract class ItemEvolvingReRollBase : SpecialEffectAction
{
    /// <summary>
    /// Whether this variant also lets the player name the effect to swap <em>to</em>, rather than
    /// only which of the item's lines is replaced.
    /// </summary>
    /// <remarks>
    /// Both variants let the player choose the line: the replace window puts a radio button on every
    /// effect and the cast carries that index either way. What the selectable variant adds is the
    /// list of possible results to pick from, which rides one field further along.
    /// </remarks>
    protected abstract bool PlayerSelects { get; }

    /// <summary>Position of the chosen line inside the cast's extra values.</summary>
    private const int SlotIndexValue = 0;

    /// <summary>Position of the chosen replacement, sent only by the selectable variant.</summary>
    private const int ChosenGroupValue = 1;

    public override void Execute(BaseUnit caster,
        SkillCaster casterObj,
        BaseUnit target,
        SkillCastTarget targetObj,
        CastAction castObj,
        Skill skill,
        SkillObject skillObject,
        DateTime time,
        int value1,
        int value2,
        int value3,
        int value4)
    {
        var effectName = GetType().Name;

        if (caster is not Character owner)
        {
            Logger.Error("{0}: caster {1} is not a character", effectName, caster?.Id);
            return;
        }

        if (targetObj is not SkillCastItemTarget itemTarget)
        {
            Logger.Warn("{0}: target {1} is not an item", effectName, targetObj);
            skill.Cancelled = true;
            return;
        }

        if (owner.Inventory.GetItemById(itemTarget.Id) is not EquipItem equipItem)
        {
            Logger.Warn("{0}: item {1} not found or not equipment", effectName, itemTarget.Id);
            skill.Cancelled = true;
            return;
        }

        if (equipItem.EnchantDisabled || !equipItem.UsedRndAttrGroupIds.Any())
        {
            // Nothing to swap - don't spend the material over it.
            owner.SendErrorMessage(ErrorMessageType.ItemCannotUse);
            skill.Cancelled = true;
            return;
        }

        var categoryId = (equipItem.Template as EquipItemTemplate)?.RndAttrCategoryId ?? 0;
        if (categoryId == 0)
        {
            owner.SendErrorMessage(ErrorMessageType.ItemCannotUse);
            skill.Cancelled = true;
            return;
        }

        var usesStone = casterObj is SkillItem;
        var stone = casterObj is SkillItem source
            ? owner.Inventory.Bag.GetItemByItemId(source.ItemId)
            : null;
        var category = ItemEnchantGameData.Instance.GetRndAttrCategory(categoryId);
        var allowedStones = category == null ? null : ItemManager.Instance.GetItemSet(category.ReRollItemSetId);
        if (stone == equipItem || !TryResolvePayment(usesStone, equipItem.EvolveChance,
                skill.Template.Id, stone, allowedStones, out var remainingChances))
        {
            owner.SendErrorMessage(ErrorMessageType.ItemCannotUse);
            skill.Cancelled = true;
            return;
        }

        var index = PlayerSelects
            ? ResolveSelectedSlot(equipItem.RndAttrGroupIds, skillObject)
            : ResolveIndex(equipItem, skillObject);
        if (index < 0)
        {
            owner.SendErrorMessage(ErrorMessageType.ItemCannotUse);
            skill.Cancelled = true;
            return;
        }
        var beforeGroupId = equipItem.RndAttrGroupIds[index];

        // Everything the piece wears, the line being replaced included. A swap is meant to trade the
        // effect away, so handing the same one back is not an outcome it can have - and excluding
        // only the other lines let exactly that happen.
        var held = ItemEnchantGameData.Instance.GetRndAttrAttributes(equipItem.UsedRndAttrGroupIds);

        // The replacement comes out of the same bundle as the line being replaced - that bundle owns
        // this slot, and its choices are the only ones the window offers for it.
        var afterGroupId = ResolveReplacement(PlayerSelects, skillObject,
            groupId => IsAllowedReplacement(categoryId, groupId, held, beforeGroupId),
            () => ItemEnchantGameData.Instance.RollRndAttrGroup(categoryId, equipItem.Grade, held, beforeGroupId));
        if (afterGroupId == 0)
        {
            Logger.Warn("{0}: pool {1} has no valid replacement at grade {2}", effectName, categoryId, equipItem.Grade);
            owner.SendErrorMessage(ErrorMessageType.ItemCannotUse);
            skill.Cancelled = true;
            return;
        }

        equipItem.RndAttrGroupIds[index] = afterGroupId;
        equipItem.EvolveChance = remainingChances;
        equipItem.IsDirty = true;

        // The dialog spells both lines out, so the magnitudes are looked up here the same way the
        // client does it. A swap that lands on the same group is a re-roll of the value, not a change.
        // Both magnitudes are read at the piece's own progress. Asking without it returns the floor
        // of the range, which is where a line starts out and is zero for the percentage effects -
        // the dialog then offered to trade "0.0%" for "0.0%".
        var before = ItemEnchantGameData.Instance.GetRndAttrModifier(beforeGroupId, equipItem.Grade,
            equipItem.EvolvingExp);
        var after = ItemEnchantGameData.Instance.GetRndAttrModifier(afterGroupId, equipItem.Grade,
            equipItem.EvolvingExp);
        var changedAttribute = beforeGroupId != afterGroupId;

        owner.SendPacket(new SCItemDetailUpdatedPacket(equipItem));

        // Gear bonuses are totalled when a piece is equipped and not again, so a change made to a
        // piece already being worn never reached the character sheet. Re-total here; the piece is
        // only in the sum at all while it sits in the equipment container.
        if (equipItem.SlotType == SlotType.Equipment)
            owner.UpdateGearBonuses(null, null);

        owner.SendPacket(new SCItemReRollEvolvingResultPacket(equipItem.Id,
            (byte)ItemGradeEnchantResult.Success, changedAttribute, before, after));

        Logger.Debug("{0}: {1} re-rolled slot {2} of item {3}: group {4} -> {5}",
            effectName, owner.Name, index, equipItem.Id, beforeGroupId, afterGroupId);
    }

    // Item casts always pay with their selected stone, even when earned attempts remain.
    // The skill engine consumes that source only after this effect succeeds.
    internal static bool TryResolvePayment(bool usesStone, ushort chances, uint skillId,
        Item stone, ItemSet allowedStones, out ushort remainingChances)
    {
        remainingChances = chances;
        if (usesStone)
            return stone is { Count: > 0, SlotType: SlotType.Inventory } &&
                   stone.Template?.UseSkillId == skillId &&
                   allowedStones?.Items.Values.Any(entry => entry.ItemId == stone.TemplateId) == true;

        if (chances == 0)
            return false;
        remainingChances--;
        return true;
    }

    /// <summary>
    /// Which of the item's lines the swap replaces.
    /// </summary>
    /// <remarks>
    /// The replace window puts a radio button on every effect and sends the chosen position as the
    /// first of the cast's extra values - zero-based, in the order the lines are stored. Both
    /// variants send it; reading it only for the selectable one, or looking for it on a skill object
    /// the cast does not carry, replaces whichever line comes first instead of the one the player
    /// pointed at. Out of range falls back to a random line so a stale window cannot wedge the cast.
    /// </remarks>
    private static int ResolveIndex(EquipItem equipItem, SkillObject skillObject)
    {
        var used = equipItem.UsedRndAttrGroupIds.Count();

        if (skillObject is SkillObjectExtraValues extras && extras.Values.Length > SlotIndexValue)
        {
            var chosen = extras.Values[SlotIndexValue];
            if (chosen >= 0 && chosen < used)
                return chosen;
        }

        return Random.Shared.Next(used);
    }

    internal static int ResolveSelectedSlot(uint[] groups, SkillObject skillObject)
    {
        if (groups == null || skillObject is not SkillObjectExtraValues extras ||
            extras.Values.Length <= SlotIndexValue)
            return -1;

        var slot = extras.Values[SlotIndexValue];
        return slot >= 0 && slot < groups.Length && groups[slot] != 0 ? slot : -1;
    }

    /// <summary>
    /// Selectable casts carry [slot index, replacement group id]. Invalid selections never roll
    /// a substitute: returning zero cancels the cast before the stone or earned chance is spent.
    /// </summary>
    internal static uint ResolveReplacement(bool playerSelects, SkillObject skillObject,
        Func<uint, bool> isAllowed, Func<uint> roll)
    {
        if (!playerSelects)
            return roll();

        if (skillObject is not SkillObjectExtraValues extras ||
            extras.Values.Length <= ChosenGroupValue || extras.Values[ChosenGroupValue] <= 0)
            return 0;

        var groupId = (uint)extras.Values[ChosenGroupValue];
        return isAllowed(groupId) ? groupId : 0;
    }

    private static bool IsAllowedReplacement(uint categoryId, uint groupId, List<short> held,
        uint bundleAnchor)
    {
        // A selection must come from this slot's bundle and must not duplicate a held attribute.
        if (!ItemEnchantGameData.Instance.IsGroupInSameBundle(categoryId, bundleAnchor, groupId) ||
            !ItemEnchantGameData.Instance.IsGroupInCategory(groupId, categoryId))
            return false;

        var attribute = ItemEnchantGameData.Instance.GetRndAttrAttributes([groupId]);
        return attribute.Count > 0 && !held.Contains(attribute[0]);
    }
}
