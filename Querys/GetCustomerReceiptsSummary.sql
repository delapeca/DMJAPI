-------------------------------------------------------------
-- GetCustomerReceiptsSummary.sql
--
-- Cartera bàsica de rebuts (efectes) per client (OBOE).
--
-- Paràmetres (venen com NVARCHAR des de C#):
--   @CardCode       NVARCHAR(...)
--   @ReceiptStatus  NVARCHAR(20)   -- 'PENDING' | 'TRANSIT' | 'PAID' o buit = tots
--   @FromDate       DATE           -- pot venir NULL
--   @ToDate         DATE           -- pot venir NULL
--   @ReceiptNum     NVARCHAR(50)   -- opcional, BoeNum exacte
--   @NumAtCard      NVARCHAR(100)  -- reservat per futur
--   @MaxRows        NVARCHAR(?)    -- ve com text, el convertim a INT
-------------------------------------------------------------

SET NOCOUNT ON;

-------------------------------------------------------------
-- Normalització de @MaxRows (NVARCHAR → INT)
-------------------------------------------------------------
IF (@MaxRows IS NULL OR @MaxRows = N'' OR TRY_CONVERT(INT, @MaxRows) IS NULL OR TRY_CONVERT(INT, @MaxRows) <= 0)
BEGIN
    SET @MaxRows = N'500';
END;

-------------------------------------------------------------
-- CTE Base: UNA fila per efecte (només OBOE)
-------------------------------------------------------------
;WITH Base AS
(
    SELECT
        O.BoeKey      AS ReceiptId,
        O.CardCode    AS CardCode,
        O.DueDate     AS DueDate,
        O.TaxDate     AS TaxDate,
        O.BoeNum      AS BoeNum,
        O.BoeSum      AS BoeSum,
        O.BoeStatus   AS BoeStatus,
        O.Comments    AS Comments
    FROM OBOE O
    WHERE
        O.CardCode = @CardCode
        AND (@FromDate IS NULL OR O.DueDate >= @FromDate)
        AND (@ToDate   IS NULL OR O.DueDate <= @ToDate)
        AND (
            @ReceiptNum IS NULL
            OR @ReceiptNum = N''
            OR CAST(O.BoeNum AS NVARCHAR(50)) = @ReceiptNum
        )
)

-------------------------------------------------------------
-- SELECT final:
--   · 1 fila per efecte
--   · mapping de BoeStatus → PENDING / TRANSIT / PAID
--   · filtre funcional per @ReceiptStatus (si ve informat)
-------------------------------------------------------------
SELECT TOP (CAST(@MaxRows AS INT))
    -- Data principal (preferim DueDate, sinó TaxDate)
    ISNULL(B.DueDate, B.TaxDate)       AS [Date],

    -- Data de venciment
    B.DueDate                          AS [DueDate],

    -- Data de cobrament (no disponible ara mateix)
    CAST(NULL AS DATETIME)             AS [PaidDate],

    -- Número d’efecte / rebut
    B.BoeNum                           AS [Number],

    -- Origen (encara buit; ja el farem més endavant, quan tinguem BOE1 clar)
    CAST('' AS NVARCHAR(50))           AS [OriginDocType],
    CAST('' AS NVARCHAR(50))           AS [OriginDocNum],

    -- Estat funcional
    CASE
        WHEN B.BoeStatus IN ('G', 'S') THEN N'PENDING'   -- Generat / Enviat
        WHEN B.BoeStatus IN ('D')      THEN N'TRANSIT'   -- Depositat / en trànsit
        WHEN B.BoeStatus IN ('P')      THEN N'PAID'      -- Pagat
        ELSE N'PENDING'
    END                                AS [Status],

    -- Import de l’efecte
    B.BoeSum                           AS [Amount],

    -- Moneda (de moment NULL; ja decidirem la font exacta)
    CAST(NULL AS NVARCHAR(10))         AS [Currency],

    -- Banc / compte (reservat per futur)
    CAST(NULL AS NVARCHAR(100))        AS [BankAccount],

    -- Comentaris / observacions
    B.Comments                         AS [Remarks]

FROM Base B
WHERE
    (
        @ReceiptStatus IS NULL
        OR @ReceiptStatus = N''
        OR
        CASE
            WHEN B.BoeStatus IN ('G', 'S') THEN N'PENDING'
            WHEN B.BoeStatus IN ('D')      THEN N'TRANSIT'
            WHEN B.BoeStatus IN ('P')      THEN N'PAID'
            ELSE N'PENDING'
        END = @ReceiptStatus
    ) and B.BoeStatus not in ('C','L')  -- Excloem efectes cancel·lats o fallats
ORDER BY
    ISNULL(B.DueDate, B.TaxDate) DESC,
    B.BoeNum DESC;
