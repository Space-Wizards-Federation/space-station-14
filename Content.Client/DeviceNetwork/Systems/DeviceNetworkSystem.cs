using Content.Shared.Buffers;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DeviceNetwork.Systems;

namespace Content.Client.DeviceNetwork.Systems;

public sealed class DeviceNetworkSystem : SharedDeviceNetworkSystem
{
    private RobustArrayPool<Device> _arrayPool = default!;

    public override void Initialize()
    {
        base.Initialize();
        _arrayPool = new RobustArrayPool<Device>(256, 1);
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
                var deviceCopy = _arrayPool.Rent(devices.Count);
                devices.CopyTo(deviceCopy);
                SendToConnections(deviceCopy.AsSpan(0, devices.Count), packet);
                _arrayPool.ReturnClean(deviceCopy);
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
            var deviceCopy = _arrayPool.Rent(totalDevices);
            if (devices != null)
            {
                devices.CopyTo(deviceCopy);
            }
            if (hasTargetedDevice)
            {
                deviceCopy[totalDevices - 1] = device.Value;
            }
            SendToConnections(deviceCopy.AsSpan(0, totalDevices), packet);
            _arrayPool.ReturnClean(deviceCopy);
        }
    }
}
