namespace WordlePL;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

/// <summary>
/// Baza polskich 5-literowych słów (SJP) oraz pula haseł do losowania.
/// </summary>
public static class WordBank
{
    // Baza wszystkich dozwolonych polskich 5-literowych słów (ponad 28 700 słów z SJP)
    private static readonly HashSet<string> AllowedWords = new(StringComparer.OrdinalIgnoreCase);

    // Wyselekcjonowane, popularne hasła losowane jako zagadki
    private static readonly string[] AllWords =
    {
        "MASŁO", "TĘCZA", "SERCE", "RZEKA", "OBRAZ",
        "KWIAT", "TRAWA", "CHLEB", "ZEGAR", "DOMEK",
        "PTAKI", "PLANY", "KOCIA", "ROBOT", "WODNY",
        "BIURO", "POKÓJ", "ŻYCIE", "ŻABKA", "ŚNIEG",
        "ŚLADY", "WIATR", "BRZEG", "BRAMA", "CUKRY",
        "KARTY", "PILOT", "RADIO", "PLAŻA", "SZKŁO",
        "DROGA", "OGIEŃ", "ZAMEK", "ZŁOTO", "SMOKI",
        "SKLEP", "KROWA", "WILKI", "DZIEŃ", "NOCNY",
        "LEŚNY", "GÓRAL", "MORZE", "BANAN", "ARBUZ",
        "DESKA", "SOSNA", "JAJKO", "PALEC", "GŁOWA",
        "STOPA", "WŁOSY", "BRODA", "PŁYTA", "TORBA",
        "SŁOMA", "ŁÓDKA", "JACHT", "BURZA", "SZAFA",
        "ŁÓŻKO", "FOTEL", "LAMPA", "DRZWI", "STOŁY",
        "FARBA", "WĘŻYK", "KOSZE", "OWOCE", "DYNIA",
        "CZASU", "PIŁKA", "ŚWIAT", "WĘZEŁ", "ROWER",
        "PERŁA", "PIÓRO", "BOCIE", "SZLAK", "WIDOK",
        "MIASTO", "PRACA", "MOTYL", "KORAL"
    };

    private static readonly List<string> _available;
    private static readonly Random _rng = new();

    static WordBank()
    {
        // 1. Dodaj hasła podstawowe
        foreach (var w in AllWords)
        {
            if (w.Length == 5)
                AllowedWords.Add(w.Trim().ToUpperInvariant());
        }

        // 2. Załaduj pełną bazę słów 5-literowych z zasobu wbudowanego words_5.txt
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("words_5.txt", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 5)
                            AllowedWords.Add(line.ToUpperInvariant());
                    }
                }
            }

            // Fallback: sprawdź czy plik leży w katalogu roboczym / wyjściowym
            if (AllowedWords.Count <= AllWords.Length && File.Exists("words_5.txt"))
            {
                foreach (var line in File.ReadLines("words_5.txt", Encoding.UTF8))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 5)
                        AllowedWords.Add(trimmed.ToUpperInvariant());
                }
            }
        }
        catch
        {
            // W razie problemów z odczytem pula podstawowa pozostaje aktywna
        }

        // Zainicjuj pulę do losowania tylko słowami o poprawnej długości 5
        _available = new List<string>(AllWords.Where(w => w.Length == 5));
    }

    public static int TotalSecretWords => AllWords.Length;
    public static int AllowedWordsCount => AllowedWords.Count;

    /// <summary>
    /// Sprawdza, czy podane 5-literowe słowo istnieje w polskim słowniku.
    /// </summary>
    public static bool IsValidWord(string? word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length != 5)
            return false;

        return AllowedWords.Contains(word.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Losuje słowo do odgadnięcia z puli. Po wyczerpaniu puli resetuje ją.
    /// </summary>
    public static string GetRandomWord()
    {
        if (_available.Count == 0)
            _available.AddRange(AllWords.Where(w => w.Length == 5));

        int index = _rng.Next(_available.Count);
        string word = _available[index];
        _available.RemoveAt(index);
        return word;
    }
}
