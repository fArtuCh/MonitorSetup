namespace MonitorSetup.Core.Abstractions;

/// <summary>Opaque copy of the whole display configuration.</summary>
public interface IDisplaySnapshot;

/// <summary>Captures and restores the display configuration, used to revert a change the user did not keep.</summary>
public interface IDisplayConfigurationSnapshots
{
    IDisplaySnapshot Capture();
    void Restore(IDisplaySnapshot snapshot);
}
