-------------------------------------------------------------
-- Històric de compres d'un article per client
-- Paràmetres:
--   @CardCode  NVARCHAR(15)
--   @ItemCode  NVARCHAR(50)
--   @FromDate  DATE
--   @ToDate    DATE
------------------------------------------------------------

--DECLARE @CardCode NVARCHAR(15) = 'C009005';
--DECLARE @ItemCode NVARCHAR(50) = 'FE111668';
--DECLARE @FromDate DATE = '2024-01-01';
--DECLARE @ToDate   DATE = '2024-12-31';

SELECT
    -- 🔹 Capçalera document
    H.DocDate,
    H.DocNum,
    H.NumAtCard,
    H.CardCode,
    H.CardName,
    H.DocCur,

    -- 🔹 Article
    L.ItemCode,
    L.Dscription      AS ItemName,
    I.ItmsGrpCod,
    B.ItmsGrpNam,

    -- 🔹 Grup / Subgrup separats (patró "GRUP - SUBGRUP")
    CASE 
        WHEN CHARINDEX(' - ', B.ItmsGrpNam) > 0
            THEN LEFT(B.ItmsGrpNam, CHARINDEX(' - ', B.ItmsGrpNam) - 1)
        ELSE B.ItmsGrpNam
    END AS GroupName,
    CASE 
        WHEN CHARINDEX(' - ', B.ItmsGrpNam) > 0
            THEN LTRIM(SUBSTRING(B.ItmsGrpNam, CHARINDEX(' - ', B.ItmsGrpNam) + 3, 255))
        ELSE ''
    END AS SubGroupName,

    -- 🔹 Quantitats i preus a la data de la compra
    L.LineNum      AS DocLine,   -- 👈 línia del document
    L.Quantity,
    L.unitMsr          AS UnitMsr,         -- UM de la línia
    L.Price            AS UnitPrice,       -- preu llista (per unitat)
    L.Currency,
    L.DiscPrcnt        AS DiscountPercent, -- % descompte línia

    H.U_XN_Obra,
    H.U_XN_DescObra,
    H.U_XN_Operari,
    H.U_XN_NomOperari,

    -- Preu net unitari = Price * (1 - DiscPrcnt/100)
    CAST(
        L.Price * (1 - (ISNULL(L.DiscPrcnt, 0) / 100.0)
    ) AS NUMERIC(19,6)) AS NetUnitPrice,

    -- Total línia (sense IVA) tal com està a la factura
    L.LineTotal        AS LineTotal

FROM DLN1 L               -- línies de factura
JOIN ODLN H
    ON H.DocEntry = L.DocEntry
JOIN OITM I
    ON I.ItemCode = L.ItemCode
LEFT JOIN OITB B
    ON B.ItmsGrpCod = I.ItmsGrpCod

WHERE
    H.CardCode = @CardCode
    AND L.ItemCode = @ItemCode
    AND H.DocDate BETWEEN @FromDate AND @ToDate

ORDER BY
    H.DocDate DESC,
    H.DocNum DESC;
