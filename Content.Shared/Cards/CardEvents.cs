using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cards;

[Serializable, NetSerializable]
public sealed class CardAnimationEvent : EntityEventArgs
{
    public readonly NetCoordinates MergerCoords;
    public readonly bool MergeeFlipped;
    public readonly NetCoordinates MergeeCoords;
    public readonly Angle MergeeRotation;
    public readonly ProtoId<StackPrototype> StackId;
    public readonly List<CardData> Selected;

    public CardAnimationEvent(
        NetCoordinates mergerCoords,
        bool mergeeFlipped,
        NetCoordinates mergeeCoords,
        Angle mergeeRotation,
        ProtoId<StackPrototype> stackId,
        List<CardData> selected
    )
    {
        MergerCoords = mergerCoords;
        MergeeFlipped = mergeeFlipped;
        MergeeCoords = mergeeCoords;
        MergeeRotation = mergeeRotation;
        StackId = stackId;
        Selected = selected;
    }
}

[Serializable, NetSerializable]
public sealed class CardDropMergeEvent : EntityEventArgs
{
    public readonly NetEntity Mergee;
    public readonly NetEntity Merger;

    public CardDropMergeEvent(NetEntity merger, NetEntity mergee)
    {
        Mergee = mergee;
        Merger = merger;
    }
}

[Serializable, NetSerializable]
public sealed class CycleCardsEvent : EntityEventArgs
{
    public readonly NetEntity Cards;
    public readonly int Amount;

    public CycleCardsEvent(NetEntity cards, int amount)
    {
        Cards = cards;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class TakeCardEvent : EntityEventArgs
{
    public readonly NetEntity Cards;
    public readonly NetEntity User;
    public readonly int CardInx;

    public TakeCardEvent(NetEntity cards, NetEntity user, int cardInx)
    {
        Cards = cards;
        User = user;
        CardInx = cardInx;
    }
}

[Serializable, NetSerializable]
public sealed class FlipCardsEvent : EntityEventArgs
{
    public readonly NetEntity Cards;

    public FlipCardsEvent(NetEntity cards)
    {
        Cards = cards;
    }
}

[Serializable, NetSerializable]
public sealed class FanCardsEvent : EntityEventArgs
{
    public readonly NetEntity Cards;

    public FanCardsEvent(NetEntity cards)
    {
        Cards = cards;
    }
}

[Serializable, NetSerializable]
public sealed class ShuffleCardsEvent : EntityEventArgs
{
    public readonly NetEntity Cards;

    public ShuffleCardsEvent(NetEntity cards)
    {
        Cards = cards;
    }
}
