------------------------------------------------------------
-- intranetSetup · Obres · Detall (1) per client i Code
-- Params:
--   @CardCode NVARCHAR(15)
--   @Code INT
------------------------------------------------------------
SELECT
    Code,
    Name,
    U_XN_Alias,
    U_XN_Adreca,
    U_XN_Ubicacio,
    U_XN_Contacte,
    U_XN_Telefon,
    U_XN_Observacions,
    U_XN_TransGrat,
    U_XN_Activa
FROM [dbo].[@XNOBRES]
WHERE
    U_XN_CodiClient = @CardCode
    AND Code = @Code;