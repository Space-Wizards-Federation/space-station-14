using System.Numerics;
using Content.Shared.Cards;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using static Robust.Client.UserInterface.Control;

namespace Content.Client.Cards;

public sealed partial class CardSystem
{
    public event Action<(Entity<CardsComponent> cards, EntityUid user, int cardInx)>? OnCardButtonClicked;
    public event Action<(Entity<CardsComponent> cards, int amount)>? OnCycleClick;
    public event Action<Entity<CardsComponent>>? OnFlipButtonClicked;
    public event Action<Entity<CardsComponent>>? OnFanButtonClicked;

    private CardSpriteView? _spriteView;

    protected override void OpenInspectUI(EntityUid player, Entity<CardsComponent> cards)
    {
        CardInspect menu = new CardInspect();

        _spriteView = new CardSpriteView(_playerManager)
        {
            OverrideDirection = Direction.South,
            Scale = new Vector2(3, 3),
            VerticalExpand = true,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
        };
        _spriteView.SetEntity(cards);
        _spriteView.SetVisualStateFunc(GetCardListVisualState);
        menu.SpriteHolder.AddChild(_spriteView);
        menu.CycleLeft.OnPressed += _ => OnCycleClick?.Invoke((cards, 1));
        menu.CycleRight.OnPressed += _ => OnCycleClick?.Invoke((cards, -1));
        menu.Flip.OnPressed += _ => OnFlipButtonClicked?.Invoke(cards);
        menu.Fan.OnPressed += _ => OnFanButtonClicked?.Invoke(cards);
        _examineSystem.SendExamineControl(player, cards.Owner, menu, false);
        _spriteView.SetCards(cards, 12, 20, OnCardButtonClicked);
    }

    public sealed class CardSpriteView : SpriteView
    {
        private readonly LayoutContainer _buttonOverlay;
        private readonly ISharedPlayerManager _playerManager;
        public Entity<CardsComponent>? _cards;
        private float _cardWidth;
        private float _cardHeight;
        private Action<(Entity<CardsComponent> cards, EntityUid user, int cardInx)>? _onCardClicked;
        private Func<CardsComponent, CardListVisualState>? _getVisualState;

        private CardListVisualState VisualState
        {
            get
            {
                if (_cards == null || _getVisualState == null)
                    return new CardListVisualState(new List<CardData>(), 0, 0);
                return _getVisualState(_cards.Value.Comp);
            }
        }

        public CardSpriteView(ISharedPlayerManager playerManager)
        {
            _playerManager = playerManager;
            _buttonOverlay = new LayoutContainer
            {
                MouseFilter = MouseFilterMode.Ignore,
                VerticalExpand = true,
                HorizontalExpand = true,
                HorizontalAlignment = HAlignment.Center,
            };
            AddChild(_buttonOverlay);
        }

        public void SetVisualStateFunc(Func<CardsComponent, CardListVisualState> getVisualState)
        {
            _getVisualState = getVisualState;
        }

        public void SetCards(
            Entity<CardsComponent> cards,
            float cardWidth,
            float cardHeight,
            Action<(Entity<CardsComponent> cards, EntityUid user, int cardInx)>? onCardClicked
        )
        {
            _cards = cards;
            _cardWidth = cardWidth;
            _cardHeight = cardHeight;
            _onCardClicked = onCardClicked;
            RebuildButtons();
        }

        public void UpdateCards(Entity<CardsComponent> cards)
        {
            _cards = cards;
            RebuildButtons();
        }

        protected override void Resized()
        {
            base.Resized();
            _buttonOverlay.SetSize = Size;
            RebuildButtons();
        }

        private float GetSpriteToUIScale()
        {
            if (Sprite == null)
                return 1f;
            return EyeManager.PixelsPerMeter * Scale.X;
        }

        private void RebuildButtons()
        {
            _buttonOverlay.RemoveAllChildren();

            var visualState = VisualState;
            var count = visualState.Count;
            if (count == 0)
                return;

            var user = _playerManager.LocalSession?.AttachedEntity;
            if (user == null)
                return;

            var center = new Vector2(Size.X / 2f, Size.Y / 2f);
            var spriteScale = GetSpriteToUIScale();
            var buttonScale = Scale.X;
            var radius = CardSystem.FanRadius(count);

            for (var i = 0; i < count; i++)
            {
                var card = visualState.CardList[visualState.Start + i];
                var (offset, rotation) = CardSystem.GetCardPosRot(i, count, radius);
                offset.Y *= -1;
                rotation *= -1;

                var button = new CardHoverButton(rotation, _cardWidth * buttonScale, _cardHeight * buttonScale);
                button.OnPressed += _ => _onCardClicked?.Invoke((_cards!.Value, user.Value, card.CardInx));

                var screenPos = center + offset * spriteScale;
                LayoutContainer.SetPosition(
                    button,
                    screenPos - new Vector2(button.SetSize.X / 2f, button.SetSize.Y / 2f)
                );
                _buttonOverlay.AddChild(button);
            }
        }
    }

    public sealed class CardHoverButton : ContainerButton
    {
        private bool _hovered;
        private readonly Angle _rotation;

        public CardHoverButton(Angle rotation, float width, float height)
        {
            _rotation = rotation;
            SetSize = new Vector2(width, height);
            MouseFilter = MouseFilterMode.Stop;
            OnMouseEntered += _ =>
            {
                _hovered = true;
                UpdateDraw();
            };
            OnMouseExited += _ =>
            {
                _hovered = false;
                UpdateDraw();
            };
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            if (!_hovered)
                return;

            var center = new Vector2(PixelSizeBox.Width / 2f, PixelSizeBox.Height / 2f);
            var halfW = PixelSizeBox.Width / 2f;
            var halfH = PixelSizeBox.Height / 2f;

            Vector2 RotatePoint(Vector2 point)
            {
                var cos = (float)Math.Cos(_rotation.Theta);
                var sin = (float)Math.Sin(_rotation.Theta);
                var dx = point.X - center.X;
                var dy = point.Y - center.Y;
                return new Vector2(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
            }

            var tl = RotatePoint(new Vector2(center.X - halfW, center.Y - halfH));
            var tr = RotatePoint(new Vector2(center.X + halfW, center.Y - halfH));
            var br = RotatePoint(new Vector2(center.X + halfW, center.Y + halfH));
            var bl = RotatePoint(new Vector2(center.X - halfW, center.Y + halfH));

            handle.DrawLine(tl, tr, Color.White);
            handle.DrawLine(tr, br, Color.White);
            handle.DrawLine(br, bl, Color.White);
            handle.DrawLine(bl, tl, Color.White);
        }

        protected override bool HasPoint(Vector2 point)
        {
            var center = new Vector2(Size.X / 2f, Size.Y / 2f);
            var halfW = Size.X / 2f;
            var halfH = Size.Y / 2f;

            // Translate point to be relative to center
            var dx = point.X - center.X;
            var dy = point.Y - center.Y;

            // Rotate point into local unrotated space
            var cos = (float)Math.Cos(-_rotation.Theta);
            var sin = (float)Math.Sin(-_rotation.Theta);
            var localX = dx * cos - dy * sin;
            var localY = dx * sin + dy * cos;

            // Check if point is within unrotated rectangle bounds
            return Math.Abs(localX) <= halfW && Math.Abs(localY) <= halfH;
        }
    }
}
