------------------------------------------------------------
-- intranetSetup · Obres · Crear
-- Params:
--   @CardCode NVARCHAR(15)
--   @Name NVARCHAR(100)
--   @Alias NVARCHAR(100)
--   @Adreca NVARCHAR(100)
--   @Ubicacio NVARCHAR(100)
--   @Contacte NVARCHAR(100)
--   @Telefon NVARCHAR(50)
--   @Observacions NVARCHAR(254)
------------------------------------------------------------
INSERT INTO [dbo].[@XNOBRES]
(
  [Name],
  [U_XN_Alias],
  [U_XN_Targeta],
  [U_XN_CodiClient],
  [U_XN_DataIni],
  [U_XN_Adreca],
  [U_XN_Ubicacio],
  [U_XN_Contacte],
  [U_XN_Telefon],
  [U_XN_Observacions],
  [U_XN_TransGrat],
  [U_XN_Activa]
)
VALUES
(
  @Name,
  NULLIF(@Alias,''),
  NULL,
  @CardCode,
  GETDATE(),
  NULLIF(@Adreca,''),
  NULLIF(@Ubicacio,''),
  NULLIF(@Contacte,''),
  NULLIF(@Telefon,''),
  NULLIF(@Observacions,''),
  'N',
  'Y'
);

DECLARE @NewCode INT = SCOPE_IDENTITY();

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
    AND Code = @NewCode;