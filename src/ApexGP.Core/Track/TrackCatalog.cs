namespace ApexGP.Core;

/// <summary>Track definitions shipped with the game. All layouts are original.</summary>
public static class TrackCatalog
{
    public static TrackSurface SunriseLoop() => TrackSurface.Build(new TrackData("Sunrise Loop", 16.0,
        (0, 0), (0, -220),
        (35, -260), (100, -275), (170, -260), (220, -220), (240, -170),
        (235, -120), (260, -85), (305, -80),
        (330, -115), (325, -165), (290, -205), (235, -225),
        (175, -215), (130, -180), (135, -140), (110, -105),
        (60, -100), (25, -130), (-15, -120), (-25, -75), (0, -40)));

    public static TrackSurface DolphinRing() => TrackSurface.Build(new TrackData("Dolphin Ring", 16.0,
        (0, 0), (0, -120), (60, -160), (120, -120), (120, 0), (60, 40)));

    public static TrackSurface ByName(string name) => name switch
    {
        "Dolphin Ring" => DolphinRing(),
        _ => SunriseLoop(),
    };
}
