------------------------------------------------------------
-- intranetSetup · Obres · Editar (només camps permesos) i només si Activa='Y'
-- Params:
--   @CardCode NVARCHAR(15)
--   @Code INT
--   @Contacte NVARCHAR(100)
--   @Telefon NVARCHAR(50)
--   @Observacions NVARCHAR(254)
------------------------------------------------------------
UPDATE [dbo].[@XNOBRES]
SET
  U_XN_Contacte = NULLIF(@Contacte,''),
  U_XN_Telefon = NULLIF(@Telefon,''),
  U_XN_Observacions = NULLIF(@Observacions,'')
WHERE
  U_XN_CodiClient = @CardCode
  AND Code = @Code
  AND ISNULL(U_XN_Activa,'Y') = 'Y';

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