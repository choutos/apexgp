using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexGP.Core;

/// <summary>One car owned by the player.</summary>
public sealed class CarOwnership
{
    public string CarId { get; set; } = "";
    public double OdometerKm { get; set; }
    public List<string> TuningParts { get; set; } = new();
}

/// <summary>Player career state: credits, garage, licenses, completed events. Persisted as JSON.</summary>
public sealed class CareerProfile
{
    public double Credits { get; set; } = 15000;
    public List<CarOwnership> Garage { get; set; } = new();
    public HashSet<string> Licenses { get; set; } = new();
    public HashSet<string> CompletedEvents { get; set; } = new();
    public string ActiveCarId { get; set; } = "";

    [JsonIgnore]
    public static string DefaultSavePath => System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
        ".apexgp", "profile.json");

    public static CareerProfile New() => new() { ActiveCarId = "" };

    public void Save(string? path = null)
    {
        path ??= DefaultSavePath;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static CareerProfile Load(string? path = null)
    {
        path ??= DefaultSavePath;
        if (!System.IO.File.Exists(path)) return New();
        var p = System.IO.File.ReadAllText(path);
        return JsonSerializer.Deserialize<CareerProfile>(p) ?? New();
    }
}
