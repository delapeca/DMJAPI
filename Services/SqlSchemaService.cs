using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using XNDmjApi.Functions;
using XNDmjApi.Models.DbSetup;

namespace XNDmjApi.Services
{
    public sealed class SqlSchemaService
    {
        // Allowlist de noms (evitem “coses rares” en noms de taula/camp/udo)
        private static readonly Regex SafeName = new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        public sealed class EnsureResult
        {
            public bool Ok { get; set; }
            public string Code { get; set; } = "OK";
            public string Message { get; set; } = "";
            public List<string> Actions { get; set; } = new();
        }

        /// <summary>
        /// IMPORTANT: Aquest Ensure crea UDT/UDO via SAP DI-API (requisit SAP) perquè a SQL quedin com @...
        /// </summary>
        public EnsureResult Ensure(DbSchema schema)
        {
            if (schema == null || schema.Tables == null || schema.Tables.Count == 0)
                return new EnsureResult { Ok = false, Code = "MISSING_SCHEMA", Message = "schemaJson sense taules." };

            // Context DB obligatori (sense defaults perillosos)
            if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
                return new EnsureResult
                {
                    Ok = false,
                    Code = "DB_CONTEXT_MISSING",
                    Message = "No hi ha DOMENJO_BBDD al context. Fes login SAP abans i torna-ho a provar."
                };

            // Necessitem Company connectada per DI-API
            if (Dades.oCompany == null || !Dades.oCompany.Connected)
                return new EnsureResult
                {
                    Ok = false,
                    Code = "SAP_CONTEXT_MISSING",
                    Message = "No hi ha Company SAP connectada (DI-API). Fes login SAP abans i torna-ho a provar."
                };

            // Extra guard: assegurem que la Company connectada és la DB del context
            try
            {
                var companyDb = (Dades.oCompany.CompanyDB ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(companyDb) &&
                    !string.Equals(companyDb, Dades.DOMENJO_BBDD.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return new EnsureResult
                    {
                        Ok = false,
                        Code = "DB_CONTEXT_MISMATCH",
                        Message = $"La Company connectada ({companyDb}) no coincideix amb DOMENJO_BBDD ({Dades.DOMENJO_BBDD})."
                    };
                }
            }
            catch
            {
                // ignore
            }

            var res = new EnsureResult { Ok = true, Code = "OK", Message = "UDT/UDO assegurats." };

            // Normalitzem taules per nom (sense schema)
            var tablesByName = schema.Tables
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name))
                .ToDictionary(t => t.Name.Trim(), t => t, StringComparer.OrdinalIgnoreCase);

            var allTableNames = tablesByName.Keys.ToList();

            // UDOs declarats al JSON (schema.udos)
            var udoSpecs = (schema.Udos ?? new List<DbUdoSpec>())
                .Where(u => u != null
                    && !string.IsNullOrWhiteSpace(u.Code)
                    && !string.IsNullOrWhiteSpace(u.HeaderTable))
                .ToList();

            // Mapa: taula -> tipus UDT requerit per UDO (header/lines)
            var udtTypeByTable = new Dictionary<string, BoUTBTableType>(StringComparer.OrdinalIgnoreCase);

            foreach (var u in udoSpecs)
            {
                var header = (u.HeaderTable ?? "").Trim();
                if (string.IsNullOrWhiteSpace(header)) continue;

                ValidateName(header, "udo header table");
                udtTypeByTable[header] = BoUTBTableType.bott_MasterData;

                foreach (var ct in (u.ChildTables ?? new List<string>()))
                {
                    var child = (ct ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(child)) continue;

                    ValidateName(child, "udo child table");
                    udtTypeByTable[child] = BoUTBTableType.bott_MasterDataLines;
                }
            }

            // 1) Crear/assegurar UDT per cada taula
            foreach (var tableName in allTableNames)
            {
                ValidateName(tableName, "table");

                // IMPORTANT SAP:
                // - Si la taula forma part d'un UDO: header = MasterData, children = MasterDataLines
                // - La resta: NoObject (no inventem UDOs)
                BoUTBTableType tt = BoUTBTableType.bott_NoObject;
                if (udtTypeByTable.TryGetValue(tableName, out var forcedType))
                    tt = forcedType;

                EnsureUserTable(tableName, $"DOMENJÓ {tableName}", tt, res);

                // 2) Crear/assegurar UDFs segons columns
                var t = tablesByName[tableName];
                var cols = (t.Columns ?? new List<DbColumnSpec>());

                foreach (var c in cols)
                {
                    if (c == null) continue;
                    var colName = (c.Name ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(colName)) continue;

                    // En UDT/UDO no creem "Id identity" com a columna real
                    if (string.Equals(colName, "Id", StringComparison.OrdinalIgnoreCase) && c.Identity)
                        continue;

                    ValidateName(colName, "column");
                    EnsureUserFieldForUdt(tableName, c, res);
                }
            }

            // 3) Crear/assegurar UDOs declarats
            if (udoSpecs.Count == 0)
            {
                res.Actions.Add("! Nota: No s'ha creat cap UDO perquè el schema no inclou 'udos'.");
            }
            else
            {
                foreach (var u in udoSpecs)
                {
                    var code = (u.Code ?? "").Trim();
                    var header = (u.HeaderTable ?? "").Trim();

                    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(header))
                        continue;

                    ValidateName(code, "udo code");
                    ValidateName(header, "udo header table");

                    // Guard: que les taules existeixin al schema (evitem sorpreses)
                    if (!tablesByName.ContainsKey(header))
                        throw new Exception($"UDO '{code}': headerTable '{header}' no existeix a schema.tables.");

                    foreach (var ct in (u.ChildTables ?? new List<string>()))
                    {
                        var child = (ct ?? "").Trim();
                        if (string.IsNullOrWhiteSpace(child)) continue;
                        if (!tablesByName.ContainsKey(child))
                            throw new Exception($"UDO '{code}': childTable '{child}' no existeix a schema.tables.");
                    }

                    var udoName = (u.Name ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(udoName))
                        udoName = $"DOMENJÓ {code}";

                    EnsureUdoGeneric(code, udoName, header, u.ChildTables ?? new List<string>(), res);
                }
            }

            res.Message = "UDT/UDO creats/actualitzats (via DI-API).";
            return res;
        }

        private static void EnsureUserTable(string tableName, string description, BoUTBTableType tableType, EnsureResult res)
        {
            UserTablesMD? ut = null;
            try
            {
                ut = (UserTablesMD)Dades.oCompany.GetBusinessObject(BoObjectTypes.oUserTables);

                if (ut.GetByKey(tableName))
                    return;

                ut.TableName = tableName; // sense '@'
                ut.TableDescription = string.IsNullOrWhiteSpace(description) ? tableName : description;
                ut.TableType = tableType;

                var rc = ut.Add();
                if (rc != 0)
                    throw new Exception($"SAP Add UDT {tableName} rc={rc}: {Dades.oCompany.GetLastErrorDescription()}");

                res.Actions.Add($"+ CREATE UDT @{tableName} ({tableType})");
            }
            finally
            {
                TryReleaseCom(ut);
            }
        }

        private static void EnsureUserFieldForUdt(string udtName, DbColumnSpec c, EnsureResult res)
        {
            UserFieldsMD? uf = null;
            try
            {
                var fieldName = (c.Name ?? "").Trim();
                var sqlType = (c.SqlType ?? "").Trim();

                var sapTableName = "@" + udtName;

                if (UserFieldExists(sapTableName, "U_" + fieldName))
                    return;

                uf = (UserFieldsMD)Dades.oCompany.GetBusinessObject(BoObjectTypes.oUserFields);
                uf.TableName = sapTableName;
                uf.Name = fieldName;

                uf.Mandatory = c.Nullable ? BoYesNoEnum.tNO : BoYesNoEnum.tYES;

                ApplyTypeMapping(sqlType, uf);

                var rc = uf.Add();
                if (rc != 0)
                    throw new Exception($"SAP Add UDF {sapTableName}.U_{fieldName} rc={rc}: {Dades.oCompany.GetLastErrorDescription()}");

                res.Actions.Add($"+ ADD UDF {sapTableName}.U_{fieldName} ({DescribeSapField(uf)})");
            }
            finally
            {
                TryReleaseCom(uf);
            }
        }

        private static void EnsureUdoGeneric(string udoCode, string udoName, string headerTable, IEnumerable<string> childTables, EnsureResult res)
        {
            UserObjectsMD? uo = null;
            try
            {
                if (UdoExists(udoCode))
                    return;

                uo = (UserObjectsMD)Dades.oCompany.GetBusinessObject(BoObjectTypes.oUserObjectsMD);

                uo.Code = udoCode;
                uo.Name = udoName;
                uo.ObjectType = BoUDOObjType.boud_MasterData;
                uo.TableName = headerTable;

                uo.CanCancel = BoYesNoEnum.tNO;
                uo.CanClose = BoYesNoEnum.tNO;
                uo.CanCreateDefaultForm = BoYesNoEnum.tNO;
                uo.CanDelete = BoYesNoEnum.tNO;
                uo.CanFind = BoYesNoEnum.tYES;
                uo.CanLog = BoYesNoEnum.tYES;
                uo.ManageSeries = BoYesNoEnum.tNO;

                foreach (var ct in childTables)
                {
                    var child = (ct ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(child)) continue;
                    ValidateName(child, "child table");

                    uo.ChildTables.TableName = child;
                    uo.ChildTables.Add();
                }

                var rc = uo.Add();
                if (rc != 0)
                    throw new Exception($"SAP Add UDO {udoCode} rc={rc}: {Dades.oCompany.GetLastErrorDescription()}");

                res.Actions.Add($"+ CREATE UDO {udoCode} (Table {headerTable}, Child: {string.Join(",", childTables)})");
            }
            finally
            {
                TryReleaseCom(uo);
            }
        }

        private static bool UdoExists(string udoCode)
        {
            Recordset? rs = null;
            try
            {
                rs = (Recordset)Dades.oCompany.GetBusinessObject(BoObjectTypes.BoRecordset);
                rs.DoQuery($"SELECT Code FROM OUDO WHERE Code = '{udoCode.Replace("'", "''")}'");
                return !rs.EoF;
            }
            finally
            {
                TryReleaseCom(rs);
            }
        }

        private static bool UserFieldExists(string tableName, string fullFieldName)
        {
            Recordset? rs = null;
            try
            {
                rs = (Recordset)Dades.oCompany.GetBusinessObject(BoObjectTypes.BoRecordset);

                var t = tableName.Replace("'", "''");
                var aliasId = fullFieldName.StartsWith("U_", StringComparison.OrdinalIgnoreCase)
                    ? fullFieldName.Substring(2)
                    : fullFieldName;

                var f = aliasId.Replace("'", "''");

                rs.DoQuery($"SELECT 1 FROM CUFD WHERE TableID = '{t}' AND AliasID = '{f}'");
                return !rs.EoF;
            }
            finally
            {
                TryReleaseCom(rs);
            }
        }

        private static void ApplyTypeMapping(string sqlType, UserFieldsMD uf)
        {
            var t = (sqlType ?? "").Trim().ToLowerInvariant();

            if (t.StartsWith("nvarchar(") && t.Contains("max"))
            {
                uf.Type = BoFieldTypes.db_Memo;
                return;
            }

            if (t.StartsWith("nvarchar("))
            {
                var n = ExtractSize(t);
                if (n <= 0) n = 50;

                if (n > 254)
                {
                    uf.Type = BoFieldTypes.db_Memo;
                }
                else
                {
                    uf.Type = BoFieldTypes.db_Alpha;
                    uf.Size = n;
                }
                return;
            }

            if (t == "int" || t == "bigint")
            {
                uf.Type = BoFieldTypes.db_Numeric;
                uf.SubType = BoFldSubTypes.st_None;
                uf.EditSize = 11;
                return;
            }

            if (t == "bit")
            {
                uf.Type = BoFieldTypes.db_Alpha;
                uf.Size = 1;

                try
                {
                    uf.ValidValues.Value = "0";
                    uf.ValidValues.Description = "0";
                    uf.ValidValues.Add();

                    uf.ValidValues.Value = "1";
                    uf.ValidValues.Description = "1";
                    uf.ValidValues.Add();
                }
                catch { }

                return;
            }

            if (t == "datetime2" || t == "datetime")
            {
                uf.Type = BoFieldTypes.db_Alpha;
                uf.Size = 25;
                return;
            }

            throw new Exception($"SqlType no suportat per UDT/UDO mapping: '{sqlType}'.");
        }

        private static int ExtractSize(string t)
        {
            var i1 = t.IndexOf('(');
            var i2 = t.IndexOf(')');
            if (i1 < 0 || i2 <= i1) return -1;
            var inner = t.Substring(i1 + 1, i2 - i1 - 1).Trim();
            if (int.TryParse(inner, out var n)) return n;
            return -1;
        }

        private static string DescribeSapField(UserFieldsMD uf)
        {
            try
            {
                if (uf.Type == BoFieldTypes.db_Alpha) return $"Alpha({uf.Size})";
                if (uf.Type == BoFieldTypes.db_Memo) return "Memo";
                if (uf.Type == BoFieldTypes.db_Numeric) return $"Numeric(EditSize={uf.EditSize})";
                if (uf.Type == BoFieldTypes.db_Date) return "Date";
                return uf.Type.ToString();
            }
            catch
            {
                return "Field";
            }
        }

        private static void ValidateName(string s, string what)
        {
            if (string.IsNullOrWhiteSpace(s) || !SafeName.IsMatch(s))
                throw new Exception($"Nom no segur per {what}: '{s}'. Només [A-Za-z0-9_].");
        }

        private static void TryReleaseCom(object? o)
        {
            try
            {
                if (o != null && Marshal.IsComObject(o))
                    Marshal.ReleaseComObject(o);
            }
            catch { }
        }
    }
}
