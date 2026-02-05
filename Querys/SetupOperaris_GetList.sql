------------------------------------------------------------
-- intranetSetup · Operaris · Llistat per client
-- Params:
--   @CardCode NVARCHAR(15)
--   @OnlyActive NVARCHAR(1)  -- 'Y'/'N'/'' (si buit, no filtra)
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
    AND (
        @OnlyActive IS NULL OR @OnlyActive = '' OR
        U_XN_Actiu = @OnlyActive
    )
ORDER BY
    ISNULL(U_XN_Actiu,'Y') DESC,
    Code DESC;