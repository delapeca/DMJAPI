-------------------------------------------------------------
-- Selecció d’articles amb totes les unitats de mesura (UoM)
-- per a un client concret (@CardCode) i un grup d’articles.
--
-- Notes:
--  - Cada article (OITM) pot sortir repetit tantes vegades
--    com unitats de mesura hi hagi al grup UGP1.
--  - A partir d’aquí, al backend (C# / Laravel) es pot
--    agrupar per ItemCode i construir el JSON amb "units".
------------------------------------------------------------

--DECLARE @CardCode   NVARCHAR(15);
--DECLARE @ItemCode   NVARCHAR(50);
--DECLARE @ItemName   NVARCHAR(200);
--DECLARE @ItmsGrpCod VARCHAR(6);

-- ⚙️ Paràmetres d’entrada (exemple)
--SET @CardCode   = N'C009005';  -- Client
--SET @ItemCode   = '%';         -- Filtre per codi d’article (LIKE)
--SET @ItemName   = '%';         -- Filtre per descripcio d’article (LIKE)
--SET @ItmsGrpCod = '%';         -- Grup d’articles (ItmsGrpCod)

------------------------------------------------------------
-- Obtenir el codi de grup de client (BPGroupCod) a partir
-- del CardCode. Es farà servir per calcular descomptes.
------------------------------------------------------------

DECLARE @BPGroupCod VARCHAR(6);

SELECT  @BPGroupCod = T0.GroupCode
FROM    OCRD T0
WHERE   T0.CardCode = @CardCode;

------------------------------------------------------------
-- (Opcional) Esborrem una taula temporal si existia.
-- De moment no s’utilitza #xav1 en aquest script.
------------------------------------------------------------
DROP TABLE IF EXISTS #xav1;

------------------------------------------------------------
-- Selecció principal d’articles + preus + descomptes + UoM
------------------------------------------------------------
SELECT
    -- 🔹 Dades bàsiques de l’article
    T0.ItemCode,
    T0.ItemName,
    T0.ItmsGrpCod,
    T1.ItmsGrpNam,

    -- 🔹 Preus i descompte
    T1a.Price,
    T1a.Currency,
    T8.Discount,       -- Descompte segons grup de client i grup d’articles

    -- 🔹 Estocs
    T0.OnHand,
    T0.IsCommited       AS Committed,  -- Estoc compromès (comandes de venda)
    T0.OnOrder,
    T0.InvntryUom,      -- UM d'inventari (ex: ml, kg, unitats...)

    -- 🔹 Dades de la unitat de mesura (UoM) concreta (UGP1 + OUOM)
    T6.UomCode,
    T0.SalUnitMsr,     -- UM de venda per defecte (text)
    T0.NumInSale,      -- Nombre d’unitats d’inventari per UM de venda
    T0.PriceUnit,      -- UomEntry de la UM de preu
    T6.UomName,
    TInv.UomCode AS InventoryUomCode,


    -- 🔹 Pes / dimensions (unitats i valors)
    T0.SWeight1,
    T0.SWght1Unit,
    T2.UnitDisply AS SWeight1Unit,   -- Descripció unitat de pes

    T0.SHeight1,
    T0.SHght1Unit,
    T4.UnitDisply AS SHeight1Unit,   -- Descripció unitat d’alçada

    T0.SWidth1,
    T0.SWdth1Unit,
    T5.UnitDisply AS SWidth1Unit,    -- Descripció unitat d’amplada

    T0.SLength1,
    T0.SLen1Unit,
    T3.UnitDisply AS SLength1Uni,    -- Descripció unitat de llargada

    -- 🔹 Estat i configuració de l’article
    T0.validFor,      -- 'Y' si l’article és vàlid/actiu
    T0.UgpEntry,      -- Grup de UoM assignat a l’article
    T0.SUoMEntry,     -- UomEntry de la UM de venda per defecte
    T0.U_XN_StdMag,   -- Magatzem estàndard (camp personalitzat)

    -- 🔹 Dades específiques de cada línia de UGP1 (unitats de mesura)
    U1.UomEntry           AS UomEntry,          -- Identificador intern de la UM
    U1.BaseQty            AS BaseQty,           -- Quantitat base (revisa nom exacte a UGP1)
    U1.AltQty             AS AltQty,            -- Quantitat alternativa (idem)

    -- 🔹 Ruta de les imatges de l'article, miniatura(Thumb) i alta resolució (HiRes)
    T0.U_XN_Thumb,
    T0.U_XN_HiRes,

    CASE 
        WHEN U1.UomEntry = T0.SUoMEntry THEN 'Y'
        ELSE 'N'
    END                   AS IsDefaultSalesUom  -- Indica si és la UM de venda per defecte

    
FROM OITM T0
    -- Grup d’articles
    INNER JOIN OITB T1
        ON T1.ItmsGrpCod = T0.ItmsGrpCod
       AND T0.ItmsGrpCod LIKE @ItmsGrpCod

    -- Llista de preus (ITM1) → aquí usem la tarifa 1 com a exemple
    JOIN ITM1 T1a
        ON T1a.ItemCode  = T0.ItemCode
       AND T1a.PriceList = 1

    -- Unitats de pes i dimensions
    LEFT JOIN OWGT T2
        ON T2.UnitCode = T0.SWght1Unit

    LEFT JOIN OLGT T3
        ON T3.UnitCode = T0.SLen1Unit

    LEFT JOIN OLGT T4
        ON T4.UnitCode = T0.SHght1Unit

    LEFT JOIN OLGT T5
        ON T5.UnitCode = T0.SWdth1Unit

    --------------------------------------------------------
    -- Grup de unitats de mesura (UGP1) → totes les UoM
    --------------------------------------------------------
    LEFT JOIN UGP1 U1
        ON U1.UgpEntry = T0.UgpEntry    -- Una fila per cada UM del grup

    LEFT JOIN OUOM T6
        ON T6.UomEntry = U1.UomEntry    -- Dades descriptives de la UM (codi, nom, etc.)

    LEFT JOIN OUOM TInv
        ON TInv.UomEntry = T0.IUoMEntry



    --------------------------------------------------------
    -- Descompte segons Grup de client i grup d’articles
    --------------------------------------------------------
    LEFT JOIN OEDG T7
        ON T7.ObjCode = @BPGroupCod
       AND T7.ObjType = 10              -- 10 = per GRUP DE CLIENT

    LEFT JOIN EDG1 T8
        ON T8.AbsEntry = T7.AbsEntry
       AND T8.ObjType  = 52             -- 52 = per GRUP D’ARTICLES
       AND T8.ObjKey   = T0.ItmsGrpCod

    


WHERE T0.ItemCode LIKE @ItemCode
  AND T0.ItemName LIKE @ItemName
  AND T0.ValidFor = 'Y'      -- Només articles amb la fitxa oberta (actius)
  AND T0.U_BOY_TB_0 = 'N'
ORDER BY T0.ItemName;
