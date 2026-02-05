------------------------------------------------------------
-- intranetSetup · Obres · Llistat per client
-- Params:
--   @CardCode NVARCHAR(15)
--   @OnlyActive NVARCHAR(1)  -- 'Y' o 'N' o '' (si buit, no filtra)
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
    AND (
        @OnlyActive IS NULL OR @OnlyActive = '' OR
        U_XN_Activa = @OnlyActive
    )
ORDER BY
    ISNULL(U_XN_Activa,'Y') DESC,
    Code DESC;