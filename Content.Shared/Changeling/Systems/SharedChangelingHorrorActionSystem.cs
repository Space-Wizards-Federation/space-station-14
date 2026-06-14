using Content.Shared.Actions;
using Content.Shared.Changeling.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Changeling.Systems;

/// <summary>
/// Handles the changeling horror action system.
/// </summary>
public sealed partial class SharedChangelingHorrorActionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangelingHorrorActionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChangelingHorrorActionStorageComponent, AfterChangelingTransformEvent>(AfterTransform);
    }

    private void AfterTransform(Entity<ChangelingHorrorActionStorageComponent> ent, ref AfterChangelingTransformEvent args)
    {
        // do science to toggle things manually
    }

    private void OnMapInit(Entity<ChangelingHorrorActionComponent> ent, ref MapInitEvent args)
    {
        var horrorForm = HasComp<ChangelingHorrorComponent>(ent.Owner);
        var storage = EnsureComp<ChangelingHorrorActionStorageComponent>(ent.Owner);

        // if they are in horror form, they get to immediately receive this action. Otherwise, it goes in the storage

        foreach (var action in ent.Comp.HorrorActions)
        {
            if (horrorForm)
            {
                AddImmediate(ent.Owner, action);
            }
            else
            {
                AddToPool((ent.Owner, storage), action);
            }

        }

        // the reverse

        foreach (var action in ent.Comp.NormalActions)
        {
            if (horrorForm)
            {
                AddToPool((ent.Owner, storage), action);
            }
            else
            {
                AddImmediate(ent.Owner, action);
            }
        }


        RemCompDeferred<ChangelingHorrorComponent>(ent.Owner);
    }

    private void AddToPool(Entity<ChangelingHorrorActionStorageComponent> ent, EntProtoId action)

    {
        var actionEntity = Spawn(action);
        ent.Comp.ActionEntities.Add(actionEntity);
        AddComp<ChangelingHorrorAwareActionComponent>(actionEntity);
    }

    private void AddImmediate(EntityUid ent, EntProtoId action)

    {
        EntityUid? entityUid = null;
        _actions.AddAction(ent, ref entityUid, action);

        if (entityUid.HasValue)
            AddComp<ChangelingHorrorAwareActionComponent>(entityUid.Value);
    }
}
