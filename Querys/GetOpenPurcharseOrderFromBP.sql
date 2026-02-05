--declare @WhsCode as nvarchar(8);
--declare @CardCode as nvarchar(15);
--declare @DocNum as nvarchar(15);
--declare @empID as smallint;

--set @WhsCode = N'30';
--set @CardCode = N'P040160';
--set @DocNum = 24001748
--set @empID = 1;

--set @WhsCode = N'30';
--set @CardCode = N'P000494';
--set @DocNum = 24001618
--set @empID = 1;


drop table if exists #xav1;
SELECT T2.U_empID, T0.[CardCode], T0.[CardName], T0.DocNum, T0.DocEntry, T0.DocDate, T1.WhsCode, T1.LineNum, T1.ItemCode, T1.Dscription, T1.Quantity, T1.OpenQty, T1.Price, T1.DiscPrcnt
, sum(T2.U_RealQuantity) as U_RealQuantity, (T1.OpenQty-sum(T2.U_RealQuantity)) as VirtualQuantity, T2.U_LineStatus
into #xav1
FROM [dbo].[OPOR]  T0 
INNER JOIN [dbo].[POR1] T1 ON 
T0.[DocEntry] = T1.[DocEntry]
left JOIN [dbo].[@XNTMPGOODRECEIPT] T2
ON t2.U_empID = @empID
and T2.U_DocNum = T0.DocNum
AND T2.U_ItemCode = T1.ItemCode
and T2.U_LineNum = T1.LineNum
WHERE T1.[OpenQty] > 0 
and  T1.[WhsCode] = @WhsCode 
and  T0.[CANCELED] = 'N'
and T0.CardCode = @CardCode
and T0.DocNum = @DocNum
GROUP BY T2.U_empID, T0.[CardCode], T0.[CardName], T0.DocNum, T0.DocEntry, T0.DocDate, T1.WhsCode, T1.LineNum, T1.ItemCode, T1.Dscription, T1.Quantity, T1.OpenQty, T1.Price, T1.DiscPrcnt, T2.U_LineStatus
ORDER BY T1.LineNum;

update #xav1 set U_RealQuantity=0 where (U_RealQuantity is null or U_RealQuantity<0);
update #xav1 set VirtualQuantity=0 where (VirtualQuantity is null or VirtualQuantity<0);
update #xav1 set U_LineStatus=0 where U_LineStatus is null;

delete from #xav1 where  (OpenQty-U_RealQuantity) <= 0 ;

select * from #xav1;
drop table if exists #xav1;