/*
    GetSalesDeliveryNotesSummary
    billingStatus: NOT_INVOICED | INVOICED
    Criteri facturat: existeix INV1 amb BaseType=15 i BaseEntry=ODLN.DocEntry

    OPCIÓ B (retorns):
    - NOT_INVOICED: excloure només si està retornat al 100%.
      -> Ha d'existir almenys 1 línia DLN1 amb pendent > 0
         (DLN1.Quantity - SUM(RDN1.Quantity per BaseEntry/BaseLine) > 0)
*/
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

    CASE
        WHEN EXISTS (
            SELECT 1
            FROM INV1 I
            INNER JOIN OINV H ON H.DocEntry = I.DocEntry
            WHERE I.BaseType = 15
              AND I.BaseEntry = T0.DocEntry
              AND ISNULL(H.CANCELED,'N') = 'N'
        ) THEN 'INVOICED'

        WHEN EXISTS (
            SELECT 1
            FROM RDN1 R
            INNER JOIN ORDN RH ON RH.DocEntry = R.DocEntry
            WHERE R.BaseType = 15
              AND R.BaseEntry = T0.DocEntry
              AND ISNULL(RH.CANCELED,'N') = 'N'
        ) THEN 'HAS_RETURN'

        -- (Opcional) Abonament basat directament a l’albarà
        WHEN EXISTS (
            SELECT 1
            FROM RIN1 CM
            INNER JOIN ORIN CMH ON CMH.DocEntry = CM.DocEntry
            WHERE CM.BaseType = 15
              AND CM.BaseEntry = T0.DocEntry
              AND ISNULL(CMH.CANCELED,'N') = 'N'
        ) THEN 'HAS_CREDIT'

        ELSE 'NOT_INVOICED'
    END AS BillingStatus

FROM ODLN T0
WHERE
    T0.CardCode = @CardCode
    AND T0.DocDate >= @FromDate
    AND T0.DocDate <= @ToDate
    AND ISNULL(T0.CANCELED,'N') = 'N'
    AND (
        (UPPER(@BillingStatus) = 'INVOICED'
            AND EXISTS (
                SELECT 1
                FROM INV1 I
                INNER JOIN OINV H ON H.DocEntry = I.DocEntry
                WHERE I.BaseType = 15
                  AND I.BaseEntry = T0.DocEntry
                  AND ISNULL(H.CANCELED,'N') = 'N'
            )
        )
        OR
        (UPPER(@BillingStatus) = 'NOT_INVOICED'
            AND NOT EXISTS (
                SELECT 1
                FROM INV1 I
                INNER JOIN OINV H ON H.DocEntry = I.DocEntry
                WHERE I.BaseType = 15
                  AND I.BaseEntry = T0.DocEntry
                  AND ISNULL(H.CANCELED,'N') = 'N'
            )

            -- ✅ OPCIÓ B: només és "pendent" si NO està retornat al 100%
            AND EXISTS (
                SELECT 1
                FROM DLN1 L
                WHERE L.DocEntry = T0.DocEntry
                  AND (
                      L.Quantity
                      - ISNULL((
                          SELECT SUM(R.Quantity)
                          FROM RDN1 R
                          INNER JOIN ORDN RH ON RH.DocEntry = R.DocEntry
                          WHERE R.BaseType = 15
                            AND R.BaseEntry = L.DocEntry
                            AND R.BaseLine  = L.LineNum
                            AND ISNULL(RH.CANCELED,'N') = 'N'
                      ), 0)
                  ) > 0.0001
            )
            AND NOT EXISTS (
                SELECT 1
                FROM RIN1 CM
                INNER JOIN ORIN CMH ON CMH.DocEntry = CM.DocEntry
                WHERE CM.BaseType = 15
                  AND CM.BaseEntry = T0.DocEntry
                  AND ISNULL(CMH.CANCELED,'N') = 'N'
            )
        )
    )
ORDER BY T0.DocDate DESC, T0.DocNum DESC;
