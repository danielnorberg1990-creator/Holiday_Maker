# Holiday Maker

Konsolapplikation (C#/.NET) som hjälper en resebyrå att hantera kunder,
rumssökningar och bokningar i MySQL-databasen **Holiday_Maker**.

## Databas

Schemat följer uppgiftens krav:

- `Hotels` – boende med ort, avstånd till strand/centrum och omdöme
- `Amenities` – faciliteter (Pool, Restaurang, Fitness, Parkering)
- `Hotel_x_Amenities` – vilka faciliteter varje boende har
- `Rooms` – rum med kapacitet (1/2/4/6) och pris per natt
- `Customers` – kunder med unik e-post
- `Bookings` – bokningar med start-/slutdatum, totalpris och antal personer

### SQL-skript (mappen `database/`)

| Skript        | Syfte                                                             |
|---------------|-------------------------------------------------------------------|
| `schema.sql`  | Skapar tabellerna (idempotent: `DROP IF EXISTS` först).           |
| `testdata.sql`| 5 boenden på 3 orter, 20 rum, 4 faciliteter, 5 kunder, 25 bokningar i december 2026. Idempotent (truncar och lägger in på nytt). |
| `queries.sql` | Exempel-SELECT:ar (sökning, dubbelbokningskontroll, rapporter).   |

Kör skripten i ordningen: `schema.sql` → `testdata.sql` (t.ex. i VS Code
Database Client eller `mysql -u <user> -p < schema.sql`).

## Applikation

### Funktioner

1. **Registrera kund** – ny kund med namn, adress, e-post och telefon
   (avvisas om e-posten redan finns).
2. **Välj kund** – listar alla kunder och låter välja en.
3. **Sök lediga rum** – ort, check-in/check-out, antal personer samt
   valfria filter (pool, restaurang, maximalt avstånd till strand/centrum).
   Sortering på pris per natt eller omdöme.
4. **Skapa bokning** – validerar kapacitet och dubbelbokning i transaktion,
   beräknar totalpris (nätter × pris/natt) och visar en bekräftelse.
5. **Visa kundens bokningar** – alla bokningar med boende, rum, datum och pris.
6. **Ändra antal personer** – i en befintlig bokning (kontrollerar kapacitet).
7. **Avboka** – tar bort bokningen så rummet kan bokas igen.

### Kör

```bash
dotnet run
```

### Anslutning

Databasanslutningen ligger i `HolidayMakerDb.cs` (konstant `ConnectionString`).
Uppdatera `Server`, `User Id` och `Password` efter behov.

## Designval

- **Tillgänglighet:** ett rum är ledigt om inget datumintervall överlappar:
  `b.start_date < @endDate AND b.end_date > @startDate`
  (utcheckning och incheckning samma dag är tillåtet).
- **Konkurrens:** bokning görs i en transaktion med `SELECT … FOR UPDATE`
  på rum och befintliga bokningar för att undvika dubbelbokning.
- **Kapacitet:** antalet personer får aldrig överstiga rummets `capacity`.
- **Totalpris:** sparas vid bokningstidpunkt som `nätter × pris/natt`.
- **E-post är unik:** `UNIQUE` på `Customers.email`, fel 1062 fångas i UI:et.

## Testdata – goda fall att prova

- Sök i Ystad 12/1–12/10 för 2 personer → flera rum, bl.a. Strandhotell Ystad.
- Sök i Åre 12/1–12/4 → Fjällpensionatet 302 (bokad) utesluts.
- Sök 6 personer i Ystad → inga rum (det största är 4-personersrum).
- Böka rum 103 (bokad 5–20/12) 12/8–12/12 → nekas (dubbelbokning).
- Böka samma rum 12/20–12/22 → godkänns (utcheckning samma dag).
- Avboka bokning 5 och sök 4 personer i Ystad 12/7–12/10 → 103 dyker upp.
