/*
    GetSalesDeliveryNoteDetail
    Canvi: NO mostrar línies retornades al 100% (RDN1/ORDN)
*/
SELECT
    T0."DocEntry",
    T0."DocNum",
    T0."DocDate",
    T0."DocDueDate",
    T0."CardCode",
    T0."CardName",
    T0."NumAtCard",
    T0."U_XN_NomOperari",
    T0."U_XN_DescObra",
    T0."Comments",
    T0."DocCur",
    T0."DocTotal",
    T0."DocStatus",
    T0."CANCELED",
    CASE
        WHEN ISNULL(T0."CANCELED",'N') = 'Y' THEN 'Cancel·lat'
        WHEN T0."DocStatus" = 'C' THEN 'Tancat'
        ELSE 'Obert'
    END AS "DocStatusText",

    T1."LineNum",
    T1."ItemCode",
    COALESCE(I."ItemName", T1."Dscription") AS "ItemName",
    T1."Dscription",
    T1."Quantity",
    T1."unitMsr" AS "UnitMsr",
    T1."Price" AS "UnitPrice",
    T1."DiscPrcnt" AS "DiscountPercent",
    (T1."Price" * (1 - (T1."DiscPrcnt" / 100.0))) AS "NetUnitPrice",
    T1."LineTotal",
    T1."WhsCode",
    W."WhsName"
FROM ODLN T0
INNER JOIN DLN1 T1 ON T0."DocEntry" = T1."DocEntry"
LEFT JOIN OITM I ON T1."ItemCode" = I."ItemCode"
LEFT JOIN OWHS W ON T1."WhsCode" = W."WhsCode"
WHERE
    T0."CardCode" = @CardCode
    AND T0."DocEntry" = @DocEntry

    -- ✅ Excloure línies retornades 100% (si pendent <= 0, no surt)
    AND (
        T1."Quantity"
        - ISNULL((
            SELECT SUM(R."Quantity")
            FROM RDN1 R
            INNER JOIN ORDN RH ON RH."DocEntry" = R."DocEntry"
            WHERE R."BaseType" = 15
              AND R."BaseEntry" = T1."DocEntry"
              AND R."BaseLine"  = T1."LineNum"
              AND ISNULL(RH."CANCELED",'N') = 'N'
        ), 0)
    ) > 0.0001

ORDER BY T1."LineNum";
