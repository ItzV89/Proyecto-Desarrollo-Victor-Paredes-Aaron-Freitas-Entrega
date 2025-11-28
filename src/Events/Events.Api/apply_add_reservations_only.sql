START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    CREATE TABLE "Reservations" (
        "Id" uuid NOT NULL,
        "UserKeycloakId" text NOT NULL,
        "EventId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "Status" text NOT NULL,
        CONSTRAINT "PK_Reservations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    CREATE TABLE "ReservationSeat" (
        "Id" uuid NOT NULL,
        "ReservationId" uuid NOT NULL,
        "SeatId" uuid NOT NULL,
        "Code" text NOT NULL,
        "Type" text,
        "Price" numeric(10,2) NOT NULL,
        "ReservationId1" uuid,
        CONSTRAINT "PK_ReservationSeat" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReservationSeat_Reservations_ReservationId" FOREIGN KEY ("ReservationId") REFERENCES "Reservations" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ReservationSeat_Reservations_ReservationId1" FOREIGN KEY ("ReservationId1") REFERENCES "Reservations" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    CREATE INDEX "IX_ReservationSeat_ReservationId" ON "ReservationSeat" ("ReservationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    CREATE INDEX "IX_ReservationSeat_ReservationId1" ON "ReservationSeat" ("ReservationId1");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20251127210915_AddReservations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251127210915_AddReservations', '8.0.0');
    END IF;
END $EF$;
COMMIT;
