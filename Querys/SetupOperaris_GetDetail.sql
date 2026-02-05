------------------------------------------------------------
-- intranetSetup · Operaris · Detall (1) per client i Code
-- Params:
--   @CardCode NVARCHAR(15)
--   @Code INT
------------------------------------------------------------
SELECT
    Code,
    Name,
    U_XN_DniOperari,
    U_XN_Telefon,
    U_XN_Observacions,
    U_XN_Encarregat,
    U_XN_Actiu
FROM [dbo].[@XNOPERARIS]
WHERE
    U_XN_CodiClient = @CardCode
    AND Code = @Code;