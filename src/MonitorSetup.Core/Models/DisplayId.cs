namespace MonitorSetup.Core.Models;

/// <summary>
/// Identifies a physical display: the graphics adapter it hangs off and the adapter's output (target).
/// Stable while the system runs; across reboots use <see cref="DisplayInfo.DevicePath"/>.
/// </summary>
public readonly record struct DisplayId(long AdapterId, uint TargetId)
{
    public override string ToString() => $"{AdapterId:X16}:{TargetId}";
}
