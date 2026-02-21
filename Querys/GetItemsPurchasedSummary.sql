-------------------------------------------------------------
-- Resum d'articles comprats per client i període
--
-- Paràmetres:
--   @CardCode  - codi de client (OCRD.CardCode)
--   @FromDate  - data inicial (inclosa)
--   @ToDate    - data final (inclosa)
--
-- Cada fila = 1 article (amb grup, quantitat total i preus actuals)
-- El descompte actual ve d'EDG1 (igual que a GetItemsSalesPrices.sql)
------------------------------------------------------------

--DECLARE @CardCode NVARCHAR(15) = 'C009005';
--DECLARE @FromDate DATE = '2026-01-01';
--DECLARE @ToDate   DATE = '2026-12-31';

------------------------------------------------------------
-- Obtenir el codi de grup de client (BPGroupCod)
-- a partir del CardCode (igual que a GetItemsSalesPrices.sql)
------------------------------------------------------------
DECLARE @BPGroupCod VARCHAR(6);

SELECT  @BPGroupCod = T0.GroupCode
FROM    OCRD T0
WHERE   T0.CardCode = @CardCode;

------------------------------------------------------------
-- Selecció principal de compres
------------------------------------------------------------
SELECT
       -- Codi real de SAP (únic per GRUP, no per SUBGRUP)
    G.ItmsGrpCod AS GroupCode,

    -- Nom de GRUP (abans de ' - ') o tot el text si no hi ha subgrup
    CASE 
        WHEN CHARINDEX(' - ', G.ItmsGrpNam) > 0 
            THEN LEFT(G.ItmsGrpNam, CHARINDEX(' - ', G.ItmsGrpNam) - 1)
        ELSE G.ItmsGrpNam
    END AS GroupName,

    -- Nom de SUBGRUP (després de ' - '), o NULL si no n’hi ha
    CASE 
        WHEN CHARINDEX(' - ', G.ItmsGrpNam) > 0 
            THEN LTRIM(SUBSTRING(G.ItmsGrpNam, CHARINDEX(' - ', G.ItmsGrpNam) + 3, LEN(G.ItmsGrpNam)))
        ELSE NULL
    END AS SubGroupName,


    I.ItemCode,
    I.ItemName,
    
    --H.U_XN_Obra,
    --H.U_XN_DescObra,
    --H.U_XN_Operari,
    --H.U_XN_NomOperari,

    SUM(L.Quantity)    AS TotalQuantity,

    ISNULL(P.Price, 0)                            AS CurrentPrice,
    ISNULL(T8.Discount, 0)                        AS CurrentDiscount,
    ISNULL(P.Price, 0) * (1 - ISNULL(T8.Discount,0) / 100.0) AS CurrentNetPrice
FROM ODLN H
INNER JOIN DLN1 L  ON L.DocEntry = H.DocEntry
INNER JOIN OITM I  ON I.ItemCode = L.ItemCode
INNER JOIN OITB G  ON G.ItmsGrpCod = I.ItmsGrpCod
INNER JOIN OCRD C  ON C.CardCode   = H.CardCode

-- Preu actual per la tarifa del client
LEFT  JOIN ITM1 P  ON P.ItemCode   = I.ItemCode
                   AND P.PriceList = C.ListNum

------------------------------------------------------------
-- Descompte segons Grup de client (@BPGroupCod)
-- i Grup d'articles (ItmsGrpCod) → igual que al query de vendes
------------------------------------------------------------
LEFT JOIN OEDG T7
    ON T7.ObjCode = @BPGroupCod
   AND T7.ObjType = 10              -- 10 = per GRUP DE CLIENT

LEFT JOIN EDG1 T8
    ON T8.AbsEntry = T7.AbsEntry
   AND T8.ObjType  = 52             -- 52 = per GRUP D’ARTICLES
   AND T8.ObjKey   = I.ItmsGrpCod

WHERE
    H.CardCode = @CardCode
    AND H.DocDate >= @FromDate
    AND H.DocDate <  DATEADD(DAY, 1, @ToDate)
    AND ISNULL(H.Canceled, 'N') = 'N'
    AND L.ItemCode IS NOT NULL
    AND L.ItemCode <> ''
GROUP BY
    G.ItmsGrpCod,
    G.ItmsGrpNam,
    I.ItemCode,
    I.ItemName,
    P.Price,
    T8.Discount
    --H.U_XN_Obra,
    --H.U_XN_DescObra,
    --H.U_XN_Operari,
    --H.U_XN_NomOperari
ORDER BY
    G.ItmsGrpNam,
    I.ItemName;