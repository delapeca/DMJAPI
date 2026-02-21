/*
    GetSalesReturnsSummary
*/
SELECT
    T0.DocEntry,
    T0.DocNum,
    T0.DocDate,
    T0.DocTotal,
    T0.DocCur,
    T0.NumAtCard,
    T0.DocStatus,
    T0.CANCELED,

    T0.U_XN_Obra,
    T0.U_XN_DescObra,
    T0.U_XN_Operari,
    T0.U_XN_NomOperari,

    CASE
        WHEN ISNULL(T0.CANCELED,'N') = 'Y' THEN 'Cancel·lat'
        WHEN T0.DocStatus = 'C' THEN 'Tancat'
        ELSE 'Obert'
    END AS DocStatusText
FROM ORDN T0
WHERE
    T0.CardCode = @CardCode
    AND T0.DocDate >= @FromDate
    AND T0.DocDate <= @ToDate
    AND ISNULL(T0.CANCELED,'N') = 'N'
ORDER BY T0.DocDate DESC, T0.DocNum DESC;
