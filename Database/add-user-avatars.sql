BEGIN;

CREATE TABLE IF NOT EXISTS public."UserAvatars" (
    "UserId" text PRIMARY KEY REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE,
    "Photo" bytea NOT NULL
);

COMMIT;
