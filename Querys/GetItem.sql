--declare @ItemCode varchar(20);

--set @ItemCode = 'FU102458';
--set @ItemCode = 'FE113038';
--set @ItemCode = 'SA107428';
--set @ItemCode = 'CE100165';
--set @ItemCode = 'AI100001';

drop table if exists #xav1;

select
    T0.ItemCode,
    T0.ItemName,
    T1.Price,
    CAST(0 as numeric(19,6)) as UnitPrice,
    T2.ItmsGrpCod,
    T2.ItmsGrpNam,
    cast(T0.U_XN_LongDesc as varchar(max)) as U_XN_LongDesc,
    T0.U_XN_HiRes,
    T0.U_XN_Thumb,
    T0.CodeBars,
    T3.UomCode,
    T0.InvntryUom,
    T12.Weight1 as SWeight1,
    T4.UnitDisply as SWeight1Unit,
    T12.Length1 as SLength1,
    T5.UnitDisply as SLength1Unit,
    T12.Height1 as SHeight1,
    T6.UnitDisply as SHeight1Unit,
    T12.Width1 as SWidth1,
    T7.UnitDisply as SWidth1Unit,
    T9.AltQty,
    T9.BaseQty,
    T10.Code,
    T0.validFor,
    T11.UomCode as IUomCode,
    T0.U_XN_FT,
    T0.SalUnitMsr,

    -- IVA (vendes) per article
    T0.VatGourpSa as VatGroupSa,
    VAT.Name      as VatGroupName,
    CAST(VAT.Rate as numeric(19,6)) as VatRate,
    VAT.EffecDate as VatRateEffecDate,

    -- Proveïdor de referència (Preferred Vendor)
    T0.CardCode   as PrefVendorCode,
    V.CardName    as PrefVendorName

into #xav1
from OITM T0
join ITM1 T1
    ON T1.ItemCode = T0.ItemCode
   and T1.PriceList = 1
join OITB T2
    on T2.ItmsGrpCod = T0.ItmsGrpCod
left join ITM12 T12
    on T12.ItemCode = T0.ItemCode
   and T12.[UomType] = 'S'
   and T12.[UomEntry] = T0.[SUoMEntry]
left join OUOM T3
    on T3.UomEntry = T0.SUoMEntry
left join OWGT T4
    on T4.UnitCode = T12.Wght1Unit
left join OLGT T5
    on T5.UnitCode = T12.Len1Unit
left join OLGT T6
    on T6.UnitCode = T12.Hght1Unit
left join OLGT T7
    on T7.UnitCode = T12.Wdth1Unit
left join OUGP T8
    on T8.UgpCode = T0.ItemCode
left join UGP1 T9
    on T9.UgpEntry = T8.UgpEntry
   and T9.UomEntry = T0.SUoMEntry
left join OITT T10
    on T10.Code = T0.ItemCode
left join OUOM T11
    on T11.UomEntry = T0.IUoMEntry

-- Nom del proveïdor preferit
left join OCRD V
    on V.CardCode = T0.CardCode

-- Tipus d'IVA vigent (últim EffecDate <= avui) per al VatGourpSa de l'article
outer apply (
    select top (1)
        X.Code,
        X.Name,
        X.Rate,
        X.EffecDate
    from OVTG X
    where X.Code = T0.VatGourpSa
      and X.EffecDate <= CONVERT(date, GETDATE())
    order by X.EffecDate desc
) VAT

where T0.ItemCode = @ItemCode;

drop table if exists #xav2;

select
    T0.ItemCode,
    T0.ItemName,
    T0.Price,
    T0.UnitPrice,
    T0.ItmsGrpCod,
    T0.ItmsGrpNam,
    T0.U_XN_LongDesc,
    T0.U_XN_HiRes,
    T0.U_XN_Thumb,
    T0.CodeBars,
    T0.UomCode,
    T0.InvntryUom,
    T0.SWeight1,
    T0.SWeight1Unit,
    T0.SLength1,
    T0.SLength1Unit,
    T0.SHeight1,
    T0.SHeight1Unit,
    T0.SWidth1,
    T0.SWidth1Unit,
    T0.AltQty,
    T0.BaseQty,
    T0.Code,
    T0.validFor,
    sum(T1.OnHand) as OnHand,
    T0.IUomCode,
    T0.U_XN_FT,
    T0.SalUnitMsr,

    -- IVA (vendes)
    T0.VatGroupSa,
    T0.VatGroupName,
    T0.VatRate,
    T0.VatRateEffecDate,

    -- Proveïdor de referència
    T0.PrefVendorCode,
    T0.PrefVendorName

into #xav2
from #xav1 T0
join OITW T1
    ON T1.ItemCode = T0.ItemCode
group by
    T0.ItemCode, T0.ItemName, T0.Price, T0.UnitPrice, T0.ItmsGrpCod, T0.ItmsGrpNam,
    T0.U_XN_LongDesc, T0.U_XN_HiRes, T0.U_XN_Thumb, T0.CodeBars, T0.UomCode, T0.InvntryUom,
    T0.SWeight1, T0.SWeight1Unit, T0.SLength1, T0.SLength1Unit, T0.SHeight1, T0.SHeight1Unit,
    T0.SWidth1, T0.SWidth1Unit, T0.AltQty, T0.BaseQty, T0.Code, T0.validFor,
    T0.IUomCode, T0.U_XN_FT, T0.SalUnitMsr,
    T0.VatGroupSa, T0.VatGroupName, T0.VatRate, T0.VatRateEffecDate,
    T0.PrefVendorCode, T0.PrefVendorName;

UPDATE #xav2
set UnitPrice = Price * BaseQty;

update #xav2
set UnitPrice = Price
where UnitPrice is null;

select
    *,
    CAST(Price     * (1 + ISNULL(VatRate, 0) / 100.0) as numeric(19,6)) as PriceWithVat,
    CAST(UnitPrice * (1 + ISNULL(VatRate, 0) / 100.0) as numeric(19,6)) as UnitPriceWithVat
from #xav2;
