------------------------------------------------------------
-- intranetSetup · Operaris · Tancar/Reobrir (U_XN_Actiu)
-- Params:
--   @CardCode NVARCHAR(15)
--   @Code INT
--   @Actiu NVARCHAR(1)  -- 'Y'/'N'
------------------------------------------------------------
UPDATE [dbo].[@XNOPERARIS]
SET
  U_XN_Actiu = @Actiu
WHERE
  U_XN_CodiClient = @CardCode
  AND Code = @Code;

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