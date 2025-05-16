-- name: SelectUser
-- result: Dips.Tests.Models.User
-- Create user record
SELECT * FROM users
WHERE (COALESCE id = @Id:uuid, TRUE) AND name = @Name:text;
