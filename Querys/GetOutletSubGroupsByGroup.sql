-------------------------------------------------------------
-- Subgrups OUTLET per GROUP amb articles i estoc
--
-- Format ItmsGrpNam:
--   OUTLET - XXXXX
--   OUTLET - XXXXX - YYYYY
--   OUTLET-XXXXX-YYYYY
--
-- Paràmetres:
--   @GroupName  → XXXXX
--
-- Retorna:
--   ItmsGrpCod     → codi numèric del grup SAP
--   ItmsGrpNam     → nom complet SAP (OUTLET - XXXXX - YYYYY)
--   GroupName      → XXXXX
--   SubGroupName   → YYYYY (o NULL si no hi ha subgrup)
--   ItemsCount     → nº d’articles amb estoc (>0)
-------------------------------------------------------------

--DECLARE @GroupName NVARCHAR(100) = 'FERRETERIA';

SELECT
    MIN(G.ItmsGrpCod)              AS ItmsGrpCod,
    MIN(G.ItmsGrpNam)              AS ItmsGrpNam,
    B.GroupName,
    C.SubGroupName,
    COUNT(DISTINCT I.ItemCode)     AS ItemsCount
FROM OITB G
JOIN OITM I
    ON I.ItmsGrpCod = G.ItmsGrpCod
JOIN OITW W
    ON W.ItemCode = I.ItemCode
CROSS APPLY (
    -- Part després del primer '-' i sense espais inicials
    SELECT LTRIM(
        CASE
            WHEN CHARINDEX('-', G.ItmsGrpNam) > 0
                THEN SUBSTRING(G.ItmsGrpNam, CHARINDEX('-', G.ItmsGrpNam) + 1, 200)
            ELSE G.ItmsGrpNam
        END
    ) AS AfterFirstDash
) A
CROSS APPLY (
    -- GroupName = text abans del segon '-' (si n'hi ha), sinó tot el que queda
    SELECT
        CASE
            WHEN CHARINDEX('-', A.AfterFirstDash) > 0
                THEN LTRIM(LEFT(A.AfterFirstDash, CHARINDEX('-', A.AfterFirstDash) - 1))
            ELSE A.AfterFirstDash
        END AS GroupName
) B
CROSS APPLY (
    -- SubGroupName = text després del segon '-' (si n'hi ha), sinó NULL
    SELECT
        CASE
            WHEN CHARINDEX('-', A.AfterFirstDash) > 0
                THEN LTRIM(SUBSTRING(A.AfterFirstDash, CHARINDEX('-', A.AfterFirstDash) + 1, 200))
            ELSE NULL
        END AS SubGroupName
) C
WHERE
    LEFT(G.ItmsGrpNam, 6) = 'OUTLET'
    AND W.OnHand > 0
    AND B.GroupName = @GroupName
GROUP BY
    B.GroupName,
    C.SubGroupName
ORDER BY
    C.SubGroupName;
