// Table.cs – gemensam hjälpklass för att skriva ut tabeller i
// konsolen: en rubrikrad med kolumnnamnen, en streckavgränsning
// och därefter dataraderna.

namespace Holiday_Maker;

/// <summary>
/// Skriver ut en tabell i konsolen. Första raden i matrisen är
/// rubriken med kolumnnamnen, de följande raderna är data.
/// Kolumnbredderna beräknas från rubriken och alla rader, så att
/// kolumnerna alltid räcker upp – även om namn och orter är längre
/// än förväntat. Kolumner med true i rightAligned högerjusteras
/// (tal-kolumner), övriga vänsterjusteras (text-kolumner).
/// Används för kundlistan, sökresultatet (meny 2) och listorna i
/// avbokningsflödet (meny 3).
/// </summary>
internal static class Table
{
    public static void Print(List<string[]> rows, bool[] rightAligned)
    {
        if (rows.Count == 0)
            return;

        // Kolumnbredden blir längden på den längsta cellen i kolumnen
        // (rubrikraden medräknad), så att alla rader räcker upp.
        int columns = rows[0].Length;
        int[] widths = new int[columns];
        for (int col = 0; col < columns; col++)
            foreach (string[] row in rows)
                widths[col] = Math.Max(widths[col], row[col].Length);

        string FormatRow(string[] cells)
        {
            string line = string.Empty;
            for (int col = 0; col < columns; col++)
                line += (col > 0 ? " | " : string.Empty)
                    + (rightAligned[col] ? cells[col].PadLeft(widths[col]) : cells[col].PadRight(widths[col]));
            return line;
        }

        Console.WriteLine(FormatRow(rows[0]));
        Console.WriteLine(new string('-', widths.Sum() + 3 * (columns - 1)));
        // Rad 0 är rubriken, som redan skrivits ut – dataraderna
        // börjar alltså på rad 1.
        for (int row = 1; row < rows.Count; row++)
            Console.WriteLine(FormatRow(rows[row]));
    }
}