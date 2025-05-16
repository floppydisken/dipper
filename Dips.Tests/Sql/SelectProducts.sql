-- name: SelectProducts
-- result: Dips.Tests.Models.Product
select * from products
where id = @Id:uuid;