-------------------------------------------------------------
-- Groups OUTLET amb articles i estoc
--
-- Format ItmsGrpNam:
--   OUTLET - XXXXX
--   OUTLET - XXXXX - YYYYY
--   OUTLET-XXXXX-YYYYY
--
-- Retorna:
--   GroupName   → XXXXX
--   ItemsCount  → nº d'articles amb estoc (>0) d'aquest GROUP
--
-- Nota:
--   - Un GROUP pot tenir o no SUBGROUPS (YYYYY).
-------------------------------------------------------------

SELECT
    B.GroupName,
    COUNT(DISTINCT I.ItemCode) AS ItemsCount
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
WHERE
    -- Només grups OUTLET (accepta OUTLET-, OUTLET - , etc. mentre comenci per OUTLET)
    LEFT(G.ItmsGrpNam, 6) = 'OUTLET'
    AND W.OnHand > 0
GROUP BY
    B.GroupName
ORDER BY
    B.GroupName;
