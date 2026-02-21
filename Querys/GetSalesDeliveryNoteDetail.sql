/*
    GetSalesDeliveryNoteDetail.sql
    Detall d'un albarà amb filtratge de línies segons estat
    
    Paràmetres:
        @CardCode NVARCHAR(15)
        @DocEntry INT
        @LineFilter NVARCHAR(20) - ALL | NOT_INVOICED | INVOICED
        
    ALL: Mostra totes les línies
    NOT_INVOICED: Només línies no facturades (LineStatus = 'O')
    INVOICED: Només línies facturades (LineStatus = 'C')
*/

-- Variables declarades automàticament per DataAccess
-- DECLARE @CardCode NVARCHAR(15) = 'C00001';
-- DECLARE @DocEntry INT = 1234;
-- DECLARE @LineFilter NVARCHAR(20) = 'ALL';

SELECT
    -- Capçalera albarà
    T0.DocEntry,
    T0.DocNum,
    T0.DocDate,
    T0.DocDueDate,
    T0.CardCode,
    T0.CardName,
    T0.NumAtCard,
    T0.U_XN_NomOperari,
    T0.U_XN_DescObra,
    T0.Comments,
    T0.DocCur,
    T0.DocTotal,
    T0.DocStatus,
    T0.CANCELED,
    CASE
        WHEN ISNULL(T0.CANCELED,'N') = 'Y' THEN 'Cancel·lat'
        WHEN T0.DocStatus = 'C' THEN 'Tancat'
        ELSE 'Obert'
    END AS DocStatusText,
    
    -- Línia
    T1.LineNum,
    T1.ItemCode,
    COALESCE(I.ItemName, T1.Dscription) AS ItemName,
    T1.Dscription,
    T1.Quantity,
    T1.OpenQty,                         -- Quantitat pendent segons SAP
    
    -- Quantitat retornada
    ISNULL((
        SELECT SUM(R.Quantity)
        FROM RDN1 R
        INNER JOIN ORDN RH ON RH.DocEntry = R.DocEntry
        WHERE R.BaseType = 15
          AND R.BaseEntry = T1.DocEntry
          AND R.BaseLine = T1.LineNum
          AND ISNULL(RH.CANCELED,'N') = 'N'
    ), 0) AS ReturnedQty,
    
    -- Quantitat facturada
    ISNULL((
        SELECT SUM(IV.Quantity)
        FROM INV1 IV
        INNER JOIN OINV IVH ON IVH.DocEntry = IV.DocEntry
        WHERE IV.BaseType = 15
          AND IV.BaseEntry = T1.DocEntry
          AND IV.BaseLine = T1.LineNum
          AND ISNULL(IVH.CANCELED,'N') = 'N'
    ), 0) AS InvoicedQty,
    
    -- Quantitat pendent neta (després de devolucions)
    (
        T1.Quantity
        - ISNULL((
            SELECT SUM(R.Quantity)
            FROM RDN1 R
            INNER JOIN ORDN RH ON RH.DocEntry = R.DocEntry
            WHERE R.BaseType = 15
              AND R.BaseEntry = T1.DocEntry
              AND R.BaseLine = T1.LineNum
              AND ISNULL(RH.CANCELED,'N') = 'N'
        ), 0)
    ) AS OpenQtyNet,
    
    T1.unitMsr AS UnitMsr,
    T1.Price AS UnitPrice,
    T1.DiscPrcnt AS DiscountPercent,
    (T1.Price * (1 - (T1.DiscPrcnt / 100.0))) AS NetUnitPrice,
    T1.LineTotal,
    T1.LineStatus,                      -- O=Open, C=Closed
    T1.WhsCode,
    W.WhsName,
    
    -- Factures relacionades (opcional, per debug)
    STUFF((
        SELECT ', ' + CAST(IVH.DocNum AS NVARCHAR(10))
        FROM INV1 IV
        INNER JOIN OINV IVH ON IVH.DocEntry = IV.DocEntry
        WHERE IV.BaseType = 15
          AND IV.BaseEntry = T1.DocEntry
          AND IV.BaseLine = T1.LineNum
          AND ISNULL(IVH.CANCELED,'N') = 'N'
        FOR XML PATH('')
    ), 1, 2, '') AS InvoiceNumbers
    
FROM ODLN T0
INNER JOIN DLN1 T1 ON T0.DocEntry = T1.DocEntry
LEFT JOIN OITM I ON T1.ItemCode = I.ItemCode
LEFT JOIN OWHS W ON T1.WhsCode = W.WhsCode
WHERE
    T0.CardCode = @CardCode
    AND T0.DocEntry = @DocEntry
    
    -- Filtre de línies segons @LineFilter
    AND (
        UPPER(@LineFilter) = 'ALL'
        OR (UPPER(@LineFilter) = 'NOT_INVOICED' AND T1.LineStatus = 'O' 
            AND (
                T1.Quantity
                - ISNULL((
                    SELECT SUM(R.Quantity)
                    FROM RDN1 R
                    INNER JOIN ORDN RH ON RH.DocEntry = R.DocEntry
                    WHERE R.BaseType = 15
                      AND R.BaseEntry = T1.DocEntry
                      AND R.BaseLine = T1.LineNum
                      AND ISNULL(RH.CANCELED,'N') = 'N'
                ), 0)
            ) > 0.0001
        )
        OR (UPPER(@LineFilter) = 'INVOICED' AND T1.LineStatus = 'C')
    )
    
ORDER BY T1.LineNum;
