-- name: SelectUser
-- result: Dipper.Tests.Models.Product
-- Create user record
SELECT * FROM users
WHERE (COALESCE id = @Id:uuid, TRUE) AND name = @Name:text;
