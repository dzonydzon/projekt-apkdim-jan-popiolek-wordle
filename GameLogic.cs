namespace WordlePL;

using System.Collections.Generic;

/// <summary>
/// Status oceny pojedynczej litery.
/// </summary>
public enum LetterStatus
{
    Unknown,  // Nie sprawdzona jeszcze
    Absent,   // Nie ma w haśle (ciemnoszary)
    Present,  // Jest w haśle, ale na innej pozycji (żółty)
    Correct   // Jest na właściwej pozycji (zielony)
}

/// <summary>
/// Logika gry Wordle z dwuetapowym algorytmem oceny liter.
/// </summary>
public static class GameLogic
{
    public const int WordLength = 5;
    public const int MaxAttempts = 6;

    /// <summary>
    /// Ocenia próbę gracza względem szukanego hasła.
    /// Algorytm dwuetapowy poprawnie obsługuje powtarzające się litery:
    ///   1) Najpierw zaznacza dokładne dopasowania (Correct/zielone).
    ///   2) Następnie zaznacza litery w złym miejscu (Present/żółte), odliczając od dostępnej puli.
    /// </summary>
    public static LetterStatus[] Evaluate(string guess, string secret)
    {
        var result = new LetterStatus[WordLength];
        var remaining = new Dictionary<char, int>();

        // Zlicz litery hasła
        foreach (char c in secret)
        {
            if (!remaining.ContainsKey(c))
                remaining[c] = 0;
            remaining[c]++;
        }

        // Krok 1: dokładne dopasowania (zielone)
        for (int i = 0; i < WordLength; i++)
        {
            if (guess[i] == secret[i])
            {
                result[i] = LetterStatus.Correct;
                remaining[guess[i]]--;
            }
        }

        // Krok 2: litery na złym miejscu (żółte) lub nieobecne (szare)
        for (int i = 0; i < WordLength; i++)
        {
            if (result[i] == LetterStatus.Correct)
                continue;

            char c = guess[i];
            if (remaining.ContainsKey(c) && remaining[c] > 0)
            {
                result[i] = LetterStatus.Present;
                remaining[c]--;
            }
            else
            {
                result[i] = LetterStatus.Absent;
            }
        }

        return result;
    }

    /// <summary>
    /// Aktualizuje stan klawiatury – każdy klawisz zapamiętuje swój najlepszy status
    /// (Correct > Present > Absent).
    /// </summary>
    public static void UpdateKeyboardState(Dictionary<char, LetterStatus> keyboard,
                                           string guess, LetterStatus[] evaluation)
    {
        for (int i = 0; i < WordLength; i++)
        {
            char c = guess[i];
            var newStatus = evaluation[i];

            if (!keyboard.ContainsKey(c) || newStatus > keyboard[c])
            {
                keyboard[c] = newStatus;
            }
        }
    }
}
