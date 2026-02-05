------------------------------------------------------------
-- intranetSetup · Operaris · Editar (camps permesos) i només si Actiu='Y'
-- Params:
--   @CardCode NVARCHAR(15)
--   @Code INT
--   @Name NVARCHAR(100)
--   @Dni NVARCHAR(32)
--   @Telefon NVARCHAR(50)
--   @Observacions NVARCHAR(254)
--   @Encarregat NVARCHAR(1)
------------------------------------------------------------
UPDATE [dbo].[@XNOPERARIS]
SET
  [Name] = NULLIF(@Name,''),
  U_XN_DniOperari = NULLIF(@Dni,''),
  U_XN_Telefon = NULLIF(@Telefon,''),
  U_XN_Observacions = NULLIF(@Observacions,''),
  U_XN_Encarregat = @Encarregat
WHERE
  U_XN_CodiClient = @CardCode
  AND Code = @Code
  AND ISNULL(U_XN_Actiu,'Y') = 'Y';

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