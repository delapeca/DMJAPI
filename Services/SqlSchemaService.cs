using Newtonsoft.Json;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using XNDmjApi.Functions;
using XNDmjApi.Models;

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
        /// IMPORTANT: Aquest Ensure JA NO crea taules SQL “normals”.
        /// Ara crea UDT/UDO via SAP DI-API (requisit SAP) perquè a SQL quedin com @...
        /// </summary>
        public EnsureResult Ensure(SqlSchemaSpec spec)
        {
            if (spec == null || spec.Tables == null || spec.Tables.Count == 0)
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
            var tablesByName = spec.Tables
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name))
                .ToDictionary(t => t.Name.Trim(), t => t, StringComparer.OrdinalIgnoreCase);

            var allTableNames = tablesByName.Keys.ToList();

            // Detectem estructura Change-Requests (header + 2 child)
            var hasCrqUdo =
                tablesByName.ContainsKey("XN_CRQ") &&
                tablesByName.ContainsKey("XN_CRQ1") &&
                tablesByName.ContainsKey("XN_CRQ_EVT");

            var crqHeader = "XN_CRQ";
            var crqChildren = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "XN_CRQ1", "XN_CRQ_EVT" };

            // 1) Crear/assegurar UDT per cada taula
            foreach (var tableName in allTableNames)
            {
                ValidateName(tableName, "table");

                // IMPORTANT SAP:
                // - Si anem a crear UDO CRQ: header = MasterData, children = MasterDataLines
                // - La resta: NoObject (no inventem UDOs)
                BoUTBTableType tt = BoUTBTableType.bott_NoObject;

                if (hasCrqUdo)
                {
                    if (tableName.Equals(crqHeader, StringComparison.OrdinalIgnoreCase))
                        tt = BoUTBTableType.bott_MasterData;
                    else if (crqChildren.Contains(tableName))
                        tt = BoUTBTableType.bott_MasterDataLines;
                }

                EnsureUserTable(tableName, $"DOMENJÓ {tableName}", tt, res);

                // 2) Crear/assegurar UDFs segons columns
                var t = tablesByName[tableName];
                var cols = (t.Columns ?? new List<SqlColumnSpec>());

                foreach (var c in cols)
                {
                    if (c == null) continue;
                    var colName = (c.Name ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(colName)) continue;

                    // En UDT/UDO NO creem el teu "Id identity" com a columna real:
                    // - La clau en UDT és Code (i per child, Code + LineId)
                    // - Si al JSON t’arriba "Id" primaryKey/identity, l’ignorarem.
                    if (string.Equals(colName, "Id", StringComparison.OrdinalIgnoreCase) && (c.Identity || c.PrimaryKey))
                        continue;

                    // UDF name a DI-API va sense "U_" (SAP li posa U_ a SQL)
                    ValidateName(colName, "column");

                    EnsureUserFieldForUdt(tableName, c, res);
                }
            }

            // 3) Crear/assegurar UDO si detectem XN_CRQ (header + 2 child)
            if (hasCrqUdo)
            {
                EnsureUdoChangeRequests(crqHeader, new[] { "XN_CRQ1", "XN_CRQ_EVT" }, res);
            }
            else
            {
                res.Actions.Add("! Nota: No s'ha creat UDO perquè no s'han trobat totes les taules XN_CRQ, XN_CRQ1 i XN_CRQ_EVT al schema.");
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

                // Existeix?
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

        private static void EnsureUserFieldForUdt(string udtName, SqlColumnSpec c, EnsureResult res)
        {
            UserFieldsMD? uf = null;
            try
            {
                var fieldName = (c.Name ?? "").Trim();
                var sqlType = (c.Type ?? "").Trim();

                // TableName per UDT fields habitualment és "@TABLE"
                var sapTableName = "@" + udtName;

                // Existeix?
                if (UserFieldExists(sapTableName, "U_" + fieldName))
                    return;

                uf = (UserFieldsMD)Dades.oCompany.GetBusinessObject(BoObjectTypes.oUserFields);
                uf.TableName = sapTableName;
                uf.Name = fieldName;

                // Nullable => Mandatory NO
                uf.Mandatory = c.Nullable ? BoYesNoEnum.tNO : BoYesNoEnum.tYES;

                // Map tipus (subset robust per Change-Requests)
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

        private static void EnsureUdoChangeRequests(string headerTable, IEnumerable<string> childTables, EnsureResult res)
        {
            UserObjectsMD? uo = null;
            try
            {
                var udoCode = headerTable;

                // Existeix?
                if (UdoExists(udoCode))
                    return;

                uo = (UserObjectsMD)Dades.oCompany.GetBusinessObject(BoObjectTypes.oUserObjectsMD);

                uo.Code = udoCode;
                uo.Name = "DOMENJÓ Change Requests";
                uo.ObjectType = BoUDOObjType.boud_MasterData;
                uo.TableName = headerTable; // sense '@'

                // Features mínimes
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

                    uo.ChildTables.TableName = child; // sense '@'
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

            // nvarchar(max) -> Memo
            if (t.StartsWith("nvarchar(") && t.Contains("max"))
            {
                uf.Type = BoFieldTypes.db_Memo;
                return;
            }

            // nvarchar(n)
            if (t.StartsWith("nvarchar("))
            {
                var n = ExtractSize(t);
                if (n <= 0) n = 50;

                // SAP Alpha max habitual 254; si excedeix -> Memo
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

            // int/bigint -> Numeric
            if (t == "int" || t == "bigint")
            {
                uf.Type = BoFieldTypes.db_Numeric;
                uf.SubType = BoFldSubTypes.st_None;

                // IMPORTANT (DI-API): per Numeric s'usa EditSize (1..11), NO Size
                uf.EditSize = 11; // suficient per int i per IDs típics

                return;
            }


            // bit -> Alpha(1) amb valors 0/1 (evitem enums que no existeixen al teu SAPbobsCOM)
            if (t == "bit")
            {
                uf.Type = BoFieldTypes.db_Alpha;
                uf.Size = 1;

                // Valid values: "0" / "1"
                try
                {
                    uf.ValidValues.Value = "0";
                    uf.ValidValues.Description = "0";
                    uf.ValidValues.Add();

                    uf.ValidValues.Value = "1";
                    uf.ValidValues.Description = "1";
                    uf.ValidValues.Add();
                }
                catch
                {
                    // si algun entorn no permet ValidValues en aquest tipus, no trenquem la creació del camp
                }

                return;
            }

            // datetime2/datetime -> guardem ISO (Alpha) per mantenir hora
            if (t == "datetime2" || t == "datetime")
            {
                uf.Type = BoFieldTypes.db_Alpha;
                uf.Size = 25; // ex: 2026-01-28T12:34:56Z
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
                if (uf.Type == BoFieldTypes.db_Numeric) return $"Numeric({uf.Size})";
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
            catch { /* ignore */ }
        }
    }
}
