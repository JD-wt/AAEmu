using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Models.Game.Indun.Events;

internal class IndunEventDoodadSpawneds : IndunEvent
{
    public uint DoodadAlmightyId { get; set; } // templateId
    public uint DoodadFuncGroupId { get; set; }

    public override void Subscribe(WorldInstance worldInstance)
    {
        worldInstance.Events.OnDoodadSpawn += OnDoodadSpawn;
        worldInstance.Events.OnDoodadPhaseChanged += OnDoodadPhaseChanged;
    }

    public override void UnSubscribe(WorldInstance worldInstance)
    {
        // Was "+=", which stacked one more handler on every unsubscribe.
        worldInstance.Events.OnDoodadSpawn -= OnDoodadSpawn;
        worldInstance.Events.OnDoodadPhaseChanged -= OnDoodadPhaseChanged;
    }

    private void OnDoodadSpawn(object sender, OnDoodadSpawnArgs args)
    {
        var doodad = args?.Doodad;
        if (doodad == null || sender is not WorldInstance world) { return; }
        if (doodad.TemplateId != DoodadAlmightyId) { return; }
        if (DoodadFuncGroupId != 0 && doodad.FuncGroupId != DoodadFuncGroupId) { return; }

        Logger.Debug($"IndunEventDoodadSpawneds - {doodad.TemplateId}, {DoodadAlmightyId}");
        ExecuteActions(world);
    }

    private void OnDoodadPhaseChanged(object sender, OnDoodadPhaseChangedArgs args)
    {
        // Level-placed exit portals exist from bootstrap in a hidden phase. Content's spawn
        // condition names the phase in which that portal becomes active, not merely its allocation.
        // Phase-less conditions remain creation events only.
        if (DoodadFuncGroupId == 0 || args?.Doodad == null || sender is not WorldInstance world)
            return;
        if (args.Doodad.TemplateId != DoodadAlmightyId || args.FuncGroupId != DoodadFuncGroupId)
            return;
        ExecuteActions(world);
    }

    protected virtual void ExecuteActions(WorldInstance world)
    {
        // The whole next_action_id chain, not only its first action.
        IndunManager.Instance.DoIndunActions(StartActionId, world);
    }
}
