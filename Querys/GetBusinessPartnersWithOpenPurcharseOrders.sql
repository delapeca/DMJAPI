--declare @WhsCode as nvarchar(8);

--set @WhsCode = '30'; -- N'%'

drop table if exists #xav1;
SELECT T0.[CardCode], T0.[CardName], T0.DocNum, T0.DocDate, T1.ItemCode, T1.OpenQty, sum(T2.U_RealQuantity) as U_RealQuantity
into #xav1
FROM [dbo].[OPOR]  T0 
INNER JOIN [dbo].[POR1] T1 ON 
T0.[DocEntry] = T1.[DocEntry]
LEFT JOIN [dbo].[@XNTMPGOODRECEIPT] T2
ON T2.U_DocNum = T0.DocNum
AND T2.U_ItemCode = T1.ItemCode
and T2.U_LineNum = T1.LineNum
WHERE T1.[OpenQty] > 0 
and  T1.[WhsCode] = @WhsCode 
and  T0.[CANCELED] = 'N'
and (T1.Quantity-T2.U_RealQuantity <> 0 or U_RealQuantity is null)
--and T0.CardCode = @CardCode
and (T2.U_LineStatus = 0 or T2.U_LineStatus is null)
GROUP BY T0.[CardCode], T0.[CardName], T0.DocNum, T0.DocDate, T1.ItemCode, T1.OpenQty
ORDER BY T0.CardCode, T0.DocDate;

delete from #xav1 where U_RealQuantity >= OpenQty;

select CardCode, CardName
from #xav1
GROUP BY CardCode, CardName
ORDER BY CardName;
