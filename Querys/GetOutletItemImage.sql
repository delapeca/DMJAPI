-------------------------------------------------------------
-- Rutes d’imatge d’un article OUTLET
--
-- Paràmetres:
--   @ItemCode → codi d’article (OITM.ItemCode)
--
-- Retorna:
--   ThumbPath → OITM.U_XN_Thumb
--   HiResPath → OITM.U_XN_HiRes
-------------------------------------------------------------

SELECT
    ISNULL(U_XN_Thumb, '') AS ThumbPath,
    ISNULL(U_XN_HiRes, '') AS HiResPath
FROM OITM
WHERE ItemCode = @ItemCode;
