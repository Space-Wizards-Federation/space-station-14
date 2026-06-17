using System.Numerics;
using Content.Client.Examine;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared.Wires;
using Robust.Client.Animations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Animations;
using Robust.Shared.Input;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Wires.UI
{
    public sealed partial class WiresMenu : BaseWindow
    {
        [Dependency] private IResourceCache _resourceCache = default!;

        private readonly Control _wiresHBox;
        private readonly Control _topContainer;
        private readonly Control _statusContainer;

        private readonly Label _nameLabel;
        private readonly Label _serialLabel;
        private readonly List<WireControl> _wireControls = new();
        private readonly List<int> _wireIds = new();
        private readonly List<Control> _statusControls = new();
        private readonly List<object> _statusKeys = new();

        public TextureButton CloseButton { get; set; }

        public event Action<int, WiresAction>? OnAction;

        public WiresMenu()
        {
            IoCManager.InjectDependencies(this);

            var rootContainer = new LayoutContainer {Name = "WireRoot"};
            AddChild(rootContainer);

            MouseFilter = MouseFilterMode.Stop;

            var panelTex = _resourceCache.GetTexture("/Textures/Interface/Nano/button.svg.96dpi.png");
            var back = new StyleBoxTexture
            {
                Texture = panelTex,
                Modulate = Color.FromHex("#25252A"),
            };
            back.SetPatchMargin(StyleBox.Margin.All, 10);

            var topPanel = new PanelContainer
            {
                PanelOverride = back,
                MouseFilter = MouseFilterMode.Pass
            };
            var bottomWrap = new LayoutContainer
            {
                Name = "BottomWrap"
            };
            var bottomPanel = new PanelContainer
            {
                PanelOverride = back,
                MouseFilter = MouseFilterMode.Pass
            };

            var shadow = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Children =
                {
                    new PanelContainer
                    {
                        MinSize = new Vector2(2, 0),
                        PanelOverride = new StyleBoxFlat {BackgroundColor = Color.FromHex("#525252ff")}
                    },
                    new PanelContainer
                    {
                        HorizontalExpand = true,
                        MouseFilter = MouseFilterMode.Stop,
                        Name = "Shadow",
                        PanelOverride = new StyleBoxFlat {BackgroundColor = Color.Black.WithAlpha(0.5f)}
                    },
                    new PanelContainer
                    {
                        MinSize = new Vector2(2, 0),
                        PanelOverride = new StyleBoxFlat {BackgroundColor = Color.FromHex("#525252ff")}
                    },
                }
            };

            var wrappingHBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal
            };
            _wiresHBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 4,
                VerticalAlignment = VAlignment.Bottom
            };

            wrappingHBox.AddChild(new Control {MinSize = new Vector2(20, 0)});
            wrappingHBox.AddChild(_wiresHBox);
            wrappingHBox.AddChild(new Control {MinSize = new Vector2(20, 0)});

            bottomWrap.AddChild(bottomPanel);

            LayoutContainer.SetAnchorPreset(bottomPanel, LayoutContainer.LayoutPreset.BottomWide);
            LayoutContainer.SetMarginTop(bottomPanel, -55);

            bottomWrap.AddChild(shadow);

            LayoutContainer.SetAnchorPreset(shadow, LayoutContainer.LayoutPreset.BottomWide);
            LayoutContainer.SetMarginBottom(shadow, -55);
            LayoutContainer.SetMarginTop(shadow, -80);
            LayoutContainer.SetMarginLeft(shadow, 12);
            LayoutContainer.SetMarginRight(shadow, -12);

            bottomWrap.AddChild(wrappingHBox);
            LayoutContainer.SetAnchorPreset(wrappingHBox, LayoutContainer.LayoutPreset.Wide);
            LayoutContainer.SetMarginBottom(wrappingHBox, -4);

            rootContainer.AddChild(topPanel);
            rootContainer.AddChild(bottomWrap);

            LayoutContainer.SetAnchorPreset(topPanel, LayoutContainer.LayoutPreset.Wide);
            LayoutContainer.SetMarginBottom(topPanel, -80);

            LayoutContainer.SetAnchorPreset(bottomWrap, LayoutContainer.LayoutPreset.VerticalCenterWide);
            LayoutContainer.SetGrowHorizontal(bottomWrap, LayoutContainer.GrowDirection.Both);

            var topContainerWrap = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children =
                {
                    (_topContainer = new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical
                    }),
                    new Control {MinSize = new Vector2(0, 110)}
                }
            };

            rootContainer.AddChild(topContainerWrap);

            LayoutContainer.SetAnchorPreset(topContainerWrap, LayoutContainer.LayoutPreset.Wide);

            var font = _resourceCache.GetFont("/Fonts/Boxfont-round/Boxfont Round.ttf", 13);
            var fontSmall = _resourceCache.GetFont("/Fonts/Boxfont-round/Boxfont Round.ttf", 10);

            Button helpButton;
            var topRow = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(4, 2, 12, 2),
                Children =
                {
                    (_nameLabel = new Label
                    {
                        Text = Loc.GetString("wires-menu-name-label"),
                        FontOverride = font,
                        VerticalAlignment = VAlignment.Center,
                        StyleClasses = { StyleClass.LabelKeyText },
                    }),
                    (_serialLabel = new Label
                    {
                        Text = Loc.GetString("wires-menu-dead-beef-text"),
                        FontOverride = fontSmall,
                        FontColorOverride = Color.Gray,
                        VerticalAlignment = VAlignment.Center,
                        Margin = new Thickness(8, 0, 20, 0),
                        HorizontalAlignment = HAlignment.Left,
                        HorizontalExpand = true,
                    }),
                    (helpButton = new Button
                    {
                        Text = "?",
                        Margin = new Thickness(0, 0, 2, 0),
                    }),
                    (CloseButton = new TextureButton
                    {
                        StyleClasses = {DefaultWindow.StyleClassWindowCloseButton},
                        VerticalAlignment = VAlignment.Center
                    })
                }
            };

            helpButton.OnPressed += a =>
            {
                var popup = new HelpPopup();
                UserInterfaceManager.ModalRoot.AddChild(popup);

                popup.Open(UIBox2.FromDimensions(a.Event.PointerLocation.Position, new Vector2(400, 200)));
            };

            var middle = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat {BackgroundColor = Color.FromHex("#202025")},
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        Children =
                        {
                            (_statusContainer = new GridContainer
                            {
                                Margin = new Thickness(8, 4),
                                Rows = 2
                            })
                        }
                    }
                }
            };

            _topContainer.AddChild(topRow);
            _topContainer.AddChild(new PanelContainer
            {
                MinSize = new Vector2(0, 2),
                PanelOverride = new StyleBoxFlat {BackgroundColor = Color.FromHex("#525252ff")}
            });
            _topContainer.AddChild(middle);
            _topContainer.AddChild(new PanelContainer
            {
                MinSize = new Vector2(0, 2),
                PanelOverride = new StyleBoxFlat {BackgroundColor = Color.FromHex("#525252ff")}
            });
            CloseButton.OnPressed += _ => Close();
            SetHeight = 200;
            MinWidth = 320;
        }


        public void Populate(WiresComponent state)
        {
            _nameLabel.Text = state.LocalizedBoardName;
            _serialLabel.Text = state.SerialNumber;

            PopulateWireEntries(state);
            PopulateStatusEntries(state.StatusEntries);
        }

        private void PopulateWireEntries(WiresComponent state)
        {
            if (WireEntriesChanged(state.ClientWires))
                RebuildWireEntries(state);

            foreach (var wire in state.ClientWires)
            {
                for (var i = 0; i < _wireIds.Count; i++)
                {
                    if (_wireIds[i] != wire.Id)
                        continue;

                    _wireControls[i].SetCut(wire.IsCut);
                    break;
                }
            }
        }

        private bool WireEntriesChanged(IReadOnlyList<ClientWire> wires)
        {
            if (_wireControls.Count != wires.Count)
                return true;

            for (var i = 0; i < wires.Count; i++)
            {
                var wire = wires[i];

                if (_wireIds[i] != wire.Id ||
                    _wireControls[i].WireColor != wire.Color ||
                    _wireControls[i].Letter != wire.Letter)
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildWireEntries(WiresComponent state)
        {
            _wiresHBox.RemoveAllChildren();
            _wireControls.Clear();
            _wireIds.Clear();

            var random = new Random(state.WireSeed);
            foreach (var wire in state.ClientWires)
            {
                var mirror = random.Next(2) == 0;
                var flip = random.Next(2) == 0;
                var type = random.Next(2);
                var control = new WireControl(wire.Color, wire.Letter, wire.IsCut, flip, mirror, type, _resourceCache)
                {
                    VerticalAlignment = VAlignment.Bottom
                };
                _wiresHBox.AddChild(control);
                _wireControls.Add(control);
                _wireIds.Add(wire.Id);

                control.WireClicked += () =>
                {
                    OnAction?.Invoke(wire.Id, control.IsCut ? WiresAction.Mend : WiresAction.Cut);
                };

                control.ContactsClicked += () =>
                {
                    OnAction?.Invoke(wire.Id, WiresAction.Pulse);
                };
            }
        }

        private void PopulateStatusEntries(StatusEntry[] statusEntries)
        {
            if (StatusEntriesChanged(statusEntries))
                RebuildStatusEntries(statusEntries);

            for (var i = 0; i < statusEntries.Length; i++)
                UpdateStatusControl(_statusControls[i], statusEntries[i]);
        }

        private bool StatusEntriesChanged(StatusEntry[] statusEntries)
        {
            if (_statusControls.Count != statusEntries.Length)
                return true;

            for (var i = 0; i < statusEntries.Length; i++)
            {
                var status = statusEntries[i];

                if (!Equals(_statusKeys[i], status.Key))
                    return true;

                if ((status.Value is StatusLightData) != (_statusControls[i] is StatusLight))
                    return true;
            }

            return false;
        }

        private void RebuildStatusEntries(StatusEntry[] statusEntries)
        {
            _statusContainer.RemoveAllChildren();
            _statusControls.Clear();
            _statusKeys.Clear();

            foreach (var status in statusEntries)
            {
                var control = CreateStatusControl(status);
                _statusControls.Add(control);
                _statusKeys.Add(status.Key);
                _statusContainer.AddChild(control);
            }
        }

        private Control CreateStatusControl(StatusEntry status)
        {
            return status.Value is StatusLightData statusLightData
                ? new StatusLight(statusLightData, _resourceCache)
                : new Label();
        }

        private static void UpdateStatusControl(Control control, StatusEntry status)
        {
            if (status.Value is StatusLightData statusLightData && control is StatusLight light)
            {
                light.SetData(statusLightData);
                return;
            }

            if (control is Label label)
            {
                label.Text = status.ToString();
                return;
            }

            throw new InvalidOperationException("Status entry controls must be rebuilt before updating.");
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            return DragMode.Move;
        }

        protected override bool HasPoint(Vector2 point)
        {
            // This makes it so our base window won't count for hit tests,
            // but we will still receive mouse events coming in from Pass mouse filter mode.
            // So basically, it perfectly shells out the hit tests to the panels we have!
            return false;
        }

        private sealed class WireControl : Control
        {
            private IResourceCache _resourceCache;
            private readonly WireRender _wire;

            private const string TextureContact = "/Textures/Interface/WireHacking/contact.svg.96dpi.png";

            public event Action? WireClicked;
            public event Action? ContactsClicked;
            public WireColor WireColor { get; }
            public WireLetter Letter { get; }
            public bool IsCut { get; private set; }

            public WireControl(WireColor color, WireLetter letter, bool isCut, bool flip, bool mirror, int type,
                IResourceCache resourceCache)
            {
                _resourceCache = resourceCache;
                WireColor = color;
                Letter = letter;
                IsCut = isCut;

                HorizontalAlignment = HAlignment.Center;
                MouseFilter = MouseFilterMode.Stop;

                var layout = new LayoutContainer();
                AddChild(layout);

                var greek = new Label
                {
                    Text = letter.Letter().ToString(),
                    VerticalAlignment = VAlignment.Bottom,
                    HorizontalAlignment = HAlignment.Center,
                    Align = Label.AlignMode.Center,
                    FontOverride = _resourceCache.GetFont("/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf", 12),
                    FontColorOverride = Color.Gray,
                    ToolTip = letter.Name(),
                    MouseFilter = MouseFilterMode.Stop
                };

                layout.AddChild(greek);
                LayoutContainer.SetAnchorPreset(greek, LayoutContainer.LayoutPreset.BottomWide);
                LayoutContainer.SetGrowVertical(greek, LayoutContainer.GrowDirection.Begin);
                LayoutContainer.SetGrowHorizontal(greek, LayoutContainer.GrowDirection.Both);

                var contactTexture = _resourceCache.GetTexture(TextureContact);
                var contact1 = new TextureRect
                {
                    Texture = contactTexture,
                    Modulate = Color.FromHex("#E1CA76")
                };

                layout.AddChild(contact1);
                LayoutContainer.SetPosition(contact1, new Vector2(0, 0));

                var contact2 = new TextureRect
                {
                    Texture = contactTexture,
                    Modulate = Color.FromHex("#E1CA76")
                };

                layout.AddChild(contact2);
                LayoutContainer.SetPosition(contact2, new Vector2(0, 60));

                _wire = new WireRender(color, isCut, flip, mirror, type, _resourceCache);

                layout.AddChild(_wire);
                LayoutContainer.SetPosition(_wire, new Vector2(2, 16));

                ToolTip = color.Name();
                MinSize = new Vector2(20, 102);
            }

            public void SetCut(bool isCut)
            {
                if (IsCut == isCut)
                    return;

                IsCut = isCut;
                _wire.SetCut(isCut);
            }

            protected override void KeyBindDown(GUIBoundKeyEventArgs args)
            {
                base.KeyBindDown(args);

                if (args.Function != EngineKeyFunctions.UIClick)
                {
                    return;
                }

                if (args.RelativePosition.Y > 20 && args.RelativePosition.Y < 60)
                {
                    WireClicked?.Invoke();
                }
                else
                {
                    ContactsClicked?.Invoke();
                }
            }

            protected override bool HasPoint(Vector2 point)
            {
                return base.HasPoint(point) && point.Y <= 80;
            }

            private sealed class WireRender : Control
            {
                private readonly WireColor _color;
                private bool _isCut;
                private readonly bool _flip;
                private readonly bool _mirror;
                private readonly int _type;

                private static readonly string[] TextureNormal =
                {
                    "/Textures/Interface/WireHacking/wire_1.svg.96dpi.png",
                    "/Textures/Interface/WireHacking/wire_2.svg.96dpi.png"
                };

                private static readonly string[] TextureCut =
                {
                    "/Textures/Interface/WireHacking/wire_1_cut.svg.96dpi.png",
                    "/Textures/Interface/WireHacking/wire_2_cut.svg.96dpi.png",
                };

                private static readonly string[] TextureCopper =
                {
                    "/Textures/Interface/WireHacking/wire_1_copper.svg.96dpi.png",
                    "/Textures/Interface/WireHacking/wire_2_copper.svg.96dpi.png"
                };

                private readonly IResourceCache _resourceCache;

                public WireRender(WireColor color, bool isCut, bool flip, bool mirror, int type,
                    IResourceCache resourceCache)
                {
                    _resourceCache = resourceCache;
                    _color = color;
                    _isCut = isCut;
                    _flip = flip;
                    _mirror = mirror;
                    _type = type;

                    SetSize = new Vector2(16, 50);
                }

                public void SetCut(bool isCut)
                {
                    _isCut = isCut;
                }

                protected override void Draw(DrawingHandleScreen handle)
                {
                    var colorValue = _color.ColorValue();
                    var tex = _resourceCache.GetTexture(_isCut ? TextureCut[_type] : TextureNormal[_type]);

                    var l = 0f;
                    var r = tex.Width + l;
                    var t = 0f;
                    var b = tex.Height + t;

                    if (_flip)
                    {
                        (t, b) = (b, t);
                    }

                    if (_mirror)
                    {
                        (l, r) = (r, l);
                    }

                    l *= UIScale;
                    r *= UIScale;
                    t *= UIScale;
                    b *= UIScale;

                    var rect = new UIBox2(l, t, r, b);
                    if (_isCut)
                    {
                        var copper = Color.Orange;
                        var copperTex = _resourceCache.GetTexture(TextureCopper[_type]);
                        handle.DrawTextureRect(copperTex, rect, copper);
                    }

                    handle.DrawTextureRect(tex, rect, colorValue);
                }
            }
        }

        private sealed class StatusLight : Control
        {
            private const string BlinkAnimationKey = "blink";

            private static readonly Animation _blinkingFast = new()
            {
                Length = TimeSpan.FromSeconds(0.2),
                AnimationTracks =
                {
                    new AnimationTrackControlProperty
                    {
                        Property = nameof(Control.Modulate),
                        InterpolationMode = AnimationInterpolationMode.Linear,
                        KeyFrames =
                        {
                            new AnimationTrackProperty.KeyFrame(Color.White, 0f),
                            new AnimationTrackProperty.KeyFrame(Color.Transparent, 0.1f),
                            new AnimationTrackProperty.KeyFrame(Color.White, 0.1f)
                        }
                    }
                }
            };

            private static readonly Animation _blinkingSlow = new()
            {
                Length = TimeSpan.FromSeconds(0.8),
                AnimationTracks =
                {
                    new AnimationTrackControlProperty
                    {
                        Property = nameof(Control.Modulate),
                        InterpolationMode = AnimationInterpolationMode.Linear,
                        KeyFrames =
                        {
                            new AnimationTrackProperty.KeyFrame(Color.White, 0f),
                            new AnimationTrackProperty.KeyFrame(Color.White, 0.3f),
                            new AnimationTrackProperty.KeyFrame(Color.Transparent, 0.1f),
                            new AnimationTrackProperty.KeyFrame(Color.Transparent, 0.3f),
                            new AnimationTrackProperty.KeyFrame(Color.White, 0.1f),
                        }
                    }
                }
            };

            private readonly TextureRect _inactiveLight;
            private readonly TextureRect _activeLight;
            private readonly Label _label;
            private StatusLightData? _data;
            private StatusLightState? _animationState;

            public StatusLight(StatusLightData data, IResourceCache resourceCache)
            {
                HorizontalAlignment = HAlignment.Right;

                var lightContainer = new Control
                {
                    SetSize = new Vector2(20, 20),
                    Children =
                    {
                        (_inactiveLight = new TextureRect
                        {
                            Texture = resourceCache.GetTexture(
                                "/Textures/Interface/WireHacking/light_off_base.svg.96dpi.png"),
                            Stretch = TextureRect.StretchMode.KeepCentered,
                        }),
                        (_activeLight = new TextureRect
                        {
                            Stretch = TextureRect.StretchMode.KeepCentered,
                            Texture =
                                resourceCache.GetTexture("/Textures/Interface/WireHacking/light_on_base.svg.96dpi.png"),
                        })
                    }
                };

                _activeLight.AnimationCompleted += OnAnimationCompleted;

                var font = resourceCache.GetFont("/Fonts/Boxfont-round/Boxfont Round.ttf", 12);

                var hBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    SeparationOverride = 4
                };
                hBox.AddChild(_label = new Label
                {
                    FontOverride = font,
                    FontColorOverride = Color.FromHex("#A1A6AE"),
                    VerticalAlignment = VAlignment.Center,
                });
                hBox.AddChild(lightContainer);
                hBox.AddChild(new Control {MinSize = new Vector2(6, 0)});
                AddChild(hBox);

                SetData(data);
            }

            public void SetData(StatusLightData data)
            {
                if (_data is { } oldData &&
                    oldData.Color == data.Color &&
                    oldData.State == data.State &&
                    oldData.Text == data.Text)
                {
                    return;
                }

                _data = data;
                _label.Text = data.Text;

                var hsv = Color.ToHsv(data.Color);
                hsv.Z /= 2;
                _inactiveLight.ModulateSelfOverride = Color.FromHsv(hsv);
                _activeLight.ModulateSelfOverride = data.Color.WithAlpha(0.4f);
                _activeLight.Visible = data.State != StatusLightState.Off;

                UpdateAnimation(data.State);
            }

            private void UpdateAnimation(StatusLightState state)
            {
                if (_animationState == state)
                    return;

                _animationState = state;
                _activeLight.StopAnimation(BlinkAnimationKey);
                _activeLight.Modulate = Color.White;

                if (GetAnimation(state) is { } animation)
                    _activeLight.PlayAnimation(animation, BlinkAnimationKey);
            }

            private static Animation? GetAnimation(StatusLightState state)
            {
                return state switch
                {
                    StatusLightState.Off or StatusLightState.On => null,
                    StatusLightState.BlinkingFast => _blinkingFast,
                    StatusLightState.BlinkingSlow => _blinkingSlow,
                    _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
                };
            }

            private void OnAnimationCompleted(string key)
            {
                if (key != BlinkAnimationKey ||
                    _data is not { } data ||
                    GetAnimation(data.State) is not { } animation)
                {
                    return;
                }

                _activeLight.PlayAnimation(animation, key);
            }
        }

        private sealed class HelpPopup : Popup
        {
            public HelpPopup()
            {
                var label = new RichTextLabel();
                label.SetMessage(Loc.GetString("wires-menu-help-popup"));
                AddChild(new PanelContainer
                {
                    StyleClasses = {ExamineSystem.StyleClassEntityTooltip},
                    Children = {label}
                });
            }
        }
    }
}
