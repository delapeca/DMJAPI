using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;
using XNDmjApi.Functions;
using XNDmjApi.Models.DbSetup;

namespace XNDmjApi.Services
{
    public sealed class SqlSchemaSetupService
    {
        // Noms segurs: lletres/números/underscore. (evitem punts, espais, guions, etc.)
        private static readonly Regex RxIdent = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        // Allowlist de tipus SQL per evitar injeccions via schema.
        // (Amplia-la quan ho necessitis, però sempre amb regex controlada.)
        private static readonly Regex RxSqlType = new Regex(
            @"^(int|bigint|bit|datetime2|datetime|date|uniqueidentifier|float|real|money|smallmoney|" +
            @"nvarchar\((max|[1-9][0-9]{0,3})\)|varchar\((max|[1-9][0-9]{0,3})\)|" +
            @"varbinary\((max|[1-9][0-9]{0,4})\)|" +
            @"decimal\(([1-9][0-9]?),([0-9]|[1-9][0-9]?)\))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static string SafeSchema(string? schema)
        {
            if (string.IsNullOrWhiteSpace(schema)) return "dbo";
            schema = schema.Trim();
            if (!RxIdent.IsMatch(schema)) throw new Exception($"Schema invàlid: '{schema}'");
            return schema;
        }

        private static string SafeIdent(string value, string kind)
        {
            value = (value ?? "").Trim();
            if (!RxIdent.IsMatch(value)) throw new Exception($"{kind} invàlid: '{value}'");
            return value;
        }

        private static string SafeSqlType(string sqlType)
        {
            sqlType = (sqlType ?? "").Trim();
            if (!RxSqlType.IsMatch(sqlType)) throw new Exception($"SqlType no permès: '{sqlType}'");
            return sqlType;
        }

        public EnsureSchemaResponse EnsureSchema(DbSchema schema)
        {
            if (schema == null) throw new Exception("Schema null.");
            if (schema.Tables == null || schema.Tables.Count == 0) throw new Exception("Schema.Tables buit.");

            // Assegurem que tenim connection string muntada
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                Dades.SetupDades();
            }

            var resp = new EnsureSchemaResponse { Ok = true, Code = "OK", Message = "Schema garantit." };

            using var con = new SqlConnection(Dades.ConnectionStringDOMENJO);
            con.Open();

            foreach (var t in schema.Tables)
            {
                var tableName = SafeIdent(t.Name, "TableName");
                var schemaName = SafeSchema(t.Schema);
                var fullName = $"[{schemaName}].[{tableName}]";

                if (t.Columns == null || t.Columns.Count == 0)
                    throw new Exception($"Taula '{tableName}': Columns buit.");

                // 1) Create table if not exists (amb columnes base)
                if (!TableExists(con, schemaName, tableName))
                {
                    var colsSql = new List<string>();
                    foreach (var c in t.Columns)
                    {
                        colsSql.Add(BuildColumnSql(c));
                    }

                    var createSql = $@"
IF OBJECT_ID(N'{schemaName}.{tableName}', N'U') IS NULL
BEGIN
    CREATE TABLE {fullName} (
        {string.Join("," + Environment.NewLine + "        ", colsSql)}
    );
END
";
                    ExecNonQuery(con, createSql);
                    resp.CreatedTables.Add($"{schemaName}.{tableName}");
                }
                else
                {
                    // 2) Add missing columns
                    foreach (var c in t.Columns)
                    {
                        var colName = SafeIdent(c.Name, "ColumnName");
                        if (!ColumnExists(con, schemaName, tableName, colName))
                        {
                            var alter = $"ALTER TABLE {fullName} ADD {BuildColumnSql(c)};";
                            ExecNonQuery(con, alter);
                            resp.AddedColumns.Add($"{schemaName}.{tableName}.{colName}");
                        }
                    }
                }

                // 3) PK (opcional) - només si no existeix cap PK
                if (t.PrimaryKey != null && t.PrimaryKey.Count > 0)
                {
                    if (!HasPrimaryKey(con, schemaName, tableName))
                    {
                        var pkCols = t.PrimaryKey.Select(x => $"[{SafeIdent(x, "PK column")}]" ).ToList();
                        var pkName = $"PK_{tableName}";
                        var pkSql = $"ALTER TABLE {fullName} ADD CONSTRAINT [{pkName}] PRIMARY KEY ({string.Join(",", pkCols)});";
                        ExecNonQuery(con, pkSql);
                        resp.Notes.Add($"PK creada: {schemaName}.{tableName} ({string.Join(",", t.PrimaryKey)})");
                    }
                }

                // 4) Indexes (opcional)
                if (t.Indexes != null)
                {
                    foreach (var ix in t.Indexes)
                    {
                        var ixName = SafeIdent(ix.Name, "IndexName");
                        if (ix.Columns == null || ix.Columns.Count == 0)
                            throw new Exception($"Index '{ixName}' sense columns.");

                        if (!IndexExists(con, schemaName, tableName, ixName))
                        {
                            var cols = ix.Columns.Select(x => $"[{SafeIdent(x, "IX column")}]" ).ToList();
                            var uniq = ix.Unique ? "UNIQUE " : "";
                            var ixSql = $"CREATE {uniq}INDEX [{ixName}] ON {fullName} ({string.Join(",", cols)});";
                            ExecNonQuery(con, ixSql);
                            resp.CreatedIndexes.Add($"{schemaName}.{tableName}.{ixName}");
                        }
                    }
                }
            }

            return resp;
        }

        private static string BuildColumnSql(DbColumnSpec c)
        {
            var colName = SafeIdent(c.Name, "ColumnName");
            var sqlType = SafeSqlType(c.SqlType);

            var identity = c.Identity ? " IDENTITY(1,1)" : "";
            var nullable = c.Nullable ? " NULL" : " NOT NULL";

            // Default: permetem expressions simples, però les validem mínimament (evitem ';' i comentaris)
            string def = "";
            if (!string.IsNullOrWhiteSpace(c.Default))
            {
                var d = c.Default.Trim();
                if (d.Contains(";") || d.Contains("--") || d.Contains("/*") || d.Contains("*/"))
                    throw new Exception($"Default insegur a columna {colName}.");
                def = $" CONSTRAINT [DF_{colName}] DEFAULT ({d})";
            }

            return $"[{colName}] {sqlType}{identity}{def}{nullable}";
        }

        private static void ExecNonQuery(SqlConnection con, string sql)
        {
            using var cmd = new SqlCommand(sql, con);
            cmd.CommandType = CommandType.Text;
            cmd.ExecuteNonQuery();
        }

        private static bool TableExists(SqlConnection con, string schema, string table)
        {
            const string q = @"
SELECT 1
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @schema AND t.name = @table;
";
            using var cmd = new SqlCommand(q, con);
            cmd.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
            cmd.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = table });
            var r = cmd.ExecuteScalar();
            return r != null;
        }

        private static bool ColumnExists(SqlConnection con, string schema, string table, string column)
        {
            const string q = @"
SELECT 1
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @schema AND t.name = @table AND c.name = @column;
";
            using var cmd = new SqlCommand(q, con);
            cmd.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
            cmd.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = table });
            cmd.Parameters.Add(new SqlParameter("@column", SqlDbType.NVarChar, 128) { Value = column });
            var r = cmd.ExecuteScalar();
            return r != null;
        }

        private static bool HasPrimaryKey(SqlConnection con, string schema, string table)
        {
            const string q = @"
SELECT 1
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name=@schema AND t.name=@table AND i.is_primary_key=1;
";
            using var cmd = new SqlCommand(q, con);
            cmd.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
            cmd.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = table });
            var r = cmd.ExecuteScalar();
            return r != null;
        }

        private static bool IndexExists(SqlConnection con, string schema, string table, string indexName)
        {
            const string q = @"
SELECT 1
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name=@schema AND t.name=@table AND i.name=@index;
";
            using var cmd = new SqlCommand(q, con);
            cmd.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
            cmd.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = table });
            cmd.Parameters.Add(new SqlParameter("@index", SqlDbType.NVarChar, 128) { Value = indexName });
            var r = cmd.ExecuteScalar();
            return r != null;
        }
    }
}
