using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Wall;
using Robust.Shared.Map.Components;

using static Content.Server.NPC.Pathfinding.PathfindingSystem;

namespace Content.Server.Power.EntitySystems
{
    public sealed partial class ExtensionCableSystem : EntitySystem
    {
        [Dependency] private AtmosphereSystem _atmosphere = default!;
        [Dependency] private SharedMapSystem _map = default!;
        [Dependency] private EntityQuery<ApcComponent> _apcQuery;
        [Dependency] private EntityQuery<ExtensionCableProviderComponent> _cableProviderQuery;
        [Dependency] private EntityQuery<ExtensionCableReceiverComponent> _cableReceiverQuery;
        [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery;
        [Dependency] private EntityQuery<TransformComponent> _xformQuery;
        [Dependency] private EntityQuery<WallMountComponent> _wallMountQuery;

        public override void Initialize()
        {
            base.Initialize();

            //Lifecycle events
            SubscribeLocalEvent<ExtensionCableProviderComponent, MapInitEvent>(OnProviderStarted);
            SubscribeLocalEvent<ExtensionCableProviderComponent, ComponentShutdown>(OnProviderShutdown);
            SubscribeLocalEvent<ExtensionCableReceiverComponent, MapInitEvent>(OnReceiverStarted);
            SubscribeLocalEvent<ExtensionCableReceiverComponent, ComponentShutdown>(OnReceiverShutdown);

            //Anchoring
            SubscribeLocalEvent<ExtensionCableReceiverComponent, AnchorStateChangedEvent>(OnReceiverAnchorStateChanged);
            SubscribeLocalEvent<ExtensionCableReceiverComponent, ReAnchorEvent>(OnReceiverReAnchor);

            SubscribeLocalEvent<ExtensionCableProviderComponent, AnchorStateChangedEvent>(OnProviderAnchorStateChanged);
            SubscribeLocalEvent<ExtensionCableProviderComponent, ReAnchorEvent>(OnProviderReAnchor);
        }

        #region Provider

        public void SetProviderTransferRange(EntityUid uid, int range, ExtensionCableProviderComponent? provider = null)
        {
            if (!Resolve(uid, ref provider))
                return;

            provider.TransferRange = range;
            ResetReceivers((uid, provider));
        }

        private void OnProviderStarted(Entity<ExtensionCableProviderComponent> provider, ref MapInitEvent args)
        {
            Connect(provider);
        }

        private void OnProviderShutdown(Entity<ExtensionCableProviderComponent> provider, ref ComponentShutdown args)
        {
            var xform = Transform(provider);

            // If grid deleting no need to update power.
            if (_mapGridQuery.HasComp(xform.GridUid) &&
                MetaData(xform.GridUid.Value).EntityLifeStage > EntityLifeStage.MapInitialized)
            {
                return;
            }

            Disconnect(provider);
        }

        private void OnProviderAnchorStateChanged(Entity<ExtensionCableProviderComponent> provider, ref AnchorStateChangedEvent args)
        {
            if (!IsMapInitialized(provider.Owner))
                return;

            if (args.Anchored)
                Connect(provider);
            else
                Disconnect(provider);
        }

        private void Connect(Entity<ExtensionCableProviderComponent> provider)
        {
            provider.Comp.Connectable = true;

            foreach (var receiver in FindNearbyUnconnectedReceivers(provider))
            {
                TryFindAndSetProvider(receiver);
            }
        }

        private void Disconnect(Entity<ExtensionCableProviderComponent> provider)
        {
            // same as OnProviderShutdown
            provider.Comp.Connectable = false;
            ResetReceivers(provider);
        }

        private void OnProviderReAnchor(Entity<ExtensionCableProviderComponent> provider, ref ReAnchorEvent args)
        {
            if (!IsMapInitialized(provider.Owner))
                return;

            Disconnect(provider);
            Connect(provider);
        }

        private void ResetReceivers(Entity<ExtensionCableProviderComponent> provider)
        {
            var providerId = provider.Owner;
            var receivers = provider.Comp.LinkedReceivers.ToArray();
            provider.Comp.LinkedReceivers.Clear();

            foreach (var receiver in receivers)
            {
                var receiverId = receiver.Owner;
                receiver.Comp.Provider = null;
                RaiseLocalEvent(receiverId, new ProviderDisconnectedEvent(provider), broadcast: false);
                RaiseLocalEvent(providerId, new ReceiverDisconnectedEvent((receiverId, receiver)), broadcast: false);
            }

            foreach (var receiver in receivers)
            {
                // No point resetting what the receiver is doing if it's deleting, plus significant perf savings
                // in not doing needless lookups
                var receiverId = receiver.Owner;
                if (!EntityManager.IsQueuedForDeletion(receiverId)
                    && MetaData(receiverId).EntityLifeStage <= EntityLifeStage.MapInitialized)
                {
                    TryFindAndSetProvider(receiver);
                }
            }
        }

        private IEnumerable<Entity<ExtensionCableReceiverComponent>> FindNearbyUnconnectedReceivers(Entity<ExtensionCableProviderComponent> provider)
        {
            if (!_xformQuery.TryComp(provider.Owner, out var xform)
                || xform.GridUid is not { } gridUid
                || !_mapGridQuery.TryComp(gridUid, out var gridComp))
            {
                yield break;
            }

            Entity<MapGridComponent> grid = (gridUid, gridComp);
            var tile = GetCableReachabilityTile(provider.Owner, xform, grid);
            var coordinates = _map.GridTileToLocal(gridUid, gridComp, tile);

            // Receiver wall mounts can path from the floor tile in front of them.
            var nearbyEntities = _map.GetCellsInSquareArea(gridUid, gridComp, coordinates, provider.Comp.TransferRange + 1);

            foreach (var entity in nearbyEntities)
            {
                if (entity == provider.Owner)
                    continue;

                if (EntityManager.IsQueuedForDeletion(entity) || MetaData(entity).EntityLifeStage > EntityLifeStage.MapInitialized)
                    continue;

                if (!_cableReceiverQuery.TryComp(entity, out var receiver))
                    continue;

                if (!receiver.Connectable || receiver.Provider != null)
                    continue;

                yield return (entity, receiver);
            }
        }

        #endregion

        #region Receiver

        public void SetReceiverReceptionRange(EntityUid uid, int range, ExtensionCableReceiverComponent? receiver = null)
        {
            if (!Resolve(uid, ref receiver))
                return;

            var provider = receiver.Provider;
            receiver.Provider = null;
            RaiseLocalEvent(uid, new ProviderDisconnectedEvent(provider), broadcast: false);

            if (provider != null)
            {
                RaiseLocalEvent(provider.Value, new ReceiverDisconnectedEvent((uid, receiver)), broadcast: false);
                provider.Value.Comp.LinkedReceivers.Remove((uid, receiver));
            }

            receiver.ReceptionRange = range;
            TryFindAndSetProvider((uid, receiver));
        }

        private void OnReceiverStarted(Entity<ExtensionCableReceiverComponent> receiver, ref MapInitEvent args)
        {
            if (!_xformQuery.TryComp(receiver.Owner, out var xform) || !xform.Anchored)
                return;

            Connect(receiver);
        }

        private void OnReceiverShutdown(Entity<ExtensionCableReceiverComponent> receiver, ref ComponentShutdown args)
        {
            Disconnect(receiver);
        }

        private void OnReceiverAnchorStateChanged(Entity<ExtensionCableReceiverComponent> receiver, ref AnchorStateChangedEvent args)
        {
            if (!IsMapInitialized(receiver.Owner))
                return;

            if (args.Anchored)
            {
                Connect(receiver);
            }
            else
            {
                Disconnect(receiver);
            }
        }

        private void OnReceiverReAnchor(Entity<ExtensionCableReceiverComponent> receiver, ref ReAnchorEvent args)
        {
            if (!IsMapInitialized(receiver.Owner))
                return;

            Disconnect(receiver);
            Connect(receiver);
        }

        private void Connect(Entity<ExtensionCableReceiverComponent> receiver)
        {
            receiver.Comp.Connectable = true;
            TryFindAndSetProvider(receiver);
        }

        private void Disconnect(Entity<ExtensionCableReceiverComponent> receiver)
        {
            receiver.Comp.Connectable = false;
            RaiseLocalEvent(receiver, new ProviderDisconnectedEvent(receiver.Comp.Provider), broadcast: false);
            if (receiver.Comp.Provider != null)
            {
                RaiseLocalEvent(receiver.Comp.Provider.Value, new ReceiverDisconnectedEvent(receiver), broadcast: false);
                receiver.Comp.Provider.Value.Comp.LinkedReceivers.Remove(receiver);
            }

            receiver.Comp.Provider = null;
        }

        private void TryFindAndSetProvider(Entity<ExtensionCableReceiverComponent> receiver, TransformComponent? xform = null)
        {
            if (!receiver.Comp.Connectable || receiver.Comp.Provider != null)
                return;

            if (!TryFindAvailableProvider(
                    receiver.Owner,
                    receiver.Comp.ReceptionRange,
                    out var provider,
                    xform))
                return;

            receiver.Comp.Provider = provider;
            provider.Value.Comp.LinkedReceivers.Add(receiver);
            RaiseLocalEvent(receiver, new ProviderConnectedEvent(provider), broadcast: false);
            RaiseLocalEvent(provider.Value, new ReceiverConnectedEvent(receiver), broadcast: false);
        }

        private bool TryFindAvailableProvider(
            EntityUid owner,
            int range,
            [NotNullWhen(true)] out Entity<ExtensionCableProviderComponent>? foundProvider,
            TransformComponent? xform = null)
        {
            if (!Resolve(owner, ref xform)
                || xform.GridUid is not { } gridUid
                || !TryComp(gridUid, out MapGridComponent? gridComp))
            {
                foundProvider = null;
                return false;
            }

            Entity<MapGridComponent> grid = (gridUid, gridComp);

            var start = GetCableReachabilityTile(owner, xform, grid);
            var coordinates = _map.GridTileToLocal(gridUid, gridComp, start);
            var nearbyEntities = _map.GetCellsInSquareArea(gridUid, gridComp, coordinates, range + 1);

            var candidates = new Dictionary<Vector2i, Entity<ExtensionCableProviderComponent>>();
            foreach (var entity in nearbyEntities)
            {
                if (entity == owner || !_cableProviderQuery.TryGetComponent(entity, out var provider) || !provider.Connectable)
                    continue;

                if (EntityManager.IsQueuedForDeletion(entity))
                    continue;

                if (!TryComp(entity, out MetaDataComponent? meta) || meta.EntityLifeStage > EntityLifeStage.MapInitialized)
                    continue;

                // Find the closest provider
                if (!TryComp(entity, out TransformComponent? entityXform))
                    continue;

                var gridPos = GetCableReachabilityTile(entity, entityXform, grid);
                var providerRange = Math.Min(range, provider.TransferRange);
                var pd = gridPos - start;

                // ensure in range bidirectionally
                if (pd.X * pd.X + pd.Y * pd.Y > providerRange * providerRange)
                    continue;

                if (!candidates.TryGetValue(gridPos, out var existing)
                    || (!_apcQuery.HasComp(existing.Owner) && _apcQuery.HasComp(entity)))
                {
                    candidates[gridPos] = (entity, provider);
                }
            }

            if (candidates.Count == 0)
            {
                foundProvider = null;
                return false;
            }

            var sqRange = range * range;
            // this number is the number of tiles in the Chebyshev region defined by the range. This is also the number
            // of tiles in GetCellsInSquareArea invoked above.
            var maxRegionSize = (2 * range + 1) * (2 * range + 1);

            var result = GetBreadthPath(new BreadthPathArgs
            {
                Start = start,
                Ends = candidates.Keys.ToList(),
                Diagonals = false,
                Limit = maxRegionSize,
                EdgeMultiplier = (from, to) =>
                {
                    if (!_map.TryGetTile(grid, to, out var toTile) || toTile.IsEmpty)
                        return 0f;

                    // enforce range limit
                    var delta = to - start;
                    if (delta.X * delta.X + delta.Y * delta.Y > sqRange)
                        return 0f;

                    var dir = (to - from).GetCardinalDir().ToAtmosDirection();
                    var isBlocked = _atmosphere.IsTileAirBlocked(gridUid, from, dir, gridComp)
                                    || _atmosphere.IsTileAirBlocked(gridUid, to, dir.GetOpposite(), gridComp);

                    return isBlocked ? 0f : 1f;
                },
            });

            if (result.Path is { Count: > 0 })
            {
                foundProvider = candidates[result.Path[^1]];
                return true;
            }

            foundProvider = null;
            return false;
        }

        /// <summary>
        /// Adjust cable pathing tile to account for wall mount devices
        /// </summary>
        private Vector2i GetCableReachabilityTile(
            EntityUid uid,
            TransformComponent xform,
            Entity<MapGridComponent> grid)
        {
            var tile = _map.TileIndicesFor(grid, xform.Coordinates);

            if (!_wallMountQuery.TryComp(uid, out var wallMount))
                return tile;

            var dir = (wallMount.Direction + xform.LocalRotation).GetCardinalDir();
            var frontTile = tile.Offset(dir);

            return _map.TryGetTile(grid, frontTile, out var mapTile) && !mapTile.IsEmpty
                ? frontTile
                : tile;
        }

        private bool IsMapInitialized(EntityUid uid)
        {
            return MetaData(uid).EntityLifeStage >= EntityLifeStage.MapInitialized;
        }

        #endregion

        #region Events

        /// <summary>
        /// Sent when a <see cref="ExtensionCableProviderComponent"/> connects to a <see cref="ExtensionCableReceiverComponent"/>
        /// </summary>
        public sealed class ProviderConnectedEvent : EntityEventArgs
        {
            /// <summary>
            /// The <see cref="ExtensionCableProviderComponent"/> that connected.
            /// </summary>
            public ExtensionCableProviderComponent Provider;

            public ProviderConnectedEvent(ExtensionCableProviderComponent provider)
            {
                Provider = provider;
            }
        }
        /// <summary>
        /// Sent when a <see cref="ExtensionCableProviderComponent"/> disconnects from a <see cref="ExtensionCableReceiverComponent"/>
        /// </summary>
        public sealed class ProviderDisconnectedEvent : EntityEventArgs
        {
            /// <summary>
            /// The <see cref="ExtensionCableProviderComponent"/> that disconnected.
            /// </summary>
            public ExtensionCableProviderComponent? Provider;

            public ProviderDisconnectedEvent(ExtensionCableProviderComponent? provider)
            {
                Provider = provider;
            }
        }
        /// <summary>
        /// Sent when a <see cref="ExtensionCableReceiverComponent"/> connects to a <see cref="ExtensionCableProviderComponent"/>
        /// </summary>
        public sealed class ReceiverConnectedEvent : EntityEventArgs
        {
            /// <summary>
            /// The <see cref="ExtensionCableReceiverComponent"/> that connected.
            /// </summary>
            public Entity<ExtensionCableReceiverComponent> Receiver;

            public ReceiverConnectedEvent(Entity<ExtensionCableReceiverComponent> receiver)
            {
                Receiver = receiver;
            }
        }
        /// <summary>
        /// Sent when a <see cref="ExtensionCableReceiverComponent"/> disconnects from a <see cref="ExtensionCableProviderComponent"/>
        /// </summary>
        public sealed class ReceiverDisconnectedEvent : EntityEventArgs
        {
            /// <summary>
            /// The <see cref="ExtensionCableReceiverComponent"/> that disconnected.
            /// </summary>
            public Entity<ExtensionCableReceiverComponent> Receiver;

            public ReceiverDisconnectedEvent(Entity<ExtensionCableReceiverComponent> receiver)
            {
                Receiver = receiver;
            }
        }

        #endregion
    }
}
