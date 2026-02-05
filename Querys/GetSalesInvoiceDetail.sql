/* 
    GetSalesInvoiceDetail.sql
    Detall factura de venda (OINV + INV1) per CardCode + DocEntry
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
    T0."U_XN_NomOperari",
    T0."U_XN_DescObra",
    T0."Comments",
    T0."DocCur",
    T0."DocTotal",
    T0."DocStatus",
    T0."CANCELED",
    CASE
        WHEN ISNULL(T0."CANCELED",'N') = 'Y' THEN 'Cancel·lada'
        WHEN T0."DocStatus" = 'C'       THEN 'Tancada'
        ELSE 'Oberta'
    END AS "DocStatusText",

    T1."LineNum",
    T1."ItemCode",
    COALESCE(I."ItemName", T1."Dscription") AS "ItemName",
    T1."Dscription",
    T1."Quantity",
    T1."unitMsr"      AS "UnitMsr",
    T1."Price"        AS "UnitPrice",
    T1."DiscPrcnt"    AS "DiscountPercent",
    (T1."Price" * (1 - (T1."DiscPrcnt" / 100.0))) AS "NetUnitPrice",
    T1."LineTotal",
    T1."WhsCode",
    W."WhsName"
FROM OINV T0
INNER JOIN INV1 T1 ON T0."DocEntry" = T1."DocEntry"
LEFT JOIN OITM I   ON T1."ItemCode" = I."ItemCode"
LEFT JOIN OWHS W   ON T1."WhsCode" = W."WhsCode"
WHERE
    T0."CardCode" = @CardCode
    AND T0."DocEntry" = @DocEntry
ORDER BY
    T1."LineNum";

