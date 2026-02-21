/* Querys/GetSalesOrdersDetail.sql
   Detall comanda de venda (ORDR + RDR1) per CardCode + DocEntry
   NOMÉS LÍNIES NO SERVIDES COMPLETAMENT
   Params: @CardCode NVARCHAR(15), @DocEntry INT
*/
SELECT
    T0."DocEntry",
    T0."DocNum",
    T0."DocDate",
    T0."DocDueDate",
    T0."CardCode",
    T0."CardName",
    T0."NumAtCard",
    T0."DocCur",
    T0."DocTotal",
    T0."DocStatus",
    T0."CANCELED",
    CASE
        WHEN T0."CANCELED" = 'Y' THEN 'Cancel·lada'
        WHEN T0."DocStatus" = 'C' THEN 'Tancada'
        ELSE 'Oberta'
    END AS "OrderStatusText",
    T1."LineNum",
    T1."ItemCode",
    COALESCE(I."ItemName", T1."Dscription") AS "ItemName",
    T1."Dscription",
    T1."Quantity",
    T1."OpenQty",                    -- ⬅️ NOU: Quantitat pendent de servir
    T1."DelivrdQty",                 -- ⬅️ NOU: Quantitat ja servida
    T1."unitMsr" AS "UnitMsr",
    T1."Price" AS "UnitPrice",
    T1."DiscPrcnt" AS "DiscountPercent",
    (T1."Price" * (1 - (T1."DiscPrcnt" / 100.0))) AS "NetUnitPrice",
    T1."LineTotal",
    T1."LineStatus",                 -- ⬅️ NOU: Estat de la línia (O=Open, C=Closed)
    T1."WhsCode",
    W."WhsName"
FROM ORDR T0
INNER JOIN RDR1 T1 ON T0."DocEntry" = T1."DocEntry"
LEFT JOIN OITM I ON I."ItemCode" = T1."ItemCode"
LEFT JOIN OWHS W ON W."WhsCode" = T1."WhsCode"
WHERE
    T0."CardCode" = @CardCode
    AND T0."DocEntry" = @DocEntry
    AND T1."LineStatus" = 'O'        -- ⬅️ CLAU: Només línies obertes (no completament servides)
ORDER BY T1."LineNum";