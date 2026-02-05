-------------------------------------------------------------
-- Resum d'ofertes de venda per client
-- Paràmetres:
--   @CardCode   NVARCHAR(15)
--   @FromDate   DATE
--   @ToDate     DATE
--   @Status     NVARCHAR(20)  -- 'Open' / 'Closed' / 'Cancelled' / '%'
--   @DocNum     NVARCHAR(20)  -- opcional
--   @NumAtCard  NVARCHAR(100) -- opcional (ref. client)
------------------------------------------------------------

--DECLARE @CardCode   NVARCHAR(15)  = 'C0001';
--DECLARE @FromDate   DATE          = '2024-01-01';
--DECLARE @ToDate     DATE          = '2024-12-31';
--DECLARE @Status     NVARCHAR(20)  = '%';
--DECLARE @DocNum     NVARCHAR(20)  = '';
--DECLARE @NumAtCard  NVARCHAR(100) = '';

SELECT
    -- 🔹 Camps bàsics d’oferta
    Q.DocEntry,
    Q.DocNum,
    Q.DocDate,
    Q.DocDueDate,
    Q.CardCode,
    Q.CardName,
    Q.DocCur,
    Q.DocTotal,
    Q.NumAtCard,

    -- Estat tècnic SAP
    Q.DocStatus,   -- 'O' / 'C'
    Q.CANCELED,    -- 'N' / 'Y'

    -- Estat “humà” per a l’extranet
    CASE
        WHEN Q.CANCELED = 'Y' THEN 'Cancelled'
        WHEN Q.DocStatus = 'O' THEN 'Open'
        WHEN Q.DocStatus = 'C' THEN 'Closed'
        ELSE 'Unknown'
    END AS OfferStatusText

FROM OQUT Q
WHERE
    -- Client
    Q.CardCode = @CardCode

    -- Rang de dates
    AND Q.DocDate BETWEEN @FromDate AND @ToDate

    -- Filtre d'estat
    AND (
        @Status = '%'
        OR (@Status = 'Open'      AND Q.CANCELED = 'N' AND Q.DocStatus = 'O')
        OR (@Status = 'Closed'    AND Q.CANCELED = 'N' AND Q.DocStatus = 'C')
        OR ((@Status = 'Cancelled' OR @Status = 'Canceled') AND Q.CANCELED = 'Y')
    )

    -- Filtre per número d'oferta (si s'indica)
    AND (
        @DocNum IS NULL
        OR @DocNum = ''
        OR CAST(Q.DocNum AS NVARCHAR(20)) = @DocNum
    )

    -- Filtre per referència de client (NumAtCard, cerca parcial)
    AND (
        @NumAtCard IS NULL
        OR @NumAtCard = ''
        OR Q.NumAtCard LIKE '%' + @NumAtCard + '%'
    )

ORDER BY
    Q.DocDate DESC,
    Q.DocNum DESC;
