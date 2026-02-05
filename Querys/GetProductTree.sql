--declare @ItemCode varchar(20)
--set @ItemCode = 'EI108335'

select T2.ItemCode, T1.Code, T1.Quantity, T1.Warehouse, T1.Price
from OITT T0  INNER JOIN ITT1 T1 ON 
T0.[Code] = T1.[Father]
join OITM T2 on
T1.Father = T2.ItemCode
where T0.Code = @ItemCode;