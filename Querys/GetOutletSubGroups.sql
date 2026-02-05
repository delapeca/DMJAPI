-------------------------------------------------------------
-- Subgrups OUTLET amb articles i estoc
--
-- Retorna:
--   ItmsGrpCod    → codi numèric (subgrup)
--   ItmsGrpNam    → text complet, p.ex. 'OUTLET - XXXXX' o 'OUTLET-XXXXX'
--   SubGroupName  → només la part 'XXXXX' (neteja espais)
--   ItemsCount    → nº d'articles amb estoc (>0)
-------------------------------------------------------------

SELECT
    G.ItmsGrpCod,
    G.ItmsGrpNam,
    -- Part després del primer '-' i sense espais inicials
    LTRIM(
        CASE
            WHEN CHARINDEX('-', G.ItmsGrpNam) > 0
                THEN SUBSTRING(G.ItmsGrpNam, CHARINDEX('-', G.ItmsGrpNam) + 1, 200)
            ELSE G.ItmsGrpNam
        END
    ) AS SubGroupName,
    COUNT(DISTINCT I.ItemCode) AS ItemsCount
FROM OITB G
JOIN OITM I
    ON I.ItmsGrpCod = G.ItmsGrpCod
JOIN OITW W
    ON W.ItemCode = I.ItemCode
WHERE
    -- Assegurem que el grup comença per OUTLET (accepta OUTLET- o OUTLET - o similars)
    LEFT(G.ItmsGrpNam, 6) = 'OUTLET'
    AND W.OnHand > 0
GROUP BY
    G.ItmsGrpCod,
    G.ItmsGrpNam
ORDER BY
    SubGroupName;

