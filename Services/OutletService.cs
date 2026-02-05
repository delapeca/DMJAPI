using System;
using System.Data;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Servei de negoci per al catàleg OUTLET (articles + subfamílies).
    /// 
    /// De moment només retorna "[]" fins que hi enganxem els SQL reals.
    /// </summary>
    public class OutletService
    {
        private readonly Funcions _funcions = new Funcions();

        /// <summary>
        /// Garanteix que la connexió DOMENJÓ està inicialitzada.
        /// Mateix patró que OrdersService.
        /// </summary>
        private void EnsureConnection()
        {
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                Dades.SetupDades();
            }
        }

        /// <summary>
        /// Retorna, en JSON, les sub-famílies de la família OUTLET que tenen articles.
        /// (Ara mateix: placeholder "[]")
        /// </summary>
        public string GetOutletSubGroups()
        {
            EnsureConnection();

            // No tenim paràmetres per a aquest SQL
            string[] parametres = Array.Empty<string>();

            // Llegim el .sql
            string query = Funcions.GetQuery("GetOutletSubGroups.sql");

            // Execute’m i retornem JSON
            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            if (string.IsNullOrEmpty(jsonResult))
                return "[]";

            return jsonResult;
        }


        /// <summary>
        /// Retorna, en JSON, els articles d'OUTLET d'una sub-família.
        /// (Ara mateix: placeholder "[]")
        /// </summary>
        public string GetOutletItemsBySubGroup(string subGroupCode)
        {
            EnsureConnection();

            // ItmsGrpCod ve com a string (p.ex. "123") des del frontend
            string[] parametres =
            {
                $"ItmsGrpCod:{subGroupCode}"
            };

            string query = Funcions.GetQuery("GetOutletItemsBySubGroup.sql");

            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            if (string.IsNullOrEmpty(jsonResult))
                return "[]";

            return jsonResult;
        }

        public string GetOutletItemByCode(string itemCode)
        {
            EnsureConnection();

            // Paràmetre per al SQL: @ItemCode
            string[] parametres =
            {
                $"ItemCode:{itemCode}"
            };

            // Llegim el .sql que acabes de crear
            string query = Funcions.GetQuery("GetOutletItemByCode.sql");

            // Execute’m i retornem JSON
            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            if (string.IsNullOrEmpty(jsonResult))
                return "[]";

            return jsonResult;
        }

        public string GetOutletSubGroupsByGroup(string groupName)
        {
            EnsureConnection();

            string[] parametres =
            {
                $"GroupName:{groupName}"
            };

            string query = Funcions.GetQuery("GetOutletSubGroupsByGroup.sql");

            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            if (string.IsNullOrEmpty(jsonResult))
                return "[]";

            return jsonResult;
        }

        public string GetOutletGroups()
        {
            EnsureConnection();

            string[] parametres = Array.Empty<string>();

            string query = Funcions.GetQuery("GetOutletGroups.sql");

            string jsonResult = DataAccess.GetJSon(Dades.ConnectionStringDOMENJO, query, parametres);

            if (string.IsNullOrEmpty(jsonResult))
                return "[]";

            return jsonResult;
        }

        public (string ThumbPath, string HiResPath) GetOutletItemImagePaths(string itemCode)
        {
            EnsureConnection();

            string[] parametres =
                    {
                $"ItemCode:{itemCode}"
            };

            string query = Funcions.GetQuery("GetOutletItemImage.sql");

            DataTable table = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

            if (table.Rows.Count == 0)
                return (null, null);

            var row = table.Rows[0];

            string thumb = row["ThumbPath"] as string ?? string.Empty;
            string hiRes = row["HiResPath"] as string ?? string.Empty;

            return (thumb, hiRes);
        }


    }
}

