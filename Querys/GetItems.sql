declare @ItemCode as nvarchar(50);
declare @ItemName as nvarchar(100);
declare @ItmsGrpCod as int;
declare @ValidFor as int;

set @ItemCode = N'%';
set @ItemName = N'%';
set @ItmsGrpCod = N'%';
set @ValidFor = N'Y';

select 
T0.ItemCode
, T0.ItemName
, T0.ItmsGrpCod
, T1.ItmsGrpNam
, T1a.Price
, T1a.Currency
, T0.OnHand
, T0.OnOrder
, T6.UomCode
, T0.SalUnitMsr
, T0.SWeight1
, T0.SWght1Unit
, T2.UnitDisply as SWeight1Unit
, T0.SHeight1
, T0.SHght1Unit
, T4.UnitDisply as SHeight1Unit
, T0.SWidth1
, T0.SWdth1Unit
, T5.UnitDisply as SWidth1Unit
, T0.SLength1
, T0.SLen1Unit
, T3.UnitDisply as SLength1Uni
, T0.validFor
, T0.UgpEntry
, T0.SUoMEntry
, T0.U_XN_StdMag

from OITM T0 
inner join OITB T1
on T1.ItmsGrpCod = T0.ItmsGrpCod
join ITM1 T1a
ON T1a.ItemCode=T0.ItemCode
and T1a.PriceList=1
left join OWGT T2
on T2.UnitCode=T0.SWght1Unit
left join OLGT T3
on T3.UnitCode=T0.SLen1Unit
left join OLGT T4
on T4.UnitCode=T0.SHght1Unit
left join OLGT T5
on T5.UnitCode= T0.SWdth1Unit
left join OUOM T6
on T6.UomEntry=T0.SUoMEntry

where (T0.ItemCode like '%' + @ItemCode + '%' and T0.ItemName like '%' + @ItemName + '%' and T0.ItmsGrpCod like '%' + @ItmsGrpCod+ '%' and T0.ValidFor = @ValidFor )
order by T0.ItemName
;