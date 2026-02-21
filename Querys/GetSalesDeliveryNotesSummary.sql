/*
    GetSalesDeliveryNotesSummary.sql
    Llistat d'albarans segons estat de facturació
    
    Paràmetres:
        @CardCode NVARCHAR(15)
        @FromDate DATE
        @ToDate DATE
        @BillingStatus NVARCHAR(20) - NOT_INVOICED | INVOICED
        
    NOT_INVOICED: Albarans amb ALGUNA línia no facturada (inclou parcialment facturats)
    INVOICED: Albarans amb ALGUNA línia facturada (inclou parcialment facturats)
*/

-- Variables declarades automàticament per DataAccess
-- DECLARE @CardCode NVARCHAR(15) = 'C00001';
-- DECLARE @FromDate DATE = '2024-01-01';
-- DECLARE @ToDate DATE = '2024-12-31';
-- DECLARE @BillingStatus NVARCHAR(20) = 'NOT_INVOICED';

IF UPPER(@BillingStatus) = 'NOT_INVOICED'
BEGIN
    -- ========================================================================
    -- ALBARANS NO FACTURATS (amb línies pendents)
    -- ========================================================================
    SELECT
        T0.DocEntry,
        T0.DocNum,
        T0.DocDate,
        T0.DocTotal,
        T0.DocCur,
        T0.NumAtCard,
        T0.U_XN_NomOperari,
        T0.U_XN_DescObra,
        T0.Comments,
        T0.DocStatus,
        T0.U_XN_Sign,
        T0.CANCELED,

        T0.U_XN_Obra,
        T0.U_XN_DescObra,
        T0.U_XN_Operari,
        T0.U_XN_NomOperari,

        CASE
            WHEN ISNULL(T0.CANCELED,'N') = 'Y' THEN 'Cancel·lat'
            WHEN T0.DocStatus = 'C' THEN 'Tancat'
            ELSE 'Obert'
        END AS DocStatusText,
        
        -- Estat de facturació detallat
        CASE
            WHEN NOT EXISTS (
                SELECT 1 FROM DLN1 L
                WHERE L.DocEntry = T0.DocEntry AND L.LineStatus = 'O'
            ) THEN 'FULLY_INVOICED'
            WHEN EXISTS (
                SELECT 1 FROM DLN1 L1
                WHERE L1.DocEntry = T0.DocEntry AND L1.LineStatus = 'C'
            ) AND EXISTS (
                SELECT 1 FROM DLN1 L2
                WHERE L2.DocEntry = T0.DocEntry AND L2.LineStatus = 'O'
            ) THEN 'PARTIALLY_INVOICED'
            ELSE 'NOT_INVOICED'
        END AS BillingStatus,
        
        -- Indicador de devolució
        CASE
            WHEN EXISTS (
                SELECT 1
                FROM RDN1 R
                INNER JOIN ORDN RH ON RH.DocEntry = R.DocEntry
                WHERE R.BaseType = 15
                  AND R.BaseEntry = T0.DocEntry
                  AND ISNULL(RH.CANCELED,'N') = 'N'
            ) THEN 'Y'
            ELSE 'N'
        END AS HasReturn

    FROM ODLN T0
    WHERE
        T0.CardCode = @CardCode
        AND T0.DocDate >= @FromDate
        AND T0.DocDate <= @ToDate
        AND ISNULL(T0.CANCELED,'N') = 'N'
        
        -- Té ALGUNA línia no facturada (LineStatus = 'O')
        AND EXISTS (
            SELECT 1
            FROM DLN1 L
            WHERE L.DocEntry = T0.DocEntry
              AND L.LineStatus = 'O'
              -- Validar que després de devolucions encara hi ha pendent
              AND (
                  L.Quantity
                  - ISNULL((
                      SELECT SUM(R.Quantity)
                      FROM RDN1 R
                      INNER JOIN ORDN RH ON RH.DocEntry = R.DocEntry
                      WHERE R.BaseType = 15
                        AND R.BaseEntry = L.DocEntry
                        AND R.BaseLine = L.LineNum
                        AND ISNULL(RH.CANCELED,'N') = 'N'
                  ), 0)
              ) > 0.0001
        )
        
        -- NO té abonament directe
        AND NOT EXISTS (
            SELECT 1
            FROM RIN1 CM
            INNER JOIN ORIN CMH ON CMH.DocEntry = CM.DocEntry
            WHERE CM.BaseType = 15
              AND CM.BaseEntry = T0.DocEntry
              AND ISNULL(CMH.CANCELED,'N') = 'N'
        )

    ORDER BY T0.DocDate DESC, T0.DocNum DESC;
END
ELSE IF UPPER(@BillingStatus) = 'INVOICED'
BEGIN
    -- ========================================================================
    -- ALBARANS FACTURATS (amb línies facturades)
    -- ========================================================================
    SELECT
        T0.DocEntry,
        T0.DocNum,
        T0.DocDate,
        T0.DocTotal,
        T0.DocCur,
        T0.NumAtCard,
        T0.U_XN_NomOperari,
        T0.U_XN_DescObra,
        T0.Comments,
        T0.DocStatus,
        T0.U_XN_Sign,
        T0.CANCELED,
        CASE
            WHEN ISNULL(T0.CANCELED,'N') = 'Y' THEN 'Cancel·lat'
            WHEN T0.DocStatus = 'C' THEN 'Tancat'
            ELSE 'Obert'
        END AS DocStatusText,
        
        -- Estat de facturació detallat
        CASE
            WHEN NOT EXISTS (
                SELECT 1 FROM DLN1 L
                WHERE L.DocEntry = T0.DocEntry AND L.LineStatus = 'O'
            ) THEN 'FULLY_INVOICED'
            WHEN EXISTS (
                SELECT 1 FROM DLN1 L1
                WHERE L1.DocEntry = T0.DocEntry AND L1.LineStatus = 'C'
            ) AND EXISTS (
                SELECT 1 FROM DLN1 L2
                WHERE L2.DocEntry = T0.DocEntry AND L2.LineStatus = 'O'
            ) THEN 'PARTIALLY_INVOICED'
            ELSE 'NOT_INVOICED'
        END AS BillingStatus,
        
        -- Indicador de devolució
        CASE
            WHEN EXISTS (
                SELECT 1
                FROM RDN1 R
                INNER JOIN ORDN RH ON RH.DocEntry = R.DocEntry
                WHERE R.BaseType = 15
                  AND R.BaseEntry = T0.DocEntry
                  AND ISNULL(RH.CANCELED,'N') = 'N'
            ) THEN 'Y'
            ELSE 'N'
        END AS HasReturn

    FROM ODLN T0
    WHERE
        T0.CardCode = @CardCode
        AND T0.DocDate >= @FromDate
        AND T0.DocDate <= @ToDate
        AND ISNULL(T0.CANCELED,'N') = 'N'
        
        -- Té ALGUNA línia facturada (LineStatus = 'C')
        AND EXISTS (
            SELECT 1
            FROM DLN1 L
            WHERE L.DocEntry = T0.DocEntry
              AND L.LineStatus = 'C'  -- Almenys 1 línia tancada (facturada)
        )

    ORDER BY T0.DocDate DESC, T0.DocNum DESC;
END