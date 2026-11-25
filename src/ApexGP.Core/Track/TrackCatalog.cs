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

    /// <summary>Tight, technical hillclimb-style circuit with a hairpin, esses and chicane.</summary>
    public static TrackSurface KanyuPass() => TrackSurface.Build(new TrackData("Kanyu Pass", 13.0,
        (0, -70), (0, -130),
        (48, -170), (105, -185), (155, -160), (175, -115),
        (210, -100), (238, -130), (243, -175), (215, -212),
        (172, -226), (130, -220), (105, -190),
        (75, -218), (42, -246), (-5, -256), (-52, -246),
        (-95, -222), (-125, -188), (-132, -148),
        (-118, -108), (-95, -78), (-70, -60), (-58, -28),
        (-58, 12), (-40, 52), (-10, 82), (25, 90),
        (48, 78), (44, 48), (34, 24), (26, 14)));

    public static TrackSurface ByName(string name) => name switch
    {
        "Dolphin Ring" => DolphinRing(),
        "Kanyu Pass" => KanyuPass(),
        _ => SunriseLoop(),
    };
}
