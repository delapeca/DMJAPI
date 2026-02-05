------------------------------------------------------------
-- intranetSetup · Obres · Tancar/Reobrir (U_XN_Activa)
-- Params:
--   @CardCode NVARCHAR(15)
--   @Code INT
--   @Activa NVARCHAR(1)  -- 'Y' o 'N'
------------------------------------------------------------
UPDATE [dbo].[@XNOBRES]
SET
  U_XN_Activa = @Activa
WHERE
  U_XN_CodiClient = @CardCode
  AND Code = @Code;

SELECT
    Code,
    Name,
    U_XN_Alias,
    U_XN_Adreca,
    U_XN_Ubicacio,
    U_XN_Contacte,
    U_XN_Telefon,
    U_XN_Observacions,
    U_XN_Activa
FROM [dbo].[@XNOBRES]
WHERE
    U_XN_CodiClient = @CardCode
    AND Code = @Code;