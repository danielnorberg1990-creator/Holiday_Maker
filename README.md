# Holiday Maker

Konsolapplikation (C#/.NET) som hjälper en resebyrå att hantera kunder,
rumssökningar och bokningar i MySQL-databasen **Holiday_Maker**.

## Databas

Schemat följer uppgiftens krav:

- `Hotels` – boende med ort, avstånd till strand/centrum och omdöme
- `Amenities` – faciliteter (Pool, Restaurang, Fitness, Parkering)
- `Hotel_x_Amenities` – vilka faciliteter varje boende har
- `Rooms` – rum med kapacitet (1/2/4/6), pris per natt och status (1 = bokat, 0/NULL = tillgängligt)
- `Customers` – kunder med unik e-post
- `Bookings` – bokningar med start-/slutdatum, totalpris och antal personer

### SQL-skript (mappen `database/`)

| Skript        | Syfte                                                             |
|---------------|-------------------------------------------------------------------|
| `schema.sql`  | Skapar databasen, tabellerna och användaren `testkonto` med fulla rättigheter på Holiday_Maker. Idempotent: `DROP IF EXISTS` först. |
| `testdata.sql`| 5 boenden på 3 orter, 20 rum, 4 faciliteter (10 kopplingar i `Hotel_x_Amenities`), 5 kunder, 25 bokningar i december 2026. Idempotent (tömmer tabellerna och lägger in på nytt). |
| `queries.sql` | Exempel-SELECT:ar (sökning, dubbelbokningskontroll, rapporter).   |

Kör skripten i ordningen: `schema.sql` → `testdata.sql` (t.ex. i VS Code
Database Client eller `mysql -u <user> -p < schema.sql`). `schema.sql`
kräver en administratör (t.ex. root) eftersom den även skapar användaren
`testkonto` och ger den rättigheter.

## Applikation

### Funktioner

1. **Registrera kund** – ny kund med namn, adress, e-post och telefon.
   Varje fält valideras: tomt svar eller ogiltiga tecken ger ett
   felmeddelande och man kan skriva om raden (kunden avvisas också om
   e-posten redan finns).
2. **Välj kund och sök lediga rum** – först väljer man en kund
   (alla kunder listas som en komplett tabell: nr, id, namn, e-post),
   sedan sökningen: ort, antal personer, check-in och check-out,
   valfria filter (pool, restaurang, maximalt avstånd till strand
   respektive centrum – Enter hoppas över) och sortering (pris per
   natt eller omdöme). Endast rum som är lediga under hela perioden
   visas, och pool, restaurang samt avstånd till strand/centrum visas
   som information i resultatlistan. Rum som är flaggat som bokat
   (status = 1 i Rooms) visas inte i sökresultatet. I slutet kan man
   boka direkt: check-in/check-out och antalet personer tas från
   sökningen (de frågas alltså aldrig på nytt), och rumstatus,
   kapacitet och dubbelbokning valideras i transaktion. Totalpriset
   (nätter × pris/natt) beräknas och en bekräftelse visas.
3. **Avboka** – välj först väg: (1) välj kund och se dennes namn och bokningar som en komplett tabell (boende, ort, rum, check-in/check-out, antal personer, kapacitet, totalpris) eller (2) välj boende, välj rum och se vem som har bokat det. Endast bokningar som inte är slut (check-outdatum efter idag) visas; den valda bekräftas med "ja" och tas sedan bort så rummet kan bokas igen.
4. **Ändra antal personer på en bokning** – välj kund, se dennes bokningar som en komplett tabell (samma som i avbokningen, med en kapacitetskolumn), välj en bokning och ange ett nytt antal personer. Talet får aldrig överstiga rummets kapacitet, och ändringen sparas i databasen med `UPDATE`.

### Kör

```bash
dotnet run
```

### Kodstruktur

Koden är uppdelad i mindre filer efter funktion och område. Filer som
slutar på `Menu.cs` är konsolgränssnitt och skriver aldrig SQL, medan
`Db.cs`-filerna innehåller databasanrop och aldrig skriver i konsolen:

| Fil | Innehåll |
|-----|----------|
| `Program.cs` | Startpunkt, huvudmeny och appens gemensamma tillstånd (vald kund, sökta datum och antal personer). |
| `CustomerMenu.cs` | Meny 1 – registrera kund, samt kundvalet (första steget i meny 2, används även i avbokningen och i meny 4). |
| `SearchMenu.cs` | Meny 2 – välj kund och sök lediga rum (ort, antal personer, datum, valfria filter och sortering), med direktbokning i slutet. |
| `BookingMenu.cs` | Meny 3 – avboka (via kund eller via rum), meny 4 – ändra antal personer, samt de delade bokningsstegen som direktbokningen i sökningen använder. |
| `ConsoleInput.cs` | Hjälpmetoder för inläsning av användarinput (frågar om vid felaktig inmatning; i valfria filter betyder Enter "hoppa över"). |
| `Table.cs` | Gemensam hjälpklass som skriver ut tabeller i konsolen med kolumnrubriker. |
| `HolidayMakerDb.cs` | Anslutningssträng och gemensamma hjälpmetoder för databasskiktet. |
| `CustomerDb.cs` | SQL för kunder: lista alla, hämta ett namn och registrera en ny kund. |
| `SearchDb.cs` | SQL för sökning: orter, boenden, rum per boende och lediga rum. |
| `BookingDb.cs` | SQL för bokningar: skapa (med kontroll i transaktion), lista och avboka. |
| `Models.cs` | Record-typer som bär på data ur databasen (Customer, RoomResult, ...). |

### Anslutning

Databasanslutningen ligger i `HolidayMakerDb.cs` (konstant `ConnectionString`).
Uppdatera `Server`, `User Id` och `Password` efter behov.

## Designval

- **Tillgänglighet:** ett rum är ledigt om inget datumintervall överlappar:
  `b.start_date < @endDate AND b.end_date > @startDate`
  (utcheckning och incheckning samma dag är tillåtet). Utöver detta har
  varje rum en statusflagga: `status = 1` = bokat (rummet exkluderas ur
  sökningen och kan inte bokas), 0 eller NULL = tillgängligt.
- **Konkurrens:** bokning görs i en transaktion med `SELECT … FOR UPDATE`
  på rum och befintliga bokningar för att undvika dubbelbokning.
- **Kapacitet:** antalet personer får aldrig överstiga rummets `capacity`.
- **Totalpris:** sparas vid bokningstidpunkt som `nätter × pris/natt`.
- **E-post är unik:** `UNIQUE` på `Customers.email`, fel 1062 fångas i UI:et.

## Testdokumentation

Manuella tester i applikationen (utgångsläget är testdatan i
`testdata.sql`). Varje punkt motsvarar ett krav i uppgiftstexten:

1. **Ledigt rum visas, upptaget rum filtreras bort** – Sök i Åre
   12/1–12/4 för 2 personer: Fjällpensionatets rum 301 (bokad 1–10/12) och
   302 (bokad 1–5/12) utesluts, medan 303 och Lilla Gårdens lediga rum visas.
2. **Rum kan bokas samma dag som en tidigare kund checkar ut** – Bokning 2 i
   rum 101 (Strandhotell Ystad) går 10–15/12. Sök rummet 12/15–12/18 → det
   visas som ledigt och kan bokas: utcheckningsdagen upptar inte rummet.
3. **Överlappande bokningar nekas, även när den ena perioden helt omsluter
   den andra** – Rum 103 är bokat 5–20/12 (bokning 5). Sök Ystad
   12/8–12/12 för 4 personer: 103 utesluts. Den slutliga kontrollen ligger i
   spartillfället: `CreateBooking` kontrollerar tillgängligheten på nytt i
   transaktionen (`SELECT … FOR UPDATE`) och nekar om någon hinner boka
   mellan sökning och bekräftelse.
4. **För stort sällskap kan inte boka rummet** – Sök 6 personer i Ystad →
   inga rum (det största är 4-personersrum 103). Antalet personer är dessutom
   låst till rummets kapacitet i menyn vid bokning.
5. **Kombinerade sökfiter och båda sorteringsalternativen** – Sök Helsingborg
   12/11–12/12 för 2 personer med kraven *pool* + *max 200 m till centrum* →
   exakt 4 träffar, i prisordning: rum 201 (1 200 kr), 205 (1 250 kr),
   202 (1 900 kr) och 204 (2 800 kr) på Stadscentrum Hotell; 203 har bara
   plats för 1 person. Sök sedan Ystad 12/1–12/10
   för 2 personer utan filter och växla sortering: *pris* → 14 (1 150 kr),
   102 (1 500 kr), 13 (1 800 kr), 201 (2 400 kr); *omdöme* → Strandhotell
   Ystads rum (4,70) före Pensionat Vid Hamnens (4,10).
6. **Ändring av antal personer följer rummets kapacitet** – I "Ändra antal
   personer" (meny 4) välj Emma Eriksson och bokning 5 (rum 103, ett
   4-personersrum, 4 pers). Ange 5 personer → frågan accepterar bara 1–4
   och frågar om. Ange istället 3 och bekräfta → "Bokning 5 har nu 3
   personer." (Ändra sedan tillbaka till 4.) Gränsen hålls även i
   databasen: UPDATE:en har villkoret `@occupants <= r.capacity`, så ett
   för stort tal kan aldrig sparas.
7. **Avbokning frigör rummet** – I "Avboka" (meny 3) välj väg 1 (via kund),
   välj Emma Eriksson och avboka bokning 5 (rum 103, 5–20/12) – eller
   använd väg 2 (via rum) för att hitta samma bokning via Strandhotell Ystad, rum 103 –
   och sök sedan Ystad 12/7–12/10 för 4 personer → 103 dyker upp igen.
8. **Uppgifterna finns kvar efter omstart** – Välj 0 (Avsluta), starta
   programmet igen med `dotnet run` → kunder och bokningar som skapats
   finns kvar, eftersom all data ligger i MySQL och inte i applikationen.
9. **Rum flaggat som bokat visas inte och kan inte bokas** – Sätt t.ex.
   `UPDATE Rooms SET status = 1 WHERE id = 5;` (Strandhotell Ystad, rum
   201) i databasen. Sök sedan Ystad → rum 201 visas inte längre, och
   ett försök att boka det nekas med "Rummet är redan bokat." Sätt
   tillbaka `status` till `NULL` så rummet kan bokas igen.

## Reflektion (individuell)

### Datamodellen

Domänen är uppdelad i sex tabeller. En enklare modell hade kunnat ha
boolean-kolumner (`pool`, `restaurang`) direkt på `Hotels`, men jag valde en
egen `Amenities`-tabell med en kopplingstabell (`Hotel_x_Amenities`,
många-till-många): varje ny facilitet kräver ingen schemaändring, och
"har pool" blir ett sökbar villkor i SQL (`EXISTS`). Rum hänger under sitt
boende via `hotel_id`, och `room_number` är bara unikt *inom* boendet
(`UNIQUE (hotel_id, room_number)`) – två hotell får alltså ha rum 101.
Bokningen refererar till kund och rum; `booking_number` är en
autoinkrementerad primärnyckel som blir bokningsnumret. Den enda medvetna
avvikelsen från normaliseringen är `total_price`: totalkostnaden vid
bokningstidpunkten sparas som värde så att en senare prisändring på rummet
inte ändrar kunderns faktura. Periodtillgängligheten är inte sparad som
status utan beräknas ur datumintervallen: ett rum är ledigt om ingen
bokning överlappar sökperioden, med halvöppet intervall `[start, end)`
så att utcheckning och incheckning samma dag inte krockar. Därtill har
rummet en statusflagga (1 = bokat, 0/NULL = tillgängligt) som utesluter
rummet ur sökningen och stoppar bokning av det, t.ex. om rummet är
uthyrt via en annan kanal.

### Säkerheten

All indata (namn, e-post, telefon, adress, datum, filter och sortering)
skickas som parametrar i parameteriserade anrop – användartext kan aldrig
tolkas som SQL, så SQL-injection är inte en risk. Applikationen har inget
login (enligt uppgiftens förenkling) och körs lokalt. Svagheter som jag ser:
anslutningssträngen med användarnamn och lösenord ligger som konstant i
källkoden (`HolidayMakerDb.cs`) och syns alltså för alla som har repot, och
MySQL-användaren har skrivrättigheter på hela databasen. I en produktionsmiljö
skulle anslutningsinformationen ligga i miljövariabler eller en
konfigurationsfil som inte ligger i repot, och applikationens användare
skulle ha minsta möjliga behörighet (bara `SELECT`/`INSERT`/`UPDATE`/
`DELETE` på tabellerna).

### Resultatet

Alla funktioner i uppgiften fungerar, och samtliga nio testscenarion
ovan har jag gått igenom manuellt i applikationen. Felaktig inmatning
(tomma fält, ogiltiga tecken, text i numeriska fält, felaktiga datum,
redan registrerad e-post) hanteras
med nytt försök eller meddelande istället för att programmet kraschar.
Bygget kör med 0 varningar och 0 fel, och SQL-frågorna testades i en
databasklient innan de togs in i C#-koden. Svårast var tillgänglighetslogiken
– överlappsformeln och att utcheckningsdagen inte upptar rummet – så jag
testade medvetet både gränsfallet (samma dag) och omslutande perioder.

### Möjliga förbättringar

- Tvinga "bara december 2026 är bokbart" som `CHECK` på `Bookings`.
- Byt bort `DELETE` vid avbokning mot en statuskolumn (t.ex.
  `bokning_status`) så att historiken bevaras.
- En rums-/prisklasstabell om man vill ha standard- och deluxe-rum.
- Paginering i sökresultatet om antalet träffar blir stort.
- Webbgrenssnitt i stället för konsol.

### Användning av AI

Jag har använt AI-kodassistenten Cline (i VS Code) som stöd vid
implementering av C#-koden, SQL-skripten och denna README. Assistenten
föreslog kod, schema och formuleringar; jag har själv gått igenom
datamodellen mot uppgiftens krav, testat SQL-frågorna i databasklienten,
kört applikationen och verifierat varje testscenario ovan manuellt samt
kontrollerat att testdatan (5 boenden, 20 rum, 5 kunder, 25 bokningar)
ligger rätt i databasen.
