using System.Security.Cryptography;

namespace HOMSys.Application.Services;

public static class PasswordGenerator
{
    private static readonly string[] Words =
    [
        "Apple", "River", "Tiger", "Mango", "Cloud", "Eagle", "Grape", "Piano",
        "Ocean", "Maple", "Comet", "Coral", "Delta", "Ember", "Falcon", "Garden",
        "Harbor", "Island", "Jungle", "Kettle", "Lemon", "Meadow", "Nectar", "Orbit",
        "Pepper", "Quartz", "Rocket", "Sunset", "Timber", "Umbra", "Violet", "Willow"
    ];

    public static string Generate()
    {
        var word = Words[RandomNumberGenerator.GetInt32(Words.Length)];
        var number = RandomNumberGenerator.GetInt32(0, 10000);
        return $"{word}{number:D4}";
    }

    /// <summary>Two words + 4 digits (~10 million combinations): temporary passwords sent by email (forgot password).</summary>
    public static string GenerateStrong()
    {
        var a = Words[RandomNumberGenerator.GetInt32(Words.Length)];
        var b = Words[RandomNumberGenerator.GetInt32(Words.Length)];
        return $"{a}{b}{RandomNumberGenerator.GetInt32(0, 10000):D4}";
    }
}
