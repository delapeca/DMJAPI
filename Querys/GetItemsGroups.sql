drop table if exists #xav1;

select trim(substring(T0.ItmsGrpNam,0,charindex('-',T0.ItmsGrpNam,0))) as familia
into #xav1
from OITB T0
where T0.U_XN_Visible='Y';

select familia 
from #xav1
group by familia
order by familia