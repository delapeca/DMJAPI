--declare @Familia as nvarchar(25);
--set @Familia = N'EINES I MAQUINES';

drop table if exists #xav1;

------------------------------------------------------------
-- 1️⃣ Omplim la temporal amb CODI de grup + SUBFAMÍLIA
------------------------------------------------------------
select
    T0.ItmsGrpCod,   -- 🔹 Codi de grup d'articles (clau que després passarem a GetItemsSalesPrices)
    trim(
        substring(
            T0.ItmsGrpNam,
            charindex('-', T0.ItmsGrpNam, 0) + 2,
            len(T0.ItmsGrpNam) - charindex('-', T0.ItmsGrpNam, 0)
        )
    ) as subfamilia
into #xav1
from OITB T0
where
    T0.U_XN_Visible = 'Y'
    and trim(substring(T0.ItmsGrpNam, 0, charindex('-', T0.ItmsGrpNam, 0))) = @Familia;

------------------------------------------------------------
-- 2️⃣ Tornem llista única de (ItmsGrpCod, subfamilia)
------------------------------------------------------------
select
    ItmsGrpCod,
    subfamilia
from #xav1
group by
    ItmsGrpCod,
    subfamilia
order by
    subfamilia;
