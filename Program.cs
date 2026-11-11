// Holiday Maker – konsolprogram för en resebyrås bokningssystem.
// Denna fil är programets startpunkt: den visar huvudmenyn och dirigerar
// varje menyval till rätt klass. Övrig kod är uppdelad i filer efter
// funktion och område:
//   UI (konsolgränssnitt):
//     * CustomerMenu.cs – meny 1 (registrera kund) + kundval
//     * SearchMenu.cs   – meny 2 (välj kund och sök lediga rum, med direktbokning)
//     * BookingMenu.cs  – meny 3 (avboka) + meny 4 (ändra antal personer) + delade bokningssteg
//     * ConsoleInput.cs – hjälpmetoder för inläsning av användarinput
//     * Table.cs        – skriver ut tabeller med kolumnrubriker i konsolen
//   Databasskikt:
//     * HolidayMakerDb.cs – anslutning och gemensamma hjälpmetoder
//     * Models.cs         – record-typer som bär på data ur databasen
//     * CustomerDb.cs     – kunder
//     * SearchDb.cs       – orter och rumssökning
//     * BookingDb.cs      – bokningar

namespace Holiday_Maker;

/// <summary>
/// Programets startpunkt och huvudmeny. Klassen innehåller även appens
/// gemensamma tillstånd (vald kund samt sökta datum och antal personer)
/// som delas mellan menyklasserna.
/// </summary>
internal static class Program
{
    // Id:t på den kund som användaren just nu har valt (meny 1, eller
    // kundvalet som första steg i meny 2).
    // Null om ingen kund är vald. "long?" (nullable) gör det möjligt
    // att skilja på "ingen vald" (null) och ett faktiskt kund-id.
    // Internal så att CustomerMenu och BookingMenu kan läsa och skriva den.
    internal static long? SelectedCustomerId;

    // Antalet personer från senaste sökningen (meny 2). Talet frågas
    // redan vid sökningen och sökningen returnerar bara rum som rymmer
    // minst så många, så samma tal används som antal personer i
    // bokningen när ett rum bokas direkt från sökresultatet –
    // användaren behöver alltså aldrig ange antalet personer två gånger.
    internal static int LastSearchOccupants;

    // Check-in- och check-out-datum från senaste sökningen (meny 2).
    // Datumen frågas redan vid sökningen – sökningen visar ju bara rum
    // som är lediga under just den perioden – så samma datum används
    // direkt i bokningen när ett rum väljs ur sökresultatet. Användaren
    // behöver alltså aldrig ange datumen på nytt.
    internal static DateOnly? LastSearchStartDate;
    internal static DateOnly? LastSearchEndDate;

    /// <summary>
    /// Programets inträdespunkt. Visar huvudmenyn i en oändlig loop,
    /// läser användarens val och dirigerar det till rätt metod.
    /// Loopen bryts först när användaren väljer 0 (Avsluta) – oväntade
    /// fel fångas och programmet fortsätter i huvudmenyn.
    /// </summary>
    private static void Main()
    {
        // Låt konsolen skriva svenska tecken (å, ä, ö) i stället för "oskärpa".
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=== Holiday Maker – Bokningssystem för resebyrå ===");

        // Testa anslutningen innan menyn visas. Om databasen inte kan
        // nås får användaren prova igen; endast val 0 avslutar programmet.
        while (!HolidayMakerDb.CheckConnection())
        {
            Console.WriteLine("Kunde inte ansluta till databasen.");
            Console.WriteLine("Kontrollera Server, User Id och Password i HolidayMakerDb.cs.");
            Console.WriteLine();
            Console.Write("Tryck Enter för att prova igen, eller skriv 0 för att avsluta: ");
            string? retry = Console.ReadLine();
            if (retry is null || retry.Trim() is "0")
            {
                Console.WriteLine("Avslutar programmet.");
                return;
            }
        }

        // Huvudloopen: visa menyn, läsa ett val, utföra det, upprepa.
        while (true)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine("Vad vill du göra?");
                Console.WriteLine("  1. Registrera kund");
                Console.WriteLine("  2. Välj kund och sök lediga rum");
                Console.WriteLine("  3. Avboka");
                Console.WriteLine("  4. Ändra antal personer på en bokning");
                Console.WriteLine("  0. Avsluta");
                Console.WriteLine("  (För att återgå till menyn: skriv \"b\" eller tryck Enter för att backa)");

                // allowBack: false – i huvudmenyn finns det ingenstans att
                // backa till, så "b" eller Enter får istället fråga om.
                int menuChoice = ConsoleInput.ReadInt("Välj: ", 0, 4, allowBack: false);

                // Varje menyval kallar en metod i rätt menyklass; val 0 avslutar.
                switch (menuChoice)
                {
                    case 1: CustomerMenu.RegisterCustomer(); break;
                    case 2: SearchMenu.ChooseCustomerAndSearchRooms(); break;
                    case 3: BookingMenu.CancelBookingFlow(); break;
                    case 4: BookingMenu.ChangeOccupantsFlow(); break;
                    case 0:
                        Console.WriteLine("Avslutar programmet.");
                        return;
                }
            }
            catch (BackToMenuException)
            {
                // Användaren backade (skrev "b" eller tryckte Enter i en
                // fråga) – tillbaka till huvudmenyn.
                Console.WriteLine("Avbröts – tillbaka till huvudmenyn.");
            }
            catch (EndOfInputException)
            {
                // Inmatningskanalen stängdes (t.ex. Ctrl+Z) – det finns
                // inget mer programmet kan göra, så avsluta ordentligt.
                Console.WriteLine();
                Console.WriteLine("Inmatningen stängdes – programmet avslutas.");
                return;
            }
            catch (Exception ex)
            {
                // Oväntat fel (t.ex. att databasanslutningen tappades).
                // Visa felet och fortsätt i huvudmenyn istället för att
                // låta programmet krascha.
                Console.WriteLine($"Okänt fel: {ex.Message}");
                Console.WriteLine("Tillbaka till huvudmenyn.");
            }
        }
    }
}
