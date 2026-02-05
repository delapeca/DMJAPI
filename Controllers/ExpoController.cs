using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using SAPbobsCOM;
using System.Data;
using XNDmjApi.Functions;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XNMagatzemApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class ExpoController : ControllerBase
    {

        Funcions Funcions = new Funcions();

        // GET: api/<GetItem>
        [HttpPost("GetItem")]
        public string GetItem([FromForm] string? ItemCode)
        {
            Dades.USER_SQL = "xavi";
            Dades.PWD_SQL = "$Domenjo$";
            Dades.DOMENJO_BBDD = "SBO_DOMENJO";
            Dades.USER_SAP = "admin1";
            Dades.PWD_SAP = "master";
            Dades.SetupDades();

            string query = Funcions.GetQuery("GetItem.sql");
            //query += $"WHERE T0.ItemCode = '{ItemCode}' ;";

            List<SqlParameter> prm = new List<SqlParameter>();
            SqlParameter param = null;

            param = new SqlParameter("@ItemCode", ItemCode);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, query, prm);

            return JsonConvert.SerializeObject(dt);
        }

        // GET: api/<GetItem>
        [HttpPost("GetProductTree")]
        public string GetProductTree([FromForm] string? ItemCode)
        {
            Dades.USER_SQL = "xavi";
            Dades.PWD_SQL = "$Domenjo$";
            Dades.DOMENJO_BBDD = "SBO_DOMENJO";
            Dades.USER_SAP = "admin1";
            Dades.PWD_SAP = "master";
            Dades.SetupDades();

            string query = Funcions.GetQuery("GetProductTree.sql");
            //query += $"WHERE T0.ItemCode = '{ItemCode}' ;";

            List<SqlParameter> prm = new List<SqlParameter>();
            SqlParameter param = null;

            param = new SqlParameter("@ItemCode", ItemCode);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, query, prm);

            return JsonConvert.SerializeObject(dt);
        }

        // GET: api/<GetItem>
        [HttpPost("GetTreeItem")]
        public string GetTreeItem([FromForm] string? ItemCode)
        {
            Dades.USER_SQL = "xavi";
            Dades.PWD_SQL = "$Domenjo$";
            Dades.DOMENJO_BBDD = "SBO_DOMENJO";
            Dades.USER_SAP = "admin1";
            Dades.PWD_SAP = "master";
            Dades.SetupDades();

            string query = Funcions.GetQuery("GetTreeItem.sql");
            //query += $"WHERE T0.ItemCode = '{ItemCode}' ;";

            List<SqlParameter> prm = new List<SqlParameter>();
            SqlParameter param = null;

            param = new SqlParameter("@ItemCode", ItemCode);
            //param = new SqlParameter("@ItemCode", ItemCode.HasValue ? (object)ItemCode : DBNull.Value);
            prm.Add(param);

            DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, query, prm);

            return JsonConvert.SerializeObject(dt);
        }

        // POST: api/Expo/GetItems
        // Accepta llistes separades per comes/; línies i taules enganxades (Excel: 1a columna TAB)
        [HttpPost("GetItems")]
        public string GetItems([FromForm] string? ItemCodes)
        {
            // Mateixa configuració que GetItem (sense refactors)
            Dades.USER_SQL = "xavi";
            Dades.PWD_SQL = "$Domenjo$";
            Dades.DOMENJO_BBDD = "SBO_DOMENJO";
            Dades.USER_SAP = "admin1";
            Dades.PWD_SAP = "master";
            Dades.SetupDades();

            string raw = ItemCodes ?? string.Empty;

            // Parse robust (comes/; línies / taula excel)
            List<string> codes = ExtractItemCodesFromRaw(raw);

            if (codes.Count == 0)
            {
                // Mateix tipus de resposta que GetItem: JSON d'un DataTable (buit)
                return JsonConvert.SerializeObject(new DataTable());
            }

            string query = Funcions.GetQuery("GetItem.sql");

            DataTable? merged = null;

            foreach (string code in codes)
            {
                List<SqlParameter> prm = new List<SqlParameter>
        {
            new SqlParameter("@ItemCode", code),
        };

                DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, query, prm);

                if (merged == null)
                    merged = dt.Clone();

                foreach (DataRow r in dt.Rows)
                    merged.ImportRow(r);
            }

            return JsonConvert.SerializeObject(merged ?? new DataTable());
        }

        // ------------------------------------------------------------------
        // Helpers locals (mateixa validació conservadora que a la Intranet)
        // ------------------------------------------------------------------
        private static List<string> ExtractItemCodesFromRaw(string raw)
        {
            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outCodes = new List<string>();

            foreach (string line0 in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string line = line0.Trim();
                if (line.Length == 0) continue;

                string low = line.ToLowerInvariant();
                if (low.Contains("número de artículo") || low.Contains("numero de articulo") ||
                    low.Contains("descripción") || low.Contains("descripcion") ||
                    low.Contains("en stock"))
                {
                    continue; // ignorem capçaleres típiques
                }

                // Excel enganxat: 1a columna
                if (line.Contains('\t'))
                {
                    string first = line.Split('\t', 2)[0].Trim().Trim('\'', '"');
                    if (IsValidItemCode(first) && seen.Add(first)) outCodes.Add(first);
                    continue;
                }

                // múltiples codis per línia amb comes/;
                if (line.Contains(',') || line.Contains(';'))
                {
                    foreach (string part in line.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        string c = part.Trim().Trim('\'', '"');
                        if (IsValidItemCode(c) && seen.Add(c)) outCodes.Add(c);
                    }
                    continue;
                }

                // "CODI  descripció..." -> primer token
                string firstToken = line
                    .Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]
                    .Trim()
                    .Trim('\'', '"');

                if (IsValidItemCode(firstToken) && seen.Add(firstToken)) outCodes.Add(firstToken);
            }

            return outCodes;
        }

        private static bool IsValidItemCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            if (code.Length > 50) return false;

            // 1r caràcter alfanumèric
            if (!char.IsLetterOrDigit(code[0])) return false;

            for (int i = 0; i < code.Length; i++)
            {
                char ch = code[i];
                if (char.IsLetterOrDigit(ch)) continue;
                if (ch == '.' || ch == '_' || ch == '-') continue;
                return false;
            }

            return true;
        }

    }
}
