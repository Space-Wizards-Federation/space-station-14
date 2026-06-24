using System.Linq;
using System.Numerics;
using Content.Client.Examine;
using Content.Client.Gameplay;
using Content.Client.Stack;
using Content.Client.Storage.Systems;
using Content.Shared.Cards;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Cards;

/// <inheritdoc />
[UsedImplicitly]
public sealed partial class CardSystem : SharedCardSystem
{
    [Dependency]
    private SharedStorageSystem _storage = default!;

    [Dependency]
    private SpriteSystem _sprite = default!;

    [Dependency]
    private IStateManager _stateManager = default!;

    [Dependency]
    private ExamineSystem _examineSystem = default!;

    [Dependency]
    private ISharedPlayerManager _playerManager = default!;

    [Dependency]
    private StackSystem _stacks = default!;

    [Dependency]
    private ItemCounterSystem _counterSystem = default!;

    private const string RsiPath = "/Textures/Objects/Fun/PlayingCards/nanotrasenbasiccards.rsi";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardsComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeNetworkEvent<CardAnimationEvent>(HandleCardAnimation);
        OnCardButtonClicked += args =>
        {
            RaisePredictiveEvent(
                new TakeCardEvent(GetNetEntity(args.cards.Owner), GetNetEntity(args.user), args.cardInx)
            );
        };
        OnCycleClick += args =>
        {
            RaisePredictiveEvent(new CycleCardsEvent(GetNetEntity(args.cards.Owner), args.amount));
        };
        OnFlipButtonClicked += args =>
        {
            RaisePredictiveEvent(new FlipCardsEvent(GetNetEntity(args.Owner)));
        };
        OnFanButtonClicked += args =>
        {
            RaisePredictiveEvent(new FanCardsEvent(GetNetEntity(args.Owner)));
        };
    }

    protected override void OnCardsDropped(Entity<CardsComponent> ent, ref DroppedEvent args)
    {
        base.OnCardsDropped(ent, ref args);

        if (_stateManager.CurrentState is not GameplayStateBase screen)
            return;

        // Find stack to merge with
        var uid = screen
            .GetClickableEntities(Transform(ent).Coordinates)
            .FirstOrDefault(e => e != ent.Owner && TryComp<CardsComponent>(e, out _));

        if (
            !TryComp<CardsComponent>(uid, out var recipientCardsComp)
            || ent.Comp.Flipped != recipientCardsComp.Flipped
            || !TryComp<StackComponent>(ent.Owner, out var donorStack)
            || !TryComp<StackComponent>(uid, out var recipientStack)
            || !Stacks.TryMergeStacks((ent.Owner, donorStack), (uid, recipientStack), out _)
        )
            return;

        if (Timing.IsFirstTimePredicted)
            RaisePredictiveEvent(new CardDropMergeEvent(GetNetEntity(uid), GetNetEntity(ent.Owner)));
    }

    private void HandleCardAnimation(CardAnimationEvent args)
    {
        PlayCardAnimation(
            GetCoordinates(args.MergerCoords),
            args.MergeeFlipped,
            GetCoordinates(args.MergeeCoords),
            args.MergeeRotation,
            args.StackId,
            args.Selected
        );
    }

    protected override void PlayCardAnimation(
        EntityCoordinates mergerCoords,
        bool mergeeFlipped,
        EntityCoordinates mergeeCoords,
        Angle mergeeRotation,
        ProtoId<StackPrototype> newStackId,
        List<CardData> selected,
        bool playOnUser = false
    )
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        // Spawns an invisible client side clone to apply the sprite to for the cards which are being transferred
        var ent = SpawnTempClone(mergeeFlipped, mergeeCoords, newStackId, selected);
        if (ent == EntityUid.Invalid)
            return;

        _storage.PlayPickupAnimation(ent, mergeeCoords, mergerCoords, mergeeRotation);
        QueueDel(ent);
    }

    private EntityUid SpawnTempClone(
        bool mergeeFlipped,
        EntityCoordinates mergeeCoords,
        ProtoId<StackPrototype> newStackId,
        List<CardData> selected
    )
    {
        if (!PrototypeManager.TryIndex(newStackId, out var newStack))
            return EntityUid.Invalid;

        var ent = Spawn(newStack.Spawn, TransformSystem.ToMapCoordinates(mergeeCoords));

        if (
            !TryComp<CardsComponent>(ent, out var cardsComp)
            || !TryComp<StackComponent>(ent, out var stackComp)
            || !TryComp(ent, out SpriteComponent? spriteComp)
        )
        {
            QueueDel(ent);
            return EntityUid.Invalid;
        }

        cardsComp.Cards = selected;
        cardsComp.Flipped = mergeeFlipped;
        Stacks.SetCount((ent, stackComp), cardsComp.Cards.Count);
        _sprite.SetDrawDepth((ent, spriteComp), (int)DrawDepth.BelowMobs);

        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            Appearance.SetData(ent, CardVisuals.CardList, GetCardListVisualState(cardsComp), appearance);
            Appearance.SetData(ent, CardVisuals.IsFlipped, cardsComp.Flipped, appearance);

            // Appearance only changes on Update
            // As this clone is deleted shortly after need to update the sprite now
            var ev = new AppearanceChangeEvent
            {
                Component = appearance,
                AppearanceData = new Dictionary<Enum, object>(),
                Sprite = spriteComp,
            };
            RaiseLocalEvent(ent, ref ev);
        }

        _sprite.SetVisible((ent, spriteComp), false);
        return ent;
    }

    /// <summary>
    /// Calculates the local position of a card on a fanned arc, given its angle from center.
    /// </summary>
    /// <param name="angle">The angle of the card along the fan, in radians, where 0 is centered.</param>
    /// <param name="radius">The radius of the fan's arc.</param>
    /// <returns>The local position offset for the card.</returns>
    public static Vector2 FanPosition(double angle, float radius) =>
        new((float)Math.Sin(angle) * radius, (float)Math.Cos(angle) * radius - radius * (3f / 4f));

    /// <summary>
    /// Calculates the radius of the fan arc based on the number of cards.
    /// </summary>
    /// <param name="count">The total number of cards in the fan.</param>
    /// <returns>The fan radius, or 0 if there is only one card, since a single card cannot be fanned.</returns>
    public static float FanRadius(int count) => count <= 1 ? 0f : (float)Math.Sqrt(count / 20f);

    /// <summary>
    /// Calculates the position and rotation of a card at a given index within a fanned hand,
    /// arranging cards in a semi-circle from left to right.
    /// </summary>
    /// <param name="inx">The zero-based index of the card within the hand.</param>
    /// <param name="count">The total number of cards in the hand.</param>
    /// <returns>A tuple containing the card's local position and rotation.</returns>
    public static (Vector2, Angle) GetCardPosRot(int inx, int count)
    {
        var radius = FanRadius(count);
        return GetCardPosRot(inx, count, radius);
    }

    /// <summary>
    /// Calculates the position and rotation of a card at a given index within a fanned hand,
    /// arranging cards in a semi-circle from left to right.
    /// </summary>
    /// <param name="inx">The zero-based index of the card within the hand.</param>
    /// <param name="count">The total number of cards in the hand.</param>
    /// <param name="radius">The radius of the fan's arc.</param>
    /// <returns>A tuple containing the card's local position and rotation.</returns>
    public static (Vector2, Angle) GetCardPosRot(int inx, int count, float radius)
    {
        // Semi-circle from left to right
        float angle = (inx - count / 2.0f + 0.5f) / count * (float)Math.PI;
        var position = FanPosition(angle, radius);
        var rotation = new Angle(-angle);
        return (position, rotation);
    }

    // Layer names for each card
    private static (string Base, string LayerOne, string LayerTwo) CardLayers(int i) =>
        ($"card_{i}_base", $"card_{i}_layerOne", $"card_{i}_layerTwo");

    private void OnAppearanceChanged(EntityUid uid, CardsComponent component, ref AppearanceChangeEvent args)
    {
        if (uid == _spriteView?._cards?.Owner)
            _spriteView?.UpdateCards((uid, component));
        Appearance.TryGetData<bool>(uid, CardVisuals.IsFlipped, out var flipped, args.Component);

        // Card visuals state will only have one card in it if not fanned
        // It will have a max of MaxFanned when fanned
        if (!Appearance.TryGetData<CardListVisualState>(uid, CardVisuals.CardList, out var visualState, args.Component))
            visualState = new CardListVisualState(new List<CardData>(), 0, 0);
        if (
            !TryComp<SpriteComponent>(uid, out var sprite)
            || !TryComp<CardsComponent>(uid, out var cards)
            || !TryComp<StackComponent>(uid, out var stack)
            || !TryComp<TransformComponent>(uid, out var xform)
        )
            return;

        if (
            HasComp<MobStateComponent>(xform.ParentUid)
            && xform.ParentUid != _playerManager.LocalSession?.AttachedEntity
        )
        {
            if (flipped)
                visualState.Start = 0;
            flipped = false;
        }

        var count = visualState.Count;
        var radius = FanRadius(count);
        _sprite.LayerMapReserve((uid, sprite), "base_2");
        _sprite.LayerSetVisible((uid, sprite), "base_2", false);
        // amount of cards in the right stack when fanned
        var hiddenCount = flipped ? component.Cards.Count - (visualState.Start + visualState.Count) : visualState.Start;

        if (hiddenCount > 0)
        {
            var maxCount = _stacks.GetMaxCount(stack);
            _stacks.ApplyLayerFunction((uid, stack), ref hiddenCount, ref maxCount);
            _sprite.LayerSetVisible((uid, sprite), "base_2", true);
            _counterSystem.ProcessOpaqueSprite(
                uid,
                "base_2",
                hiddenCount,
                maxCount,
                stack.LayerStates,
                false,
                sprite: args.Sprite
            );
        }
        // Delete all layers which are not used here
        // Assumes that all layers will have the card before it have a layer
        // If it runs into a layer which doesn't exists it assumes no more later layers will exists
        for (var i = count; i < cards.MaxFanned; i++)
        {
            var (baseLayer, layerOne, layerTwo) = CardLayers(i);
            if (!_sprite.LayerExists((uid, sprite), baseLayer))
                break;
            _sprite.RemoveLayer((uid, sprite), baseLayer, false);
            _sprite.RemoveLayer((uid, sprite), layerOne, false);
            _sprite.RemoveLayer((uid, sprite), layerTwo, false);
        }

        for (var i = 0; i < count; i++)
        {
            var card = visualState.CardList[visualState.Start + i];
            var (baseLayer, layerOne, layerTwo) = CardLayers(i);

            if (card.CardId.Id == null || !PrototypeManager.TryIndex<CardPrototype>(card.CardId, out var prototype))
                continue;

            var (position, rotation) = GetCardPosRot(i, count, radius);

            // Creates layers
            _sprite.LayerMapReserve((uid, sprite), baseLayer);
            _sprite.LayerMapReserve((uid, sprite), layerOne);
            _sprite.LayerMapReserve((uid, sprite), layerTwo);

            if (flipped)
            {
                // Creates card and moves
                BuildCard(prototype, baseLayer, card.BaseState, layerOne, layerTwo, (uid, sprite));
                TransformLayer(layerOne, position, rotation, (uid, sprite));
                TransformLayer(layerTwo, position, rotation, (uid, sprite));
            }
            else
            {
                // Uses the base layer for the back side
                BuildLayer(baseLayer, card.CardBack, null, (uid, sprite));
                _sprite.LayerSetVisible((uid, sprite), layerOne, false);
                _sprite.LayerSetVisible((uid, sprite), layerTwo, false);
            }
            // Moves the shared layer
            TransformLayer(baseLayer, position, rotation, (uid, sprite));

            // Moves the stack texture below the left most card
            if (i == 0)
                TransformLayer("base", position, rotation, (uid, sprite));

            if (i == count - 1)
                TransformLayer("base_2", position, rotation, (uid, sprite));
        }
    }

    private void BuildCard(
        CardPrototype prototype,
        string baseLayer,
        string baseSprite,
        string layerOne,
        string layerTwo,
        Entity<SpriteComponent?> sprite
    )
    {
        BuildLayer(baseLayer, baseSprite, null, sprite);
        BuildLayer(layerOne, prototype.LayerOneState, prototype.LayerOneColor, sprite);
        BuildLayer(layerTwo, prototype.LayerTwoState, prototype.LayerTwoColor, sprite);
    }

    private void BuildLayer(string layer, string? layerState, Color? layerColor, Entity<SpriteComponent?> sprite)
    {
        _sprite.LayerSetVisible(sprite, layer, true);
        _sprite.LayerSetRsiState(sprite, layer, layerState);
        if (layerColor != null)
            _sprite.LayerSetColor(sprite, layer, layerColor.Value);
    }

    private void TransformLayer(string layer, Vector2 movement, Angle rotation, Entity<SpriteComponent?> sprite)
    {
        _sprite.LayerSetOffset(sprite, layer, movement);
        _sprite.LayerSetRotation(sprite, layer, rotation);
    }
}
