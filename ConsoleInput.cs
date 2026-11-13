// ConsoleInput.cs – inläsningshjälpmedel för hela applikationen.
// Alla metoder frågar om upprepade gånger tills ett giltigt värde har
// skrivits in, så att felaktig inmatning aldrig kraschar programmet.
//
// "Backa": i varje fråga kan användaren backa till huvudmenyn genom
// att skriva "b", eller (vid frågor som kräver ett svar) bara trycka
// Enter – undantaget kundens textfält vid registrering, där ett tomt
// svar ger istället ett felmeddelande och man kan skriva om raden. I
// de valfria filterna i sökningen (meny 2) betyder Enter istället
// "hoppa över" (nej, eller ingen gräns).
// Backa gör att BackToMenuException kastas, som fångas i Program.Main.
//
// Ctrl+Z: i en interaktiv Windowskonsol markerar Ctrl+Z (EOF-tecknet)
// radens slut när Enter trycks, och Console.ReadLine returnerar null.
// Programmet avslutas inte: ReadLineSafe visar ett felmeddelande och
// behandlar raden som tom inmatning, så frågan ställs på nytt – eller
// så får tomma svaret sin vanliga verkan (backa till huvudmenyn, eller
// "hoppa över" i de valfria filterna). Endast om inmatningen är
// omdirigerad (t.ex. pipad in) och tar slut, eller om EOF kommer
// upprepade gånger utan att användaren hunnit skriva något, kastas
// EndOfInputException och Program.Main avslutar programmet ordentligt.

using System.Text.RegularExpressions;

namespace Holiday_Maker;

/// <summary>
/// Kastas av ConsoleInput:s metoder när användaren väljer att backa
/// till huvudmenyn (skriver "b" eller trycker Enter i en fråga som
/// kräver svar). Fångas i Program.Main, som då visar en kort
/// bekräftelse och återgår till huvudmenyn.
/// </summary>
internal sealed class BackToMenuException : Exception
{
}

/// <summary>
/// Kastas när inmatningskanalen är slut för gott: inmatningen är
/// omdirigerad (t.ex. pipad in) och har tagit slut, eller så kommer
/// EOF (Ctrl+Z + Enter) så upprepade gånger i snabb följd att det inte
/// kan ha skrivits av användaren. Program.Main fångar den och avslutar
/// programmet ordentligt istället för att sitta i en oändlig frågeloop.
/// </summary>
internal sealed class EndOfInputException : Exception
{
}

/// <summary>
/// Statiska hjälpmetoder som läser användarinput från konsolen och
/// frågar om tills ett giltigt värde har skrivits. Alla menyklasser
/// använder dessa istället för att läsa konsolen direkt, så att
/// inläsningslogiken är samlad på en plats.
/// </summary>
internal static class ConsoleInput
{
    // Säkerhetsnät mot oändlig loop vid stängd inmatningskanal: om
    // ReadLine returnerar null två gånger i rad inom EofWindowMs kan det
    // inte vara användaren som tryckt Ctrl+Z (ingen hinner trycka det
    // två gånger på under en halva sekund) – då är kanalen stängd.
    private static long _lastEofAt = long.MinValue;
    private static int _eofStreak;
    private const int EofWindowMs = 500;

    /// <summary>
    /// Läser en rad från konsolen. Om ReadLine returnerar null (EOF)
    /// skiljer vi på tre fall:
    ///  1. Interaktiv konsol – användaren tryckte Ctrl+Z + Enter.
    ///     Programmet avslutas INTE: ett felmeddelande visas och ""
    ///     returneras, så den omkallande metoden behandlar raden som
    ///     en vanlig tom inmatning (frågan ställs på nytt, eller så
    ///     backar/hoppar den över precis som vid ett tomt svar).
    ///  2. Omdirigerad inmatning (t.ex. pipad in) som tar slut –
    ///     EndOfInputException, ingen användare finns kvar att fråga.
    ///  3. EOF upprepas i snabb följd – kanalen är stängd för gott,
    ///     EndOfInputException (säkerhetsnätet _eofStreak).
    /// </summary>
    internal static string? ReadLineSafe()
    {
        string? line = Console.ReadLine();
        if (line is not null)
        {
            _eofStreak = 0; // en vanlig rad – räkna om EOF-sekvensen
            return line;
        }

        // EOF: ReadLine returnerade null.
        if (Console.IsInputRedirected)
        {
            // Pipad/omdirigerad inmatning tar slut för gott.
            throw new EndOfInputException();
        }

        // Interaktiv konsol: användaren tryckte Ctrl+Z + Enter.
        long now = Environment.TickCount64;
        _eofStreak = now - _lastEofAt < EofWindowMs ? _eofStreak + 1 : 1;
        _lastEofAt = now;
        if (_eofStreak >= 2)
        {
            // Två EOF:er i snabb följd – inmatningskanalen är stängd
            // trots att den inte är omdirigerad; avsluta ordentligt.
            throw new EndOfInputException();
        }

        // Behandla raden som ogiltig inmatning: meddelande + tomt svar,
        // så att den omkallande metoden gör som vid ett tomt svar.
        Console.WriteLine("Ogiltig inmatning (Ctrl+Z) – raden ignoreras.");
        return string.Empty;
    }

    /// <summary>"b" (stort eller litet) betyder alltid "backa".</summary>
    private static bool IsBack(string? value) => value is "b" or "B";

    // Valideringsregler för kundens fält vid registrering (meny 1).
    // Regex-uttrycken kompileras en gång som statiska fält.

    // Namn: bokstäver (inklusive å/ä/ö), mellanslag, bindestreck och
    // apostrofer, och måste börja med en bokstav. T.ex. "Anna Lind"
    // eller "Ann-Marie".
    private static readonly Regex NameRegex =
        new(@"^[A-ZÅÄÖa-zåäö][A-ZÅÄÖa-zåäö' -]*$", RegexOptions.Compiled);

    // Adress: bokstäver, siffror och vanliga adresstecken (gatunamn
    // med nr, box, postnummer, boende nr); måste börja med en bokstav
    // eller siffra. T.ex. "Storgatan 22" eller "Box 123, 211 01 Malmö".
    private static readonly Regex AddressRegex =
        new(@"^[A-ZÅÄÖa-zåäö0-9][A-ZÅÄÖa-zåäö0-9' -,.()/#]*$", RegexOptions.Compiled);

    // E-post: minst ett tecken före @, minst ett tecken efter @ med en
    // punkt i domänen, och inga mellanslag. T.ex. "anna@example.com".
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    // Telefon: börjar med 0 eller +, sedan siffror och tillåtna tecken
    // (mellanslag, bindestreck, parenteser). Antalet siffror (7–15)
    // kontrolleras separat i ReadPhone. T.ex. "047-123456" eller
    // "+46 70 12 34 56".
    private static readonly Regex PhoneRegex =
        new(@"^[+0][0-9 ()-]{4,19}$", RegexOptions.Compiled);

    /// <summary>
    /// Gemensam inläsningsloop för kundens textfält: läser en rad,
    /// trimmar bort mellanslag i ändarna och validerar innehållet med
    /// den angivna funktionen. "b" = backa till huvudmenyn. Ett tomt
    /// svar eller ogiltiga tecken ger ett felmeddelande och frågan
    /// ställs igen, så att användaren kan skriva om just den raden.
    /// validate returnerar null om värdet är giltigt, annars felmeddelandet.
    /// </summary>
    private static string ReadField(
        string prompt, string emptyError, Func<string, string?> validate)
    {
        while (true)
        {
            Console.Write(prompt);
            string? value = ReadLineSafe()?.Trim();
            // "b" backar till huvudmenyn (kastas som BackToMenuException).
            if (IsBack(value))
                throw new BackToMenuException();
            // Kundens fält får aldrig vara tomma – visa felet och fråga igen.
            if (string.IsNullOrEmpty(value))
            {
                Console.WriteLine(emptyError);
                continue;
            }
            // Fältets egen validering (regex); null betyder att värdet är giltigt.
            string? error = validate(value);
            if (error is null)
                return value;
            Console.WriteLine(error);
        }
    }

    /// <summary>
    /// Läser in ett namn: bara bokstäver (inklusive å/ä/ö), mellanslag,
    /// bindestreck och apostrofer, och måste börja med en bokstav.
    /// Tomt svar eller ogiltiga tecken = felmeddelande och skriva om.
    /// "b" = backa till huvudmenyn.
    /// </summary>
    public static string ReadName(string prompt)
    {
        return ReadField(prompt,
            "Namnet får inte vara tomt – skriv namnet igen.",
            value => NameRegex.IsMatch(value) ? null
                : "Namnet får bara innehålla bokstäver, mellanslag, bindestreck och apostrofer, och det måste börja med en bokstav.");
    }

    /// <summary>
    /// Läser in en adress: bokstäver, siffror och tecknen
    /// ' - . , / ( ) #, och måste börja med en bokstav eller siffra.
    /// Tomt svar eller ogiltiga tecken = felmeddelande och skriva om.
    /// "b" = backa till huvudmenyn.
    /// </summary>
    public static string ReadAddress(string prompt)
    {
        return ReadField(prompt,
            "Adressen får inte vara tomt – skriv adressen igen.",
            value => AddressRegex.IsMatch(value) ? null
                : "Adressen får bara innehålla bokstäver, siffror och tecknen ' - . , / ( ) #, och den måste börja med en bokstav eller siffra.");
    }

    /// <summary>
    /// Läser in en e-postadress: formen namn@domän.se, alltså minst
    /// ett tecken före och efter @, en punkt i domänen och inga
    /// mellanslag. Tomt svar eller ogiltig form = felmeddelande och
    /// skriva om. "b" = backa till huvudmenyn.
    /// </summary>
    public static string ReadEmail(string prompt)
    {
        return ReadField(prompt,
            "E-posten får inte vara tomt – skriv e-postadressen igen.",
            value => EmailRegex.IsMatch(value) ? null
                : "Ogiltig e-postadress – den måste ha formen namn@domän.se, utan mellanslag.");
    }

    /// <summary>
    /// Läser in ett telefonnummer: måste börja med 0 eller +, få
    /// bara innehålla siffror, mellanslag, bindestreck och
    /// parenteser, och ha 7–15 siffror. Tomt svar eller ogiltigt
    /// nummer = felmeddelande och skriva om. "b" = backa till
    /// huvudmenyn.
    /// </summary>
    public static string ReadPhone(string prompt)
    {
        return ReadField(prompt,
            "Telefonnumret får inte vara tomt – skriv numret igen.",
            value =>
            {
                if (!PhoneRegex.IsMatch(value))
                    return "Telefonnumret måste börja med 0 eller + och får bara innehålla siffror, mellanslag, bindestreck och parenteser.";
                // Count räknar siffrorna i strängen – tecken som
                // mellanslag och bindestreck räknas alltså inte med.
                int digits = value.Count(char.IsDigit);
                return digits >= 7 && digits <= 15 ? null
                    : "Telefonnumret måste innehålla 7–15 siffror.";
            });
    }

    /// <summary>
    /// Läser in ett heltal inom intervallet [min, max] och frågar om
    /// på nytt om talet är felaktigt. Enter eller "b" = backa till
    /// huvudmenyn (om allowBack är sant, vilket är standard).
    /// </summary>
    public static int ReadInt(string prompt, int min, int max, bool allowBack = true)
    {
        while (true)
        {
            Console.Write(prompt);
            // TryParse misslyckas tyst (ingen exception) om inmatningen
            // inte är ett giltigt tal – då meddelas felet och vi frågar igen.
            string? input = ReadLineSafe()?.Trim();
            if (allowBack && (string.IsNullOrEmpty(input) || IsBack(input)))
                throw new BackToMenuException();
            if (int.TryParse(input, out int value) && value >= min && value <= max)
                return value;
            Console.WriteLine($"Ange ett heltal mellan {min} och {max}.");
        }
    }

    /// <summary>
    /// Läser in ett större heltal (long) inom intervallet [min, max].
    /// Enter eller "b" = backa (om allowBack är sant).
    /// </summary>
    public static long ReadLong(string prompt, long min, long max, bool allowBack = true)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = ReadLineSafe()?.Trim();
            if (allowBack && (string.IsNullOrEmpty(input) || IsBack(input)))
                throw new BackToMenuException();
            if (long.TryParse(input, out long value) && value >= min && value <= max)
                return value;
            Console.WriteLine($"Ange ett heltal mellan {min} och {max}.");
        }
    }

    /// <summary>
    /// Läser in ett datum i formatet åååå-mm-dd och frågar om vid fel.
    /// Enter eller "b" = backa (om allowBack är sant).
    /// </summary>
    public static DateOnly ReadDate(string label, bool allowBack = true)
    {
        while (true)
        {
            Console.Write($"{label} (åååå-mm-dd): ");
            string? input = ReadLineSafe()?.Trim();
            if (allowBack && (string.IsNullOrEmpty(input) || IsBack(input)))
                throw new BackToMenuException();
            if (DateOnly.TryParse(input, out DateOnly value))
                return value;
            Console.WriteLine("Ange datumet i formatet åååå-mm-dd, t.ex. 2026-12-05.");
        }
    }

    /// <summary>
    /// Läser in ett ja/nej-svar. Accepterar även genvägarna
    /// j/n, yes/no och x (för nej). Enter eller "b" = backa
    /// (om allowBack är sant, vilket är standard).
    /// </summary>
    public static bool ReadYesNo(string prompt, bool allowBack = true)
    {
        while (true)
        {
            Console.Write($"{prompt} (ja/nej): ");
            // ToLowerInvariant gör att "JA", "Ja" och "ja" alla accepteras.
            string? answer = ReadLineSafe()?.Trim().ToLowerInvariant();
            if (allowBack && (string.IsNullOrEmpty(answer) || answer is "b"))
                throw new BackToMenuException();
            if (answer is "ja" or "j" or "yes" or "y")
                return true;
            if (answer is "nej" or "n" or "no" or "x")
                return false;
            Console.WriteLine("Svara ja eller nej.");
        }
    }

    // ------------------------------------------------------------------
    // Valfria frågor (sökfilter): tomt svar (Enter) hoppas över
    // ------------------------------------------------------------------
    /// <summary>
    /// Som ReadYesNo, men ett tomt svar (Enter) betyder "hoppa över"
    /// och ger false – användaren behöver inte svara om hon inte vill
    /// ha filtret. "b" backar till huvudmenyn som i ReadYesNo.
    /// </summary>
    public static bool ReadYesNoOptional(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt} (ja/nej, Enter = hoppa över): ");
            string? answer = ReadLineSafe()?.Trim().ToLowerInvariant();
            if (IsBack(answer))
                throw new BackToMenuException();
            if (string.IsNullOrEmpty(answer))
                return false; // tomt svar = hoppa över filtret
            if (answer is "ja" or "j" or "yes" or "y")
                return true;
            if (answer is "nej" or "n" or "no" or "x")
                return false;
            Console.WriteLine("Svara ja eller nej (eller tryck Enter för att hoppa över).");
        }
    }

    /// <summary>
    /// Som ReadInt, men ett tomt svar (Enter) betyder "hoppa över" och
    /// returnerar null – används för valfria siffervärden som
    /// maximalt avstånd. "b" backar till huvudmenyn som i ReadInt.
    /// </summary>
    public static int? ReadIntOptional(string prompt, int min, int max)
    {
        while (true)
        {
            Console.Write($"{prompt} (Enter = hoppa över): ");
            string? input = ReadLineSafe()?.Trim();
            if (IsBack(input))
                throw new BackToMenuException();
            if (string.IsNullOrEmpty(input))
                return null; // tomt svar = hoppa över filtret
            if (int.TryParse(input, out int value) && value >= min && value <= max)
                return value;
            Console.WriteLine($"Ange ett heltal mellan {min} och {max} (eller Enter för att hoppa över).");
        }
    }

    /// <summary>
    /// Läser in ett datum som måste vara efter "after" (t.ex. check-out
    /// efter check-in). Frågar om om datumen är felaktiga eller inte
    /// ligger efter, och "b" backar till huvudmenyn.
    /// </summary>
    public static DateOnly ReadDateAfter(string label, DateOnly after)
    {
        while (true)
        {
            DateOnly value = ReadDate(label);
            if (value > after)
                return value;
            Console.WriteLine($"{label} måste vara efter {after:yyyy-MM-dd}.");
        }
    }
}
