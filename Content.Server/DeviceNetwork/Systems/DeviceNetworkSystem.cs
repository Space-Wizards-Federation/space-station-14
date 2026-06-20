using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;
using System.Buffers;
using Content.Server.GameTicking.Events;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.GameTicking;
using Robust.Server.GameStates;

namespace Content.Server.DeviceNetwork.Systems;

/// <summary>
///     Entity system that handles everything device network related.
///     Device networking allows machines and devices to communicate with each other while adhering to restrictions like range or being connected to the same powernet.
/// </summary>
[UsedImplicitly]
public sealed partial class DeviceNetworkSystem : SharedDeviceNetworkSystem
{
    [Dependency] private DeviceListSystem _deviceLists = default!;
    [Dependency] private NetworkConfiguratorSystem _configurator = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<DeviceNetworkManagerComponent, MapInitEvent>(OnManagerInit);
        SubscribeLocalEvent<DeviceNetworkComponent, ComponentShutdown>(OnNetworkShutdown);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        EnsureManager();
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        ClearManager();
    }

    private void OnManagerInit(Entity<DeviceNetworkManagerComponent> ent, ref MapInitEvent args)
    {
        _pvsOverride.AddGlobalOverride(ent);
    }

    private void ClearManager()
    {
        if (TryGetManager(out var found))
            Del(found);
    }

    /// <summary>
    /// Removes the <see cref="DeviceNetworkManagerComponent"/> if it no longer has any entities in its networks.
    /// </summary>
    private void CheckClearManager()
    {
        if (!TryGetManager(out var found))
            return;

        foreach (var network in found.Value.Comp.Networks.Values)
        {
            if (network.Devices.Count != 0)
                return;
        }

        Del(found);
    }

    /// <summary>
    /// Automatically disconnect when an entity with a DeviceNetworkComponent shuts down.
    /// </summary>
    private void OnNetworkShutdown(Entity<DeviceNetworkComponent> ent, ref ComponentShutdown args)
    {
        var component = ent.Comp;
        foreach (var list in component.DeviceLists)
        {
            if (Deleted(list))
                return;

            _deviceLists.OnDeviceShutdown(list, ent);
        }

        foreach (var list in component.Configurators)
        {
            if (Deleted(list))
                return;

            _configurator.OnDeviceShutdown(list, ent);
        }

        if (TryGetNetwork(component.DeviceNetId, out var network))
            network.Remove(ent);

        CheckClearManager();
    }

    protected override void SendPacket(ref DeviceNetworkPacketEvent packet)
    {
        if (!TryEnsureNetwork(packet.NetId, out var network))
            return;

        if (packet.Address == null)
        {
            // Broadcast to all listening devices
            if (network.ListeningDevices.TryGetValue(packet.Frequency, out var devices) && CheckRecipientsList(packet, ref devices))
            {
                var deviceCopy = ArrayPool<Device>.Shared.Rent(devices.Count);
                devices.CopyTo(deviceCopy);
                SendToConnections(deviceCopy.AsSpan(0, devices.Count), packet);
                ArrayPool<Device>.Shared.Return(deviceCopy);
            }
        }
        else
        {
            var totalDevices = 0;
            var hasTargetedDevice = false;
            if (network.ReceiveAllDevices.TryGetValue(packet.Frequency, out var devices))
            {
                totalDevices += devices.Count;
            }

            if (!TryGetDevice(packet.NetId, packet.Address, out var device))
                return;

            if (!device.Value.ReceiveAll &&
                device.Value.ReceiveFrequency == packet.Frequency)
            {
                totalDevices += 1;
                hasTargetedDevice = true;
            }
            var deviceCopy = ArrayPool<Device>.Shared.Rent(totalDevices);
            if (devices != null)
            {
                devices.CopyTo(deviceCopy);
            }
            if (hasTargetedDevice)
            {
                deviceCopy[totalDevices - 1] = device.Value;
            }
            SendToConnections(deviceCopy.AsSpan(0, totalDevices), packet);
            ArrayPool<Device>.Shared.Return(deviceCopy);
        }
    }
}
