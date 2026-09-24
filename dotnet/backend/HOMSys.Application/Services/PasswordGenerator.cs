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
}
