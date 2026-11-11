-- ============================================================================
-- Holiday_Maker – SQL-frågor
-- Skrivna och provrunna i en databasklient innan de togs in i C#-koden.
-- Motsvarande parameteriserade anrop finns i SearchDb.cs och BookingDb.cs
-- (anslutningen och parametrarna sköts gemensamt i HolidayMakerDb.cs).
-- ============================================================================

USE Holiday_Maker;

-- ----------------------------------------------------------------------------
-- 1. Lediga rum: Ystad, 12–14 december 2026, 2 personer, pris lägst först.
--    Överlappsregeln: en bokning b överlappar sökperioden [start, end) om
--    b.start_date < end OCH b.end_date > start.
--    Utcheckningsdagen upptar alltså inte rummet.
-- ----------------------------------------------------------------------------
SELECT r.id                 AS rum_id,
       h.name               AS boende,
       h.location           AS ort,
       r.room_number        AS rumsnummer,
       r.capacity           AS kapacitet,
       r.id_price_per_night AS pris_per_natt,
       h.rating             AS omdome
FROM Rooms AS r
JOIN Hotels AS h ON h.id = r.hotel_id
WHERE h.location = 'Ystad'
  AND r.capacity >= 2
  AND (r.status IS NULL OR r.status = 0)
  AND NOT EXISTS (
        SELECT 1
        FROM Bookings AS b
        WHERE b.room_id = r.id
          AND b.start_date < '2026-12-14'
          AND b.end_date   > '2026-12-12')
ORDER BY r.id_price_per_night ASC, h.name ASC;

-- ----------------------------------------------------------------------------
-- 2. Samma typ av sökning med valfria filter:
--    pool krävs + maximalt 1000 m till stranden, sorterat på omdöme.
-- ----------------------------------------------------------------------------
SELECT r.id                 AS rum_id,
       h.name               AS boende,
       h.location           AS ort,
       r.room_number        AS rumsnummer,
       r.capacity           AS kapacitet,
       r.id_price_per_night AS pris_per_natt,
       h.rating             AS omdome
FROM Rooms AS r
JOIN Hotels AS h ON h.id = r.hotel_id
WHERE h.location = 'Ystad'
  AND r.capacity >= 2
  AND (r.status IS NULL OR r.status = 0)
  AND h.distance_to_beach <= 1000
  AND EXISTS (
        SELECT 1
        FROM Hotel_x_Amenities AS hxa
        JOIN Amenities AS a ON a.id = hxa.amenities_id
        WHERE hxa.hotel_id = h.id
          AND a.name = 'Pool')
  AND NOT EXISTS (
        SELECT 1
        FROM Bookings AS b
        WHERE b.room_id = r.id
          AND b.start_date < '2026-12-14'
          AND b.end_date   > '2026-12-12')
ORDER BY h.rating DESC, r.id_price_per_night ASC;

-- ----------------------------------------------------------------------------
-- 3. Visa alla bokningar för en kund (kund 1).
-- ----------------------------------------------------------------------------
SELECT b.booking_number       AS bokningsnummer,
       h.name                 AS boende,
       r.room_number          AS rumsnummer,
       b.start_date           AS incheckning,
       b.end_date             AS utcheckning,
       b.number_of_occupant   AS antal_personer,
       b.total_price          AS totalpris
FROM Bookings AS b
JOIN Rooms AS r ON r.id = b.room_id
JOIN Hotels AS h ON h.id = r.hotel_id
WHERE b.customer_id = 1
ORDER BY b.start_date, b.booking_number;

-- ----------------------------------------------------------------------------
-- 4. Ändra antal personer på en bokning (bokning 7: rum 104, kapacitet 1).
--    Villkoret "2 <= r.capacity" i WHERE-klausulen gör att ett för stort
--    tal aldrig sparas – uppdateringen påverkar 0 rader. (I applikationen
--    är samma kontroll parameteriserad i BookingDb.cs.)
-- ----------------------------------------------------------------------------
SELECT r.capacity
FROM Bookings AS b
JOIN Rooms AS r ON r.id = b.room_id
WHERE b.booking_number = 7;

-- Försök att ändra till 2 personer: påverkar 0 rader (kapaciteten är 1).
UPDATE Bookings AS b
JOIN Rooms AS r ON r.id = b.room_id
SET b.number_of_occupant = 2
WHERE b.booking_number = 7
  AND 2 <= r.capacity;

-- ----------------------------------------------------------------------------
-- 5. Avboka en bokning (bokning 3). Rummet kan sedan bokas igen.
-- ----------------------------------------------------------------------------
DELETE FROM Bookings
WHERE booking_number = 3;
