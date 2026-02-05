-- GetClientProfile.sql
-- Perfil bàsic de client (OCRD + OCRG + OCTG) + saldos (Balance/OrdersBal/DNotesBal)
-- Params (venen com NVARCHAR des de Funcions/DataAccess):
--   @CardCode NVARCHAR(...)

SET NOCOUNT ON;

SELECT
    T0.CardCode                                   AS CardCode,
    T0.CardName                                   AS CardName,
    T0.LicTradNum                                 AS Nif,                -- DNI/CIF/NIF
    T0.GroupCode                                  AS GroupCode,
    ISNULL(G.GroupName, N'')                      AS GroupName,
    T0.GroupNum                            AS PaymentGroupCode,
    ISNULL(PT.PymntGroup, N'')                    AS PaymentGroupName,
    ISNULL(T0.E_Mail, N'')                        AS Email,
    ISNULL(T0.Phone1, N'')                        AS Phone,
    ISNULL(T0.Cellular, N'')                      AS Mobile,

    -- Saldos estàndard B1
    CAST(ISNULL(T0.Balance, 0)   AS DECIMAL(19,6)) AS BalanceAccount,
    CAST(ISNULL(T0.OrdersBal, 0) AS DECIMAL(19,6)) AS BalanceOrders,
    CAST(ISNULL(T0.DNotesBal, 0) AS DECIMAL(19,6)) AS BalanceDeliveries

FROM OCRD T0
LEFT JOIN OCRG G  ON G.GroupCode = T0.GroupCode
LEFT JOIN OCTG PT ON PT.GroupNum = T0.GroupNum
WHERE T0.CardCode = @CardCode;

