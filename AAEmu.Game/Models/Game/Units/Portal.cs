using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Models.Tasks.World;

namespace AAEmu.Game.Models.Game.Units;

public sealed class Portal(ITaskManager taskManager = null) : Npc
{
    // Portal prefab models have dying_time = 0 in content. Keep them visible for a
    // short grace period so SCUnitsRemoved does not cut off the client's close effect.
    private static readonly TimeSpan CloseEffectDelay = TimeSpan.FromSeconds(2);
    private int _closing;
    private int _deleted;

    public bool IsClosing => Volatile.Read(ref _closing) != 0;
    /// <summary>Bit 1 of the SCUnitState NPC flag: walking into this unit sends CSUsePortal.</summary>
    private const byte EntranceFlag = 0x02;
    /// <summary>Bit 2: the client shows the destination name but does not auto-use the portal.</summary>
    private const byte ExitFlag = 0x04;

    public Transform TeleportPosition { get; set; }
    public Npc LinkedPortal { get; set; }

    /// <summary>The yellow portal that appears at the destination; it is not walked through.</summary>
    public bool IsExit { get; init; }

    public override byte UnitStateFlag => IsExit ? ExitFlag : EntranceFlag;

    public void Close(KillReason killReason = KillReason.PortalTimeout)
    {
        if (Interlocked.Exchange(ref _closing, 1) != 0)
            return;

        Hp = 0;
        BroadcastPacket(new SCUnitDeathPacket(ObjId, killReason), false);
        (taskManager ?? TaskManager.Instance).Schedule(new DeletePortalTask(this), CloseEffectDelay);

        // Each end has its own lifetime task; closing either end must be idempotent.
        if (LinkedPortal is Portal linked)
            linked.Close(killReason);
    }

    public override void DoDie(BaseUnit killer, KillReason killReason)
    {
        Close(killReason);
    }

    public override void Delete()
    {
        if (Interlocked.Exchange(ref _deleted, 1) != 0)
            return;

        Interlocked.Exchange(ref _closing, 1);
        Hp = 0;
        // Final removal (or forced cleanup) must not send another death animation.
        base.Delete();
        // A closing partner owns its own animation deadline.
        if (LinkedPortal is Portal { IsClosing: false } linked)
            linked.Delete();
    }
}
