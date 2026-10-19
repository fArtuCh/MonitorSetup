using MonitorSetup.Core.Models;

namespace MonitorSetup.Core.Abstractions;

/// <summary>How a display was set up when it was turned off.</summary>
public sealed record SavedDisplayState(int X, int Y, int Width, int Height, int RefreshRate, DisplayOrientation Orientation);

/// <summary>Persists <see cref="SavedDisplayState"/> between runs, keyed by a stable display key.</summary>
public interface IDisplayStateStore
{
    void Save(string displayKey, SavedDisplayState state);
    SavedDisplayState? Get(string displayKey);
}
