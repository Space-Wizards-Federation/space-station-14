using Content.Shared.Actions;
using Content.Shared.Changeling.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Changeling.Systems;

/// <summary>
/// Handles the changeling horror action system.
/// </summary>
public abstract partial class SharedChangelingHorrorActionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangelingHorrorActionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChangelingHorrorActionStorageComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ChangelingHorrorActionStorageComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ChangelingHorrorActionStorageComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.StoredIsHorror = !HasComp<ChangelingHorrorComponent>(ent.Owner);
    }

    private void OnShutdown(Entity<ChangelingHorrorActionStorageComponent> ent, ref ComponentShutdown args)
    {
        foreach (var action in ent.Comp.ActionEntities)
        {
            Del(action);
        }

        ent.Comp.ActionEntities.Clear();
    }

    /// <summary>
    /// Retrieves horror actions & stores non horror actions
    /// </summary>
    /// <param name="ent">The changeling</param>
    public void TransformToHorror(EntityUid ent)
    {
        if (TryComp<ChangelingHorrorActionStorageComponent>(ent, out var storage))
        {
            if (!storage.StoredIsHorror)
            {
                Log.Error("Changeling tried to morph into horror form but its actions are already the horror's");
                return;
            }
            Toggle(ent);
        }
    }

    /// <summary>
    /// Retrieves non horror actions & stores horror actions
    /// </summary>
    /// <param name="ent">The changeling</param>
    public void TransformFromHorror(EntityUid ent)
    {
        if (TryComp<ChangelingHorrorActionStorageComponent>(ent, out var storage))
        {
            if (storage.StoredIsHorror)
            {
                Log.Error("Changeling tried to morph from horror form but its actions are already normal");
                return;
            }
            Toggle(ent);
        }
    }

    private void Toggle(EntityUid ent)
    {
        var storage = EnsureComp<ChangelingHorrorActionStorageComponent>(ent);
        var actionEnts = new List<EntityUid>();
        foreach (var action in _actions.GetActions(ent))
        {
            if (HasComp<ChangelingHorrorAwareActionComponent>(action.Owner))
            {
                actionEnts.Add(action.Owner);
            }
        }

        foreach (var action in storage.ActionEntities)
        {
            _actions.AddActionDirect(ent, action);
        }

        storage.ActionEntities.Clear();

        foreach (var action in actionEnts)
        {
            _actions.SetTemporary(action, false);
            _actions.RemoveAction(action);
            storage.ActionEntities.Add(action);
        }

        // Toggle the flag
        storage.StoredIsHorror = !storage.StoredIsHorror;
        // networking
        Dirty<ChangelingHorrorActionStorageComponent>((ent, storage));
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


        RemCompDeferred<ChangelingHorrorActionComponent>(ent.Owner);
        // networking
        Dirty<ChangelingHorrorActionStorageComponent>((ent, storage));
    }

    private void AddToPool(Entity<ChangelingHorrorActionStorageComponent> ent, EntProtoId action)

    {
        // we add the action then removing, keeping it on the storage component
        var id = _actions.AddAction(ent.Owner, action);
        if (!id.HasValue)
            return;

        _actions.SetTemporary(id.Value, false);
        _actions.RemoveAction(id);
        AddComp<ChangelingHorrorAwareActionComponent>(id.Value);

        // finaly sotre the action
        var storage = EnsureComp<ChangelingHorrorActionStorageComponent>(ent.Owner);
        storage.ActionEntities.Add(id.Value);
    }

    private void AddImmediate(EntityUid ent, EntProtoId action)

    {
        EntityUid? entityUid = null;
        _actions.AddAction(ent, ref entityUid, action);

        if (entityUid.HasValue)
            AddComp<ChangelingHorrorAwareActionComponent>(entityUid.Value);
    }
}
