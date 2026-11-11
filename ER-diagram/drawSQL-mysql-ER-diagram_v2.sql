CREATE TABLE `Hotels`(
    `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `name` VARCHAR(255) NOT NULL,
    `location` VARCHAR(255) NOT NULL,
    `distance_to_beach` INT NOT NULL,
    `distance_to_city` INT NOT NULL,
    `rating` DECIMAL(8, 2) NOT NULL
);
CREATE TABLE `Customers`(
    `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `name` VARCHAR(255) NOT NULL,
    `address` VARCHAR(255) NOT NULL,
    `email` VARCHAR(255) NOT NULL,
    `phone` VARCHAR(255) NOT NULL
);
ALTER TABLE
    `Customers` ADD UNIQUE `customers_email_unique`(`email`);
CREATE TABLE `Rooms`(
    `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `id_price_per_night` DECIMAL(8, 2) NOT NULL,
    `capacity` INT NOT NULL,
    `hotel_id` BIGINT NOT NULL,
    `room_number` INT NOT NULL,
    `status` BOOLEAN NOT NULL
);
CREATE TABLE `Bookings`(
    `booking_number` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `customer_id` BIGINT NOT NULL,
    `room_id` BIGINT NOT NULL,
    `start_date` DATE NOT NULL,
    `end_date` DATE NOT NULL,
    `total_price` DECIMAL(8, 2) NOT NULL,
    `number_of_occupant` INT NOT NULL
);
CREATE TABLE `Amenities`(
    `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `name` VARCHAR(255) NOT NULL
);
CREATE TABLE `Hotel_x_Amenities`(
    `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `hotel_id` BIGINT NOT NULL,
    `amenities_id` BIGINT NOT NULL
);
ALTER TABLE
    `Rooms` ADD CONSTRAINT `rooms_hotel_id_foreign` FOREIGN KEY(`hotel_id`) REFERENCES `Hotels`(`id`);
ALTER TABLE
    `Hotel_x_Amenities` ADD CONSTRAINT `hotel_x_amenities_amenities_id_foreign` FOREIGN KEY(`amenities_id`) REFERENCES `Amenities`(`id`);
ALTER TABLE
    `Bookings` ADD CONSTRAINT `bookings_customer_id_foreign` FOREIGN KEY(`customer_id`) REFERENCES `Customers`(`id`);
ALTER TABLE
    `Bookings` ADD CONSTRAINT `bookings_room_id_foreign` FOREIGN KEY(`room_id`) REFERENCES `Rooms`(`id`);
ALTER TABLE
    `Hotel_x_Amenities` ADD CONSTRAINT `hotel_x_amenities_hotel_id_foreign` FOREIGN KEY(`hotel_id`) REFERENCES `Hotels`(`id`);