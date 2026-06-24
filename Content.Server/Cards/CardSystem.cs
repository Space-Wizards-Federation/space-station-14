using Content.Shared.Cards;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Cards;

/// <inheritdoc />
[UsedImplicitly]
public sealed partial class CardSystem : SharedCardSystem
{
    private int _inxCounter = 0;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CardDropMergeEvent>(HandleDropMerge);
    }

    protected override void OnCardsInit(Entity<CardsComponent> ent, ref ComponentInit args)
    {
        base.OnCardsInit(ent, ref args);
        for (var i = 0; i < ent.Comp.Cards.Count; i++)
        {
            var card = ent.Comp.Cards[i];
            card.CardInx = _inxCounter;
            _inxCounter++;
            ent.Comp.Cards[i] = card;
        }
        Dirty(ent.Owner, ent.Comp);
    }

    protected override void OpenInspectUI(EntityUid player, Entity<CardsComponent> cards) { }

    private void HandleDropMerge(CardDropMergeEvent args)
    {
        var mergee = GetEntity(args.Mergee);
        var merger = GetEntity(args.Merger);

        if (
            TryComp<StackComponent>(mergee, out var donorStack)
            && TryComp<StackComponent>(merger, out var recipientStack)
        )
            Stacks.TryMergeStacks((mergee, donorStack), (merger, recipientStack), out _);
    }

    protected override void PlayCardAnimation(
        EntityCoordinates mergerCoords,
        bool mergeeFlipped,
        EntityCoordinates mergeeCoords,
        Angle mergeeRotation,
        ProtoId<StackPrototype> stackId,
        List<CardData> selected,
        bool playOnUser = false
    )
    {
        var ev = new CardAnimationEvent(
            GetNetCoordinates(mergerCoords),
            mergeeFlipped,
            GetNetCoordinates(mergeeCoords),
            mergeeRotation,
            stackId,
            selected
        );
        // Don't for the client which is running it as they can have their own predicted animation
        // Do play it for systems which are not predicted i.e. splitting stacks
        var filter = Filter
            .Pvs(mergerCoords)
            .RemoveWhereAttachedEntity(e => !playOnUser && (e == mergerCoords.EntityId || e == mergeeCoords.EntityId));
        RaiseNetworkEvent(ev, filter);
    }
}
