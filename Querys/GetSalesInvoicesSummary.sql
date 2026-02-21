-------------------------------------------------------------
-- GetSalesInvoicesSummary.sql
--
-- Resum de factures de venda per client (OINV)
--
-- Paràmetres:
--   @CardCode   NVARCHAR(15)
--   @FromDate   DATE
--   @ToDate     DATE
--   @DocNum     NVARCHAR(20)   -- opcional (DocNum exacte)
--   @NumAtCard  NVARCHAR(100)  -- opcional (LIKE sobre NumAtCard)
--
-- Nota:
--   - Només es tenen en compte factures no cancel·lades.
------------------------------------------------------------

/*
-- Exemple de proves:

DECLARE @CardCode  NVARCHAR(15)  = 'C009005';
DECLARE @FromDate  DATE          = '2024-01-01';
DECLARE @ToDate    DATE          = '2024-12-31';
DECLARE @DocNum    NVARCHAR(20)  = '';
DECLARE @NumAtCard NVARCHAR(100) = '';

*/

SELECT
    T0.DocEntry,
    T0.DocNum,
    T0.DocDate,
    T0.CardCode,
    T0.CardName,
    T0.NumAtCard,
    T0.DocCur,
    T0.DocTotal,
    T0.DocStatus,
    T0.CANCELED,

    T0.U_XN_Obra,
    T0.U_XN_DescObra,
    T0.U_XN_Operari,
    T0.U_XN_NomOperari,

    CASE
        WHEN ISNULL(T0.CANCELED,'N') = 'Y' THEN 'Cancel·lada'
        WHEN T0.DocStatus = 'C'       THEN 'Tancada'
        ELSE 'Oberta'
    END AS DocStatusText
FROM OINV T0
WHERE
    T0.CardCode = @CardCode
    AND T0.DocDate >= @FromDate
    AND T0.DocDate <= @ToDate
    AND ISNULL(T0.CANCELED,'N') = 'N'
    AND (
        @DocNum IS NULL
        OR @DocNum = ''
        OR CAST(T0.DocNum AS NVARCHAR(20)) = @DocNum
    )
    AND (
        @NumAtCard IS NULL
        OR @NumAtCard = ''
        OR T0.NumAtCard LIKE '%' + @NumAtCard + '%'
    )
ORDER BY
    T0.DocDate DESC,
    T0.DocNum DESC;
