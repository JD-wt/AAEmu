using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Tasks.World;

/// <summary>Remove a portal after the client has had time to play its closing effect.</summary>
public class DeletePortalTask(Portal portal) : Task
{
    public override void Execute() => portal.Delete();
}
