using Microsoft.Data.SqlClient;
using SAPbobsCOM;
using System.Data;
using XNDmjApi.Functions;
using XNDmjApi.Models;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Servei encarregat d'actualitzar UDFs de media (imatges + fitxes tècniques)
    /// dels articles a SAP mitjançant DI-API.
    /// 
    /// 🔹 Ara només és un esquelet: NO fa cap Update real a SAP.
    /// 🔹 Més endavant connectarem aquí amb la lògica existent (Funcions, Company, etc.).
    /// </summary>
    public class SapItemMediaService
    {
        private readonly Funcions _funcions;
        private Company _company;

        public SapItemMediaService()
        {
            _funcions = new Funcions();

            // 🔜 En una fase posterior:
            //     - Reutilitzarem la manera com ja connectes a SAP (Funcions, Dades, etc.)
            //     - Inicialitzarem _company amb la connexió DI-API existent.
            //
            // Exemple orientatiu (NO implementat ara):
            // _company = _funcions.GetCompany(); 
        }

        /// <summary>
        /// Actualitza la media (UDFs) de l'article origen.
        /// 
        /// A la implementació final:
        ///   - U_XN_HiRes, U_XN_Thumb, U_XN_FT, U_XN_LongDesc, etc.
        ///   - Segons flags CopyImage, CopyFicha, CopyDesc.
        /// </summary>
        public ItemMediaResultDetail UpdateSourceItemMedia(ProcessItemMediaRequest request,string? imagePath,string? techFilePath)
        {
            var result = new ItemMediaResultDetail
            {
                ItemCode = request.ItemCode,
                Success = false
            };

            if (request == null || string.IsNullOrWhiteSpace(request.ItemCode))
            {
                result.Messages.Add("Falta ItemCode per actualitzar media a SAP.");
                return result;
            }

            // 1️⃣ Assegurem que tenim connexió DI-API (SQL + credencials + Company)
            Company company;
            try
            {
                company = EnsureSapConnectionForMedia(result);
            }
            catch (Exception exConn)
            {
                result.Messages.Add("No s'ha pogut connectar a SAP (DI-API): " + exConn.Message);
                return result;
            }

            try
            {
                // 2️⃣ Carreguem l'article OITM
                Items oItem = (Items)company.GetBusinessObject(BoObjectTypes.oItems);
                if (!oItem.GetByKey(request.ItemCode))
                {
                    result.Messages.Add($"No s'ha trobat l'article a SAP (OITM) amb codi {request.ItemCode}.");
                    return result;
                }

                bool hasChanges = false;

                // 3️⃣ U_XN_HiRes / U_XN_Thumb (si tenim imagePath)
                if (!string.IsNullOrWhiteSpace(imagePath))
                {
                    try
                    {
                        oItem.UserFields.Fields.Item("U_XN_HiRes").Value = imagePath;
                        string thumbPath = imagePath.Replace("_HR.jpg", "_TM.jpg", StringComparison.OrdinalIgnoreCase);
                        oItem.UserFields.Fields.Item("U_XN_Thumb").Value = thumbPath;

                        result.UpdatedFields.Add("U_XN_HiRes");
                        result.UpdatedFields.Add("U_XN_Thumb");
                        hasChanges = true;
                    }
                    catch (Exception exFields)
                    {
                        result.Messages.Add($"Error assignant U_XN_HiRes/U_XN_Thumb a SAP: {exFields.Message}");
                    }
                }

                // 4️⃣ U_XN_FT (si tenim techFilePath)
                if (!string.IsNullOrWhiteSpace(techFilePath))
                {
                    try
                    {
                        oItem.UserFields.Fields.Item("U_XN_FT").Value = techFilePath;
                        result.UpdatedFields.Add("U_XN_FT");
                        hasChanges = true;
                    }
                    catch (Exception exFields)
                    {
                        result.Messages.Add($"Error assignant U_XN_FT a SAP: {exFields.Message}");
                    }
                }

                // 🔹 U_XN_LongDesc
                // Prioritat:
                //  - Si NoLongDesc=true => esborrem (posant string buit)
                //  - Si NoLongDesc=false i LongDesc té text => actualitzem amb el text
                bool wantsClearLongDesc = request.NoLongDesc;
                bool hasLongDescText = !string.IsNullOrWhiteSpace(request.LongDesc);

                if (wantsClearLongDesc)
                {
                    try
                    {
                        // En DI-API, el "clear" fiable d'un UDF de text acostuma a ser string buit
                        oItem.UserFields.Fields.Item("U_XN_LongDesc").Value = string.Empty;

                        result.UpdatedFields.Add("U_XN_LongDesc");
                        hasChanges = true;

                        result.Messages.Add("U_XN_LongDesc esborrada (NoLongDesc=true).");
                    }
                    catch (Exception exFields)
                    {
                        result.Messages.Add($"Error esborrant U_XN_LongDesc a SAP: {exFields.Message}");
                    }
                }
                else if (hasLongDescText)
                {
                    try
                    {
                        oItem.UserFields.Fields.Item("U_XN_LongDesc").Value = request.LongDesc;

                        result.UpdatedFields.Add("U_XN_LongDesc");
                        hasChanges = true;
                    }
                    catch (Exception exFields)
                    {
                        result.Messages.Add($"Error assignant U_XN_LongDesc a SAP: {exFields.Message}");
                    }
                }


                if (!hasChanges)
                {
                    result.Messages.Add("No hi ha cap camp de media a actualitzar a SAP per l'article origen.");
                    return result;
                }

                // 5️⃣ Fem l'Update a SAP
                int rc = oItem.Update();

                if (rc != 0)
                {
                    company.GetLastError(out int errCode, out string errMsg);
                    result.Messages.Add($"Error en actualitzar l'article a SAP (codi {errCode}): {errMsg}");
                    return result;
                }

                result.Success = true;
                result.Messages.Add("Media actualitzada correctament a SAP per l'article origen.");
            }
            catch (Exception ex)
            {
                result.Messages.Add("Excepció en actualitzar media a SAP: " + ex.Message);
            }

            return result;
        }


        /// <summary>
        /// Actualitza la media (UDFs) d'una variant/target a partir de
        /// la informació de l'article origen (paths i descripció llarga).
        /// </summary>
        public ItemMediaResultDetail UpdateVariantItemMedia(ProcessItemMediaRequest request,string variantItemCode)
        {
            var result = new ItemMediaResultDetail
            {
                ItemCode = variantItemCode,
                Success = false
            };

            if (request == null || string.IsNullOrWhiteSpace(request.ItemCode))
            {
                result.Messages.Add("Falta ItemCode d'origen per copiar media cap a la variant.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(variantItemCode))
            {
                result.Messages.Add("Falta el codi d'article de la variant.");
                return result;
            }

            // 1️⃣ Connexió DI-API (mateix helper que fem servir per l'origen)
            Company company;
            try
            {
                company = EnsureSapConnectionForMedia(result);
            }
            catch (Exception exConn)
            {
                result.Messages.Add("No s'ha pogut connectar a SAP (DI-API) per la variant: " + exConn.Message);
                return result;
            }

            try
            {
                // 2️⃣ Llegim l'article origen (OITM) per obtenir els valors a copiar
                Items oItemOrigin = (Items)company.GetBusinessObject(BoObjectTypes.oItems);
                if (!oItemOrigin.GetByKey(request.ItemCode))
                {
                    result.Messages.Add($"No s'ha trobat l'article origen a SAP (OITM) amb codi {request.ItemCode}.");
                    return result;
                }

                string srcHiRes = Convert.ToString(oItemOrigin.UserFields.Fields.Item("U_XN_HiRes").Value) ?? string.Empty;
                string srcThumb = Convert.ToString(oItemOrigin.UserFields.Fields.Item("U_XN_Thumb").Value) ?? string.Empty;
                string srcFT = Convert.ToString(oItemOrigin.UserFields.Fields.Item("U_XN_FT").Value) ?? string.Empty;
                string srcLongDesc = Convert.ToString(oItemOrigin.UserFields.Fields.Item("U_XN_LongDesc").Value) ?? string.Empty;

                // 3️⃣ Llegim l'article variant
                Items oItemVariant = (Items)company.GetBusinessObject(BoObjectTypes.oItems);
                if (!oItemVariant.GetByKey(variantItemCode))
                {
                    result.Messages.Add($"No s'ha trobat l'article variant a SAP (OITM) amb codi {variantItemCode}.");
                    return result;
                }

                bool hasChanges = false;

                // 🔹 copyImage → copiar U_XN_HiRes / U_XN_Thumb
                if (request.CopyImage)
                {
                    if (!string.IsNullOrWhiteSpace(srcHiRes))
                    {
                        try
                        {
                            oItemVariant.UserFields.Fields.Item("U_XN_HiRes").Value = srcHiRes;

                            string thumbToSet = !string.IsNullOrWhiteSpace(srcThumb)
                                ? srcThumb
                                : srcHiRes.Replace("_HR.jpg", "_TM.jpg", StringComparison.OrdinalIgnoreCase);

                            oItemVariant.UserFields.Fields.Item("U_XN_Thumb").Value = thumbToSet;

                            result.UpdatedFields.Add("U_XN_HiRes");
                            result.UpdatedFields.Add("U_XN_Thumb");
                            hasChanges = true;
                        }
                        catch (Exception exFields)
                        {
                            result.Messages.Add($"Error assignant U_XN_HiRes/U_XN_Thumb a la variant {variantItemCode}: {exFields.Message}");
                        }
                    }
                    else
                    {
                        result.Messages.Add(
                            $"copyImage actiu, però l'article origen {request.ItemCode} no té U_XN_HiRes informat."
                        );
                    }
                }

                // 🔹 copyFicha → copiar U_XN_FT
                if (request.CopyFicha)
                {
                    if (!string.IsNullOrWhiteSpace(srcFT))
                    {
                        try
                        {
                            oItemVariant.UserFields.Fields.Item("U_XN_FT").Value = srcFT;
                            result.UpdatedFields.Add("U_XN_FT");
                            hasChanges = true;
                        }
                        catch (Exception exFields)
                        {
                            result.Messages.Add($"Error assignant U_XN_FT a la variant {variantItemCode}: {exFields.Message}");
                        }
                    }
                    else
                    {
                        result.Messages.Add(
                            $"copyFicha actiu, però l'article origen {request.ItemCode} no té U_XN_FT informat."
                        );
                    }
                }

                // 🔹 copyDesc → acció sobre U_XN_LongDesc
                // Casos:
                //  - Si CopyDesc=true i NoLongDesc=true => ESBORREM a la variant (encara que l'origen sigui buit)
                //  - Si CopyDesc=true i NoLongDesc=false => copiem el text de l'origen (només si no és buit)
                if (request.CopyDesc)
                {
                    if (request.NoLongDesc)
                    {
                        try
                        {
                            oItemVariant.UserFields.Fields.Item("U_XN_LongDesc").Value = string.Empty;
                            result.UpdatedFields.Add("U_XN_LongDesc");
                            hasChanges = true;

                            result.Messages.Add($"U_XN_LongDesc esborrada a la variant {variantItemCode} (NoLongDesc=true).");
                        }
                        catch (Exception exFields)
                        {
                            result.Messages.Add($"Error esborrant U_XN_LongDesc a la variant {variantItemCode}: {exFields.Message}");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(srcLongDesc))
                        {
                            try
                            {
                                oItemVariant.UserFields.Fields.Item("U_XN_LongDesc").Value = srcLongDesc;
                                result.UpdatedFields.Add("U_XN_LongDesc");
                                hasChanges = true;
                            }
                            catch (Exception exFields)
                            {
                                result.Messages.Add($"Error assignant U_XN_LongDesc a la variant {variantItemCode}: {exFields.Message}");
                            }
                        }
                        else
                        {
                            result.Messages.Add(
                                $"copyDesc actiu, però l'article origen {request.ItemCode} no té U_XN_LongDesc informat."
                            );
                        }
                    }
                }


                if (!hasChanges)
                {
                    result.Messages.Add("No hi ha cap camp de media a actualitzar per aquesta variant.");
                    return result;
                }

                // 4️⃣ Fem l'Update a SAP per la variant
                int rc = oItemVariant.Update();
                if (rc != 0)
                {
                    company.GetLastError(out int errCode, out string errMsg);
                    result.Messages.Add($"Error en actualitzar la variant {variantItemCode} a SAP (codi {errCode}): {errMsg}");
                    return result;
                }

                result.Success = true;
                result.Messages.Add($"Media copiada correctament de {request.ItemCode} a la variant {variantItemCode}.");
                return result;
            }
            catch (Exception ex)
            {
                result.Messages.Add("Excepció en actualitzar media de la variant: " + ex.Message);
                return result;
            }
        }

        /// <summary>
        /// Retorna la informació de media d'un article (SQL, sense DI-API):
        ///  - U_XN_HiRes, U_XN_Thumb, U_XN_FT, U_XN_LongDesc
        ///  - Proveïdor preferent (OITM.CardCode + OCRD.CardName)
        /// </summary>
        public ItemMediaInfoResponse GetItemMediaInfo(string itemCode)
        {
            itemCode = (itemCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                throw new ArgumentException("itemCode buit.");
            }

            EnsureSqlConnectionReady(); // <-- prepara Dades.ConnectionStringDOMENJO

            // Objecte de resposta “sempre” retornable
            var resp = new ItemMediaInfoResponse
            {
                ItemCode = itemCode
            };

            const string sql = @"
                SELECT
                    T0.ItemCode,
                    ISNULL(CONVERT(nvarchar(254), T0.U_XN_HiRes), N'')              AS HiResPath,
                    ISNULL(CONVERT(nvarchar(254), T0.U_XN_Thumb), N'')             AS ThumbPath,
                    ISNULL(CONVERT(nvarchar(254), T0.U_XN_FT), N'')                AS TechFilePath,
                    ISNULL(CONVERT(nvarchar(max), T0.U_XN_LongDesc), N'')          AS LongDesc,
                    ISNULL(CONVERT(nvarchar(50),  T0.CardCode), N'')               AS PrefVendorCode,
                    ISNULL(CONVERT(nvarchar(200), T1.CardName), N'')               AS PrefVendorName
                FROM OITM T0
                LEFT JOIN OCRD T1 ON T1.CardCode = T0.CardCode
                WHERE T0.ItemCode = @ItemCode;
                ";

            using var cn = new SqlConnection(Dades.ConnectionStringDOMENJO);
            using var cmd = new SqlCommand(sql, cn);
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = 30;
            cmd.Parameters.Add("@ItemCode", SqlDbType.NVarChar, 50).Value = itemCode;

            cn.Open();

            using var rd = cmd.ExecuteReader(CommandBehavior.SingleRow);
            if (!rd.Read())
            {
                // No trobat: retornem l'objecte amb flags false i paths null
                return resp;
            }

            string hiRes = Convert.ToString(rd["HiResPath"]) ?? string.Empty;
            string thumb = Convert.ToString(rd["ThumbPath"]) ?? string.Empty;
            string ft = Convert.ToString(rd["TechFilePath"]) ?? string.Empty;
            string longDesc = Convert.ToString(rd["LongDesc"]) ?? string.Empty;

            string vCode = Convert.ToString(rd["PrefVendorCode"]) ?? string.Empty;
            string vName = Convert.ToString(rd["PrefVendorName"]) ?? string.Empty;

            resp.HasHiRes = !string.IsNullOrWhiteSpace(hiRes);
            resp.HiResPath = resp.HasHiRes ? hiRes : null;

            resp.HasThumb = !string.IsNullOrWhiteSpace(thumb);
            resp.ThumbPath = resp.HasThumb ? thumb : null;

            resp.HasTechFile = !string.IsNullOrWhiteSpace(ft);
            resp.TechFilePath = resp.HasTechFile ? ft : null;

            resp.HasLongDesc = !string.IsNullOrWhiteSpace(longDesc);
            resp.LongDesc = resp.HasLongDesc ? longDesc : null;

            resp.PrefVendorCode = !string.IsNullOrWhiteSpace(vCode) ? vCode : null;
            resp.PrefVendorName = !string.IsNullOrWhiteSpace(vName) ? vName : null;

            return resp;
        }

        /// <summary>
        /// Assegura que Dades.ConnectionStringDOMENJO està preparada.
        /// Reutilitza el patró que ja tens (SetupDades), sense DI-API.
        /// </summary>
        private static void EnsureSqlConnectionReady()
        {
            if (string.IsNullOrWhiteSpace(Dades.ConnectionStringDOMENJO))
            {
                // Mantinc el teu default existent
                if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                }

                Dades.SetupDades();
            }

            if (string.IsNullOrWhiteSpace(Dades.ConnectionStringDOMENJO))
            {
                throw new Exception("Dades.ConnectionStringDOMENJO està buit (SetupDades no l'ha pogut inicialitzar).");
            }
        }




        /// <summary>
        /// Assegura que hi ha:
        ///  - Connexió SQL inicialitzada (SetupDades)
        ///  - Credencials SAP omplertes (USER_SAP, PWD_SAP, COMPANY_SAP)
        ///  - Connexió DI-API oberta (Dades.oCompany)
        /// 
        /// 🔹 Si ja hi ha Dades.oCompany connectat, el reutilitza.
        /// 🔹 Si no hi ha credencials SAP, fa servir el mateix usuari tècnic
        ///     que al ProcessUnified.php: xnprog / d0m3nj0, BBDD SBO_DOMENJO.
        /// </summary>
        private Company EnsureSapConnectionForMedia(ItemMediaResultDetail result)
        {
            // 1️⃣ Assegurem la part SQL / Dades (mateix patró que ItemsService.EnsureConnection)
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                if (string.IsNullOrEmpty(Dades.DOMENJO_BBDD))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                }
                Dades.SetupDades();
            }

            // 2️⃣ Si ja hi ha un Company connectat, el reutilitzem
            if (Dades.oCompany != null && Dades.oCompany.Connected)
            {
                return Dades.oCompany;
            }

            // 3️⃣ Omplim credencials SAP si estan buides
            if (string.IsNullOrWhiteSpace(Dades.COMPANY_SAP))
            {
                Dades.COMPANY_SAP = string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD)
                    ? "SBO_DOMENJO"
                    : Dades.DOMENJO_BBDD;
            }

            if (string.IsNullOrWhiteSpace(Dades.USER_SAP) || string.IsNullOrWhiteSpace(Dades.PWD_SAP))
            {
                // ⚠️ Fallback temporal: usuari tècnic de la PHP ProcessUnified.php
                Dades.USER_SAP = "xnprog";
                Dades.PWD_SAP = "d0m3nj0";

                result.Messages.Add(
                    "S'està usant l'usuari tècnic SAP 'xnprog' per al mòdul de media (configurable més endavant)."
                );
            }

            // 4️⃣ Connectem via el patró oficial SAPLoginService.CompanyConnect
            var sapLogin = new SAPLoginService();
            string connectResult = Dades.CompanyConnect();

            if (!string.Equals(connectResult, "OK", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(connectResult);
            }

            if (Dades.oCompany == null || !Dades.oCompany.Connected)
            {
                throw new Exception("Després de CompanyConnect, Dades.oCompany segueix nul o desconnectat.");
            }

            return Dades.oCompany;
        }

    }
}

