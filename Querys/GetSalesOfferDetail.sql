-------------------------------------------------------------
-- Detall d'una oferta de venda (capçalera + línies)
-- NOMÉS LÍNIES NO SERVIDES COMPLETAMENT
-- Paràmetres:
--   @CardCode NVARCHAR(15)
--   @DocEntry INT
--
-- Cada fila = 1 línia de l'oferta (QUT1), amb la
-- capçalera (OQUT) repetida.
------------------------------------------------------------
--DECLARE @CardCode NVARCHAR(15) = 'C033754';
--DECLARE @DocEntry INT = 1234;

SELECT
    -- 🔹 Capçalera oferta
    Q.DocEntry,
    Q.DocNum,
    Q.DocDate,
    Q.DocDueDate,
    Q.CardCode,
    Q.CardName,
    Q.NumAtCard,
    Q.DocCur,
    Q.DocTotal,
    Q.DocStatus,
    Q.CANCELED,
    CASE
        WHEN Q.CANCELED = 'Y' THEN 'Cancelled'
        WHEN Q.DocStatus = 'O' THEN 'Open'
        WHEN Q.DocStatus = 'C' THEN 'Closed'
        ELSE 'Unknown'
    END AS OfferStatusText,
    
    -- 🔹 Línia
    L.LineNum,
    L.ItemCode,
    L.Dscription       AS ItemName,
    L.Quantity,
    L.OpenQty,                           -- ⬅️ NOU: Quantitat pendent
    L.unitMsr          AS UnitMsr,
    L.Price            AS UnitPrice,
    L.Currency,
    L.DiscPrcnt        AS DiscountPercent,
    
    -- Preu net unitari = Price * (1 - DiscPrcnt/100)
    CAST(
        L.Price * (1 - (ISNULL(L.DiscPrcnt, 0) / 100.0))
        AS NUMERIC(19, 6)
    ) AS NetUnitPrice,
    
    -- Total línia (sense IVA)
    L.LineTotal        AS LineTotal,
    L.LineStatus,                        -- ⬅️ NOU: Estat de la línia
    
    -- Magatzem
    L.WhsCode,
    W.WhsName
    
FROM QUT1 L
JOIN OQUT Q
    ON Q.DocEntry = L.DocEntry
LEFT JOIN OWHS W
    ON W.WhsCode = L.WhsCode
WHERE
    Q.DocEntry = @DocEntry
    AND Q.CardCode = @CardCode
    AND L.LineStatus = 'O'               -- ⬅️ CLAU: Només línies obertes (no completament convertides)
ORDER BY
    L.LineNum;