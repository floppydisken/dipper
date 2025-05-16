-- name: SelectProducts
-- result: Dipper.Tests.Models.Product
select * from products
where id = @Id:uuid;