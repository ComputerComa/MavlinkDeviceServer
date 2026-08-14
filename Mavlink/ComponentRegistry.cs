using MavlinkDeviceServer.Components;
namespace MavlinkDeviceServer.Mavlink;

public sealed class ComponentRegistry
{
    private readonly List<IMavlinkComponent> _components = [];
    public IEnumerable<IMavlinkComponent> Components => _components;
    public void Register(IMavlinkComponent component) { if (_components.Any(x => x.SystemId == component.SystemId && x.ComponentId == component.ComponentId)) throw new InvalidOperationException($"Component {component.SystemId}/{component.ComponentId} is already registered."); _components.Add(component); }
    public bool Contains(byte systemId, byte componentId) =>
        _components.Any(x => x.SystemId == systemId && x.ComponentId == componentId);

    public IEnumerable<IMavlinkComponent> GetMessageRecipients(uint messageId, byte targetSystem, byte targetComponent)
    {
        // TODO: Define an explicit broadcast COMMAND_LONG policy before broader GCS interoperability testing.
        return _components.Where(x =>
            x.HandledMessageIds.Contains(messageId) &&
            (targetSystem == 0 || targetSystem == x.SystemId) &&
            (targetComponent == 0 || targetComponent == x.ComponentId));
    }
}
