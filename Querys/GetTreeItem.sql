--declare @ItemCode varchar(20);

--set @ItemCode = 'EI108335';
--set @ItemCode = 'FE113038';
--set @ItemCode = 'EI108596'; -- (Tipus: vendes, Tipus S, el preu es el de l'article pare) Stayer lote
--set @ItemCode = 'SA105528'; -- (Tipus: modelo,Tipus T, el preu es la suma de tots els articles) Inodoro Vela 

-- Obtenim els codis que pengen del article pare
drop table if exists #xav0;
select T1.Code, T0.TreeType
into #xav0
from OITT T0 join ITT1 T1
ON T0.[Code] = T1.[Father]
where T0.Code=@ItemCode;

drop table if exists #xav1;
select T0.ItemCode, T0.ItemName, T12.Price, CAST(0 as numeric(19,6)) as UnitPrice, T2.ItmsGrpCod, T2.ItmsGrpNam, cast(T0.UserText as varchar(max)) as UserText, T0.U_XN_HiRes, T0.U_XN_Thumb, T0.CodeBars, T3.UomCode, T0.InvntryUom, T0.SWeight1, T4.UnitDisply as SWeight1Unit, T0.SLength1, T5.UnitDisply as SLength1Unit, T0.SHeight1,  T6.UnitDisply as SHeight1Unit, T0.SWidth1,T7.UnitDisply as SWidth1Unit, T9.AltQty, T9.BaseQty, T10.Code, T0.validFor, T11.UomCode as IUomCode, T.TreeType
into #xav1
from #xav0 T
join OITM T0
on T0.ItemCode=T.Code
join ITM1 T1
ON T1.ItemCode=T0.ItemCode
and T1.PriceList=1
join OITB T2
on T2.ItmsGrpCod=T0.ItmsGrpCod
left join OUOM T3
on T3.UomEntry=T0.SUoMEntry
left join OWGT T4
on T4.UnitCode=T0.SWght1Unit
left join OLGT T5
on T5.UnitCode=T0.SLen1Unit
left join OLGT T6
on T6.UnitCode=T0.SHght1Unit
left join OLGT T7
on T7.UnitCode= T0.SWdth1Unit
left join OUGP T8
on T8.UgpCode=T0.ItemCode
left join UGP1 T9
on T9.UgpEntry = T8.UgpEntry
and T9.UomEntry=T0.SUoMEntry
left join OITT T10
on T10.Code = T0.ItemCode
left join OUOM T11
on T11.UomEntry=T0.IUoMEntry
left join ITT1 T12
on T12.Code=T0.ItemCode;

drop table if exists #xav2;
select T0.ItemCode, T0.ItemName, T0.Price, T0.UnitPrice, T0.ItmsGrpCod, T0.ItmsGrpNam, T0.UserText, T0.U_XN_HiRes, T0.U_XN_Thumb, T0.CodeBars, T0.UomCode, T0.InvntryUom, T0.SWeight1, T0.SWeight1Unit, T0.SLength1, T0.SLength1Unit, T0.SHeight1, T0.SHeight1Unit, T0.SWidth1, T0.SWidth1Unit, T0.AltQty, T0.BaseQty, T0.Code, T0.validFor, sum(T1.OnHand) as OnHand, T0.IUomCode, T0.TreeType
into #xav2
from #xav1 T0 join OITW T1
ON T1.ItemCode=T0.ItemCode
GROUP BY T0.ItemCode, T0.ItemName, T0.Price, T0.UnitPrice, T0.ItmsGrpCod, T0.ItmsGrpNam, T0.UserText, T0.U_XN_HiRes, T0.U_XN_Thumb, T0.CodeBars, T0.UomCode, T0.InvntryUom, T0.SWeight1, T0.SWeight1Unit, T0.SLength1, T0.SLength1Unit, T0.SHeight1, T0.SHeight1Unit, T0.SWidth1, T0.SWidth1Unit, T0.AltQty, T0.BaseQty, T0.Code, T0.validFor, T0.IUomCode, T0.TreeType;

UPDATE #xav2 set UnitPrice = Price * BaseQty;

update #xav2 set UnitPrice = Price where UnitPrice is null;

select * from #xav2;