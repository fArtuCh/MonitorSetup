namespace MonitorSetup.Infrastructure.Windows.Interop;

/// <summary>Names for DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY values.</summary>
internal static class OutputTechnology
{
    public static string Describe(uint technology) => technology switch
    {
        0 => "VGA",
        1 => "S-Video",
        2 => "Composite",
        3 => "Component",
        4 => "DVI",
        5 => "HDMI",
        6 => "LVDS (built-in)",
        8 => "D-Jpn",
        9 => "SDI",
        10 => "DisplayPort",
        11 => "DisplayPort (built-in)",
        12 => "UDI",
        13 => "UDI (built-in)",
        14 => "SDTV dongle",
        15 => "Miracast (wireless)",
        16 => "Indirect (wired)",
        17 => "Indirect (virtual)",
        18 => "DisplayPort over USB",
        0x80000000 => "Built-in",
        _ => "Other",
    };
}
