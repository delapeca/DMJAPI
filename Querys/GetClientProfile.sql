-- GetClientProfile.sql
-- Perfil bàsic de client (OCRD + OCRG + OCTG) + saldos (Balance/OrdersBal/DNotesBal)
-- Params (venen com NVARCHAR des de Funcions/DataAccess):
-- DECLARE @CardCode NVARCHAR(20) = 'C009005'; -- CardCode del client a consultar

SET NOCOUNT ON;

SELECT
    DB_NAME()                                   AS DatabaseName,
    T0.CardCode                                   AS CardCode,
    T0.CardName                                   AS CardName,
    T0.LicTradNum                                 AS Nif,
    T0.GroupCode                                  AS GroupCode,
    ISNULL(G.GroupName, N'')                      AS GroupName,
    T0.GroupNum                                   AS PaymentGroupCode,
    ISNULL(PT.PymntGroup, N'')                    AS PaymentGroupName,
    ISNULL(T0.E_Mail, N'')                        AS Email,
    ISNULL(T0.Phone1, N'')                        AS Phone,
    ISNULL(T0.Cellular, N'')                      AS Mobile,

    -- Método de pago por defecto
    T0.PymCode                                    AS PayMethCod,
    ISNULL(PM.Descript, N'')                      AS PayMethName,

    -- Campos solicitados previos
    CAST(ISNULL(T0.CreditLine, 0) AS DECIMAL(19,6)) AS CreditLine,
    CAST(ISNULL(T0.Balance, 0)   AS DECIMAL(19,6)) AS Balance,
    CAST(ISNULL(T0.DNotesBal, 0) AS DECIMAL(19,6)) AS DNotesBal,
    CAST(ISNULL(T0.OrdersBal, 0) AS DECIMAL(19,6)) AS OrdersBal,
    T0.VatStatus                                  AS VatStatus,
    ISNULL(T0.ECVatGroup, N'')                     AS ECVatGroup,

    -- Dirección fiscal por defecto (Bill-to)
    ISNULL(C1.Street, N'')                        AS BillStreet,
    ISNULL(C1.Block, N'')                         AS BillBlock,
    ISNULL(C1.ZipCode, N'')                       AS BillZipCode,
    ISNULL(C1.City, N'')                          AS BillCity,
    ISNULL(C1.Country, N'')                       AS BillCountry,
    ISNULL(C1.State, N'')                         AS BillState,
    ISNULL(C1.Address, N'')                       AS BillAddressName

FROM OCRD T0
LEFT JOIN OCRG  G   ON G.GroupCode    = T0.GroupCode
LEFT JOIN OCTG  PT  ON PT.GroupNum    = T0.GroupNum
LEFT JOIN OPYM  PM  ON PM.PayMethCod  = T0.PymCode
LEFT JOIN CRD1  C1  ON C1.CardCode    = T0.CardCode 
                    AND C1.Address    = T0.BillToDef 
                    AND C1.AdresType  = 'B'  -- Bill-to address
WHERE T0.CardCode = @CardCode;

