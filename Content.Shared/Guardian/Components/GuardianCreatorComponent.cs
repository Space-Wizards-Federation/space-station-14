using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Guardian.Components
{
    /// <summary>
    /// Creates a GuardianComponent attached to the user's GuardianHost.
    /// </summary>
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class GuardianCreatorComponent : Component
    {
        /// <summary>
        /// Counts as spent upon exhausting the injection
        /// </summary>
        /// <remarks>
        /// We don't mark as deleted as examine depends on this.
        /// </remarks>
        [DataField, AutoNetworkedField]
        public bool Used;

        /// <remarks>
        /// For methods done through injection.
        /// </remarks>
        [DataField, AutoNetworkedField]
        public bool Injector;

        /// <remarks>
        /// When the guardian creator has been used.
        /// </remarks>
        [DataField]
        public LocId InjectorEmptyPopup = "guardian-injector-empty-invalid-creation";

        /// <remarks>
        /// Description of the used guardian creator.
        /// </remarks>
        [DataField]
        public LocId InjectorEmptyExamine = "guardian-injector-empty-examine";

        /// <remarks>
        /// For methods obtained through a deck.
        /// </remarks>
        [DataField, AutoNetworkedField]
        public bool Deck;

        /// <remarks>
        /// Shows when the deck can't produce a guardian.
        /// </remarks>
        [DataField]
        public LocId DeckUsedPopup = "guardian-deck-invalid-creation";

        /// <remarks>
        /// Shows if the deck has been used before.
        /// </remarks>
        [DataField]
        public LocId DeckUsedExamine = "guardian-deck-used-examine";

        /// <remarks>
        /// Text shown to the host indicating the creation was successful.
        /// </remarks>
        [DataField]
        public LocId GuardianHauntedPopup = "guardian-created";

        /// <summary>
        /// The prototype of the guardian entity which will be created
        /// </summary>
        [DataField(required: true)]
        public EntProtoId? GuardianProto { get; set; }

        /// <summary>
        /// How long it takes to inject someone.
        /// </summary>
        [DataField("delay")]
        public float InjectionDelay = 5f;
    }
}
