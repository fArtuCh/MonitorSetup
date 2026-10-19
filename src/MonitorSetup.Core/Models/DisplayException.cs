namespace MonitorSetup.Core.Models;

/// <summary>A display change that Windows refused or that cannot be done; the message is shown to the user.</summary>
public sealed class DisplayException(string message) : Exception(message);
