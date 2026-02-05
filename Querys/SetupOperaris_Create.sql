------------------------------------------------------------
-- intranetSetup · Operaris · Crear
-- Params:
--   @CardCode NVARCHAR(15)
--   @Name NVARCHAR(100)
--   @Dni NVARCHAR(32)
--   @Telefon NVARCHAR(50)
--   @Observacions NVARCHAR(254)
--   @Encarregat NVARCHAR(1)  -- 'Y'/'N'
------------------------------------------------------------
INSERT INTO [dbo].[@XNOPERARIS]
(
  [Name],
  [U_XN_Targeta],
  [U_XN_CodiClient],
  [U_XN_DniOperari],
  [U_XN_Telefon],
  [U_XN_Observacions],
  [U_XN_Encarregat],
  [U_XN_Actiu]
)
VALUES
(
  NULLIF(@Name,''),
  NULL,
  @CardCode,
  NULLIF(@Dni,''),
  NULLIF(@Telefon,''),
  NULLIF(@Observacions,''),
  @Encarregat,
  'Y'
);

DECLARE @NewCode INT = SCOPE_IDENTITY();

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
    AND Code = @NewCode;