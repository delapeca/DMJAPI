-------------------------------------------------------------
-- Resum de comandes de venda per client (ORDR)
--
-- Paràmetres:
--   @CardCode   NVARCHAR(15)
--   @FromDate   DATE
--   @ToDate     DATE
--   @Status     NVARCHAR(20)  -- OPEN / CLOSED / CANCELED / %
--   @DocNum     NVARCHAR(20)  -- opcional (DocNum exacte)
--   @NumAtCard  NVARCHAR(100) -- opcional (LIKE sobre NumAtCard)
--
-- Nota:
--   - @Status = '%' → no filtra per estat.
--   - @Status = 'OPEN'     → DocStatus = 'O' i no cancel·lada.
--   - @Status = 'CLOSED'   → DocStatus = 'C' i no cancel·lada.
--   - @Status = 'CANCELED' → CANCELED = 'Y'.
------------------------------------------------------------

-- Exemple de proves:
-- DECLARE @CardCode  NVARCHAR(15)  = 'C009005';
-- DECLARE @FromDate  DATE          = '2024-01-01';
-- DECLARE @ToDate    DATE          = '2024-12-31';
-- DECLARE @Status    NVARCHAR(20)  = '%';
-- DECLARE @DocNum    NVARCHAR(20)  = '';
-- DECLARE @NumAtCard NVARCHAR(100) = '';

SELECT
    H.DocEntry,
    H.DocNum,
    H.DocDate,
    H.DocDueDate,
    H.CardCode,
    H.CardName,
    H.NumAtCard,
    H.DocTotal,
    H.DocCur,
    H.U_XN_Sign,
    H.DocStatus,
    ISNULL(H.CANCELED, 'N') AS Canceled,

    -- Traduïm l'estat tècnic (DocStatus + CANCELED) a text d'usuari
    CASE
        WHEN ISNULL(H.CANCELED, 'N') = 'Y'
            THEN N'Cancel·lada'
        WHEN H.DocStatus = 'O'
            THEN N'Oberta'
        WHEN H.DocStatus = 'C'
            THEN N'Tancada'
        ELSE N'Desconegut'
    END AS OrderStatusText

FROM ORDR H
WHERE
    H.CardCode = @CardCode
    AND H.DocDate BETWEEN @FromDate AND @ToDate

    -- Filtre opcional per DocNum (si ve buit, no filtra)
    AND (
        @DocNum IS NULL
        OR @DocNum = ''
        OR H.DocNum = @DocNum
    )

    -- Filtre opcional per referència de client (NumAtCard) amb LIKE
    AND (
        @NumAtCard IS NULL
        OR @NumAtCard = ''
        OR H.NumAtCard LIKE @NumAtCard
    )

    -- Filtre per estat funcional
    AND (
        @Status = '%'
        OR (
            UPPER(@Status) = 'OPEN'
            AND ISNULL(H.CANCELED, 'N') = 'N'
            AND H.DocStatus = 'O'
        )
        OR (
            UPPER(@Status) = 'CLOSED'
            AND ISNULL(H.CANCELED, 'N') = 'N'
            AND H.DocStatus = 'C'
        )
        OR (
            UPPER(@Status) = 'CANCELED'
            AND ISNULL(H.CANCELED, 'N') = 'Y'
        )
    )

ORDER BY
    H.DocDate DESC,
    H.DocNum DESC;
