using MonitorSetup.Core.Abstractions;
using MonitorSetup.Core.Models;
using MonitorSetup.Infrastructure.Windows.Interop;
using static MonitorSetup.Infrastructure.Windows.Interop.NativeMethods;

namespace MonitorSetup.Infrastructure.Windows.Services;

/// <summary>Turns displays on and off by adding or removing their CCD paths.</summary>
internal sealed class WindowsDisplayTopology : IDisplayTopology
{
    public void Deactivate(DisplayId id)
    {
        var (paths, modes) = DisplayConfig.Query(QDC_ALL_PATHS);
        var active = paths.Where(p => p.IsActive()).ToList();
        var remaining = active.Where(p => p.TargetId() != id).ToArray();

        if (remaining.Length == active.Count)
            return;
        if (remaining.Length == 0)
            throw new DisplayException("At least one display has to stay enabled.");

        DisplayConfig.Apply(remaining, modes, "disable the display");
    }

    public void Activate(DisplayId id)
    {
        var (paths, modes) = DisplayConfig.Query(QDC_ALL_PATHS);
        var active = paths.Where(p => p.IsActive()).ToList();
        if (active.Any(p => p.TargetId() == id))
            return;

        // Use a source (graphics output) that is not already driving another display.
        var usedSources = active.Select(p => (p.sourceInfo.adapterId, p.sourceInfo.id)).ToHashSet();
        var candidates = paths
            .Where(p => p.TargetId() == id && p.targetInfo.targetAvailable != 0)
            .Where(p => !usedSources.Contains((p.sourceInfo.adapterId, p.sourceInfo.id)))
            .ToList();
        if (candidates.Count == 0)
            throw new DisplayException("No free graphics output is available to drive this display.");

        int lastError = 0;
        foreach (var candidate in candidates)
        {
            var path = candidate;
            path.flags |= DISPLAYCONFIG_PATH_ACTIVE;
            path.sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
            path.targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
            var newPaths = active.Append(path).ToArray();

            // Prefer the layout Windows remembers for this set of displays.
            lastError = SetDisplayConfig((uint)newPaths.Length, newPaths, 0, null,
                SDC_APPLY | SDC_TOPOLOGY_SUPPLIED | SDC_ALLOW_PATH_ORDER_CHANGES);
            if (lastError == ERROR_SUCCESS)
                return;

            // Otherwise keep the current displays as they are and let Windows pick a mode for the new one.
            lastError = SetDisplayConfig((uint)newPaths.Length, newPaths, (uint)modes.Length, modes,
                SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);
            if (lastError == ERROR_SUCCESS)
                return;
        }

        throw new DisplayException($"Windows refused to enable the display (error {lastError}).");
    }
}
