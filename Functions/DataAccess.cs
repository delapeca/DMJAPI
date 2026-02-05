using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using XNDmjApi.Models;

namespace XNDmjApi.Functions
{
    public class DataAccess
    {
        #region " Acceso a datos "

        // Helper únic: MAI fallback a SBO_DOMENJO.
        // Si no hi ha context DB => DB_CONTEXT_MISSING.
        private static string ResolveConnectionStringOrThrow(string connectionString)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            // Context obligatori
            if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                throw new Exception("DB_CONTEXT_MISSING");

            // Si ja tenim la DOMENJÓ muntada al context (login), l’usem.
            if (!string.IsNullOrWhiteSpace(Dades.ConnectionStringDOMENJO))
                return Dades.ConnectionStringDOMENJO;

            // Si hi ha DB però no hi ha connectionstring, intentem muntar-la (sense tocar la DB)
            Dades.SetupDades();

            if (!string.IsNullOrWhiteSpace(Dades.ConnectionStringDOMENJO))
                return Dades.ConnectionStringDOMENJO;

            throw new Exception("DB_CONTEXT_MISSING");
        }

        /// <summary>
        /// Ejecuta una consulta SQL en la base de datos, y devuelve los resultados obtenidos en un objeto DataTable.
        /// </summary>
        /// <param name="SQL"></param>
        /// <param name="parametros">Array de string con formato: nombre:valor</param>
        /// <returns>Devuelve un objeto DataTable con los resultados obtenidos tras ejecución de la consulta.</returns>
        public static DataTable GetDataTable(string connectionString, string SQL, string[] parametros)
        {
            connectionString = ResolveConnectionStringOrThrow(connectionString);

            using var conexion = new SqlConnection(connectionString);
            using var comando = new SqlCommand(SQL, conexion);

            for (int i = 0; i < parametros.Length; i++)
            {
                comando.Parameters.Add(new SqlParameter(parametros[i].Split(':')[0], parametros[i].Split(':')[1]));
            }

            using var da = new SqlDataAdapter(comando);
            var ds = new DataSet();
            da.Fill(ds);

            return ds.Tables[0];
        }

        /// <summary>
        /// Ejecuta una consulta SQL en la base de datos, y devuelve los resultados obtenidos en un string con formato JSON.
        /// </summary>
        /// <param name="SQL"></param>
        /// <param name="parametros">Array de string con formato: nombre:valor</param>
        /// <returns>Devuelve un strin con los resultados obtenidos tras ejecución de la consulta.</returns>
        public static string GetJSon(string connectionString, string SQL, string[] parametros)
        {
            connectionString = ResolveConnectionStringOrThrow(connectionString);

            using var conexion = new SqlConnection(connectionString);
            conexion.Open();

            using var comando = new SqlCommand(SQL, conexion);
            for (int i = 0; i < parametros.Length; i++)
                comando.Parameters.Add(new SqlParameter(parametros[i].Split(':')[0], parametros[i].Split(':')[1]));

            using var oReader = comando.ExecuteReader();

            if (oReader != null)
            {
                var dataTable = new DataTable();
                dataTable.Load(oReader);
                return JsonConvert.SerializeObject(dataTable);
            }

            return null; // mantenim el comportament original
        }

        /// <summary>
        /// Ejecuta una consulta SQL en la base de datos, y devuelve los resultados obtenidos en un objeto DataTable.
        /// Este método es vulnerable a inyección de dependencias, por lo que debe usarse sólamente de forma interna.
        /// Para consultas que vengan desde fuera, usar GetDataTable.
        /// </summary>
        public static DataTable GetTmpDataTable(string connectionString, string SQL, List<SqlParameter> Parameters)
        {
            connectionString = ResolveConnectionStringOrThrow(connectionString);

            using var conexion = new SqlConnection(connectionString);
            using var comando = new SqlCommand(SQL, conexion);

            comando.Parameters.AddRange(Parameters.ToArray());

            using var da = new SqlDataAdapter(comando);
            var ds = new DataSet();
            da.Fill(ds);

            return ds.Tables[0];
        }

        /// <summary>
        /// Ejecuta un procedimiento almacenado en la base de datos, y devuelve los resultados obtenidos en un objeto DataTable.
        /// </summary>
        public static DataTable ExecuteStoredProcedure(string connectionString, string procedimientoAlmacenado, SqlParameter[] parametros)
        {
            connectionString = ResolveConnectionStringOrThrow(connectionString);

            using var conexion = new SqlConnection(connectionString);
            using var comando = new SqlCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = procedimientoAlmacenado,
                Connection = conexion
            };

            if (parametros != null)
            {
                for (int i = 0; i < parametros.Length; i++)
                {
                    if (parametros[i].DbType == DbType.DateTime && parametros[i].Value != null)
                        parametros[i].Value = parametros[i].Value.ToString().Replace(" ", "T");

                    if (parametros[i].DbType == DbType.DateTime && parametros[i].SqlValue != null)
                        parametros[i].SqlValue = parametros[i].SqlValue.ToString().Replace(" ", "T");

                    comando.Parameters.Add(parametros[i]);
                }
            }

            var dt = new DataTable();
            conexion.Open();
            dt.Load(comando.ExecuteReader());
            return dt;
        }

        /// <summary>
        /// Ejecutar un comando SQL en la base de datos, sin devolución de resultados.
        /// </summary>
        public static string ExecuteQuery(string connectionString, string SQL, SqlParameter[] parametros)
        {
            try
            {
                connectionString = ResolveConnectionStringOrThrow(connectionString);

                using var con = new SqlConnection(connectionString);
                using var cmd = new SqlCommand(SQL, con);

                if (parametros != null)
                {
                    for (int i = 0; i < parametros.Length; i++)
                        cmd.Parameters.Add(parametros[i]);
                }

                con.Open();
                cmd.ExecuteNonQuery();
                return cmd.ToString();
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        #endregion

        #region " Funciones para convertir DataTables/DataReaders a JSON "

        public static string DataTableToJSON(DataTable tabla)
        {
            var JSONString = new StringBuilder();
            if (tabla.Rows.Count > 0)
            {
                JSONString.Append("[");
                for (int i = 0; i < tabla.Rows.Count; i++)
                {
                    JSONString.Append("{");
                    for (int j = 0; j < tabla.Columns.Count; j++)
                    {
                        if (tabla.Columns[j].DataType == System.Type.GetType("System.DateTime"))
                        {
                            if (tabla.Rows[i][j] is DBNull)
                                JSONString.Append("\"" + tabla.Columns[j].ColumnName.ToString() + "\":" + "\"" + DBNull.Value + "\"");
                            else
                                JSONString.Append("\"" + tabla.Columns[j].ColumnName.ToString() + "\":" + "\"" + Convert.ToDateTime(tabla.Rows[i][j]).ToString("yyyy-MM-dd HH:mm:ss") + "\"");
                        }
                        else if (tabla.Columns[j].DataType == System.Type.GetType("System.String"))
                        {
                            JSONString.Append("\"" + tabla.Columns[j].ColumnName.ToString() + "\":" + "\"" + DataTableToJson_CorreccionesJSONString(tabla.Rows[i][j].ToString()) + "\"");
                        }
                        else
                        {
                            JSONString.Append("\"" + tabla.Columns[j].ColumnName.ToString() + "\":" + "\"" + tabla.Rows[i][j].ToString() + "\"");
                        }

                        if (j < tabla.Columns.Count - 1) { JSONString.Append(","); }
                    }
                    JSONString.Append("}");
                    if (i < tabla.Rows.Count - 1) { JSONString.Append(","); }
                }
                JSONString.Append("]");
            }
            else
            {
                JSONString.Append("[]");
            }

            JSONString = JSONString.Replace("\\", "\\\\");
            return JSONString.ToString();
        }

        private static string DataTableToJson_CorreccionesJSONString(string json)
        {
            json = json.Replace("\"", "'");
            json = json.Replace("\t", " ");
            json = json.Replace("\r", "");
            json = json.Replace("\n", "");
            return json;
        }

        private static string DataReaderToJson(SqlDataReader dr)
        {
            var dt = new DataTable();
            dt.Load(dr);
            return DataTableToJSON(dt);
        }

        public static string StoredProcedureToJson(string procedimientoAlmacenado, SqlParameter[] parametros, SqlDataReader dr)
        {
            var json = new StringBuilder();

            int numParametrosEntrada = 0;
            int numParametrosSalida = 0;

            json.Append("[{");
            json.Append("\"SP\": \"" + procedimientoAlmacenado + "\"");
            json.Append(",");
            json.Append("\"ParametrosEntrada\":{");
            for (int i = 0; i < parametros.Length; i++)
            {
                if (parametros[i].Direction == ParameterDirection.Input)
                {
                    if (numParametrosEntrada > 0) json.Append(",");
                    json.Append("\"" + parametros[i].ParameterName + "\":\"" + parametros[i].Value + "\"");
                    numParametrosEntrada += 1;
                }
            }
            if (numParametrosEntrada == 0) { json.Append("\"Sin parámetros de entrada\": \"Fin\""); }
            json.Append("},");
            json.Append("\"ParametrosSalida\":{");
            for (int i = 0; i < parametros.Length; i++)
            {
                if (parametros[i].Direction == ParameterDirection.Output)
                {
                    if (numParametrosSalida > 0) json.Append(",");
                    json.Append("\"" + parametros[i].ParameterName + "\":\"" + parametros[i].Value + "\"");
                    numParametrosSalida += 1;
                }
            }
            if (numParametrosSalida == 0) { json.Append("\"Sin parámetros de salida\": \"Fin\""); }
            json.Append("},");
            json.Append("\"Data\":" + DataReaderToJson(dr));
            json.Append("}]");

            json = json.Replace("\\", "\\\\");
            return json.ToString();
        }

        #endregion

        #region " Acceso a datos con resultados en formato JSON "

        public static async Task<string> JsonDataReader(string connectionString, string SQL)
        {
            connectionString = ResolveConnectionStringOrThrow(connectionString);

            using var con = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(SQL, con);

            await con.OpenAsync();
            using var dr = await cmd.ExecuteReaderAsync();

            var json = DataReaderToJson(dr);
            return json;
        }

        public static async Task<string> JsonStoredProcedure(string connectionString, string procedimientoAlmacenado, SqlParameter[] parametros)
        {
            connectionString = ResolveConnectionStringOrThrow(connectionString);

            using var con = new SqlConnection(connectionString);
            using var cmd = new SqlCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = procedimientoAlmacenado,
                Connection = con
            };

            if (parametros != null)
            {
                for (int i = 0; i < parametros.Length; i++)
                {
                    if (parametros[i].DbType == DbType.DateTime && parametros[i].Value != null)
                        parametros[i].Value = parametros[i].Value.ToString().Replace(" ", "T");

                    if (parametros[i].DbType == DbType.DateTime && parametros[i].SqlValue != null)
                        parametros[i].SqlValue = parametros[i].SqlValue.ToString().Replace(" ", "T");

                    cmd.Parameters.Add(parametros[i]);
                }
            }

            await con.OpenAsync();
            using var dr = await cmd.ExecuteReaderAsync();

            var json = StoredProcedureToJson(procedimientoAlmacenado, parametros, dr);
            return json;
        }

        #endregion

        /// <summary>
        /// IMPORTANT: aquest mètode NO pot “fixar” la DB. La DB ve del context SAP (login).
        /// Aquí només validem coherència i garantim que hi ha ConnectionString muntada.
        /// </summary>
        public static void EnsureConnection(string BBDD)
        {
            if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                throw new Exception("DB_CONTEXT_MISSING");

            if (!string.IsNullOrWhiteSpace(BBDD) &&
                !string.Equals(Dades.DOMENJO_BBDD.Trim(), BBDD.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("DB_CONTEXT_MISMATCH");
            }

            if (string.IsNullOrWhiteSpace(Dades.ConnectionStringDOMENJO))
            {
                // NO toquem DOMENJO_BBDD aquí. Només intentem muntar connstring a partir del context existent.
                Dades.SetupDades();

                if (string.IsNullOrWhiteSpace(Dades.ConnectionStringDOMENJO))
                    throw new Exception("DB_CONTEXT_MISSING");
            }
        }
    }
}
