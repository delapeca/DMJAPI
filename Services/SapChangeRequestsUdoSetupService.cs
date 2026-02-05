using SAPbobsCOM;
using System.Runtime.InteropServices;
using XNDmjApi.Functions;

namespace XNDmjApi.Services
{
    public class SapChangeRequestsUdoSetupService
    {
        // UDT/UDO names (sense @ per DI-API)
        private const string HTable = "DMJ_CRQ";
        private const string LTable = "DMJ_CRQ1";
        private const string UdoCode = "DMJ_CRQ";
        private const string UdoName = "DMJ Change Requests";

        public (bool ok, string code, string message) EnsureCreated()
        {
            var company = Dades.oCompany;
            if (company == null || !company.Connected)
                return (false, "SAP_NOT_CONNECTED", "No hi ha connexió DI-API (Dades.oCompany). Fes login SAP abans (Intranet) i torna-ho a provar.");

            try
            {
                // 1) UDTs
                EnsureTable(company, HTable, "DMJ Change Requests", BoUTBTableType.bott_Document);
                EnsureTable(company, LTable, "DMJ Change Requests Lines", BoUTBTableType.bott_DocumentLines);

                // 2) UDFs capçalera (@DMJ_CRQ)
                EnsureField(company, HTable, "CardCode", "CardCode", BoFieldTypes.db_Alpha, 15);
                EnsureField(company, HTable, "Kind", "Kind", BoFieldTypes.db_Alpha, 20);
                EnsureField(company, HTable, "Action", "Action", BoFieldTypes.db_Alpha, 20);
                EnsureField(company, HTable, "Status", "Status", BoFieldTypes.db_Alpha, 20);
                EnsureField(company, HTable, "Reason", "Reason", BoFieldTypes.db_Memo, 0);
                EnsureField(company, HTable, "RequestedByUserId", "RequestedByUserId", BoFieldTypes.db_Numeric, 11);
                EnsureField(company, HTable, "RequestedByEmail", "RequestedByEmail", BoFieldTypes.db_Alpha, 250);
                EnsureField(company, HTable, "RequestRef", "RequestRef", BoFieldTypes.db_Alpha, 50);
                EnsureField(company, HTable, "TargetId", "TargetId", BoFieldTypes.db_Numeric, 11);
                EnsureField(company, HTable, "TargetName", "TargetName", BoFieldTypes.db_Alpha, 100);
                EnsureField(company, HTable, "TargetJson", "TargetJson", BoFieldTypes.db_Memo, 0);
                EnsureField(company, HTable, "DecisionBy", "DecisionBy", BoFieldTypes.db_Alpha, 50);
                EnsureField(company, HTable, "DecisionAt", "DecisionAt", BoFieldTypes.db_Alpha, 20); // yyyyMMddHHmmss
                EnsureField(company, HTable, "DecisionNote", "DecisionNote", BoFieldTypes.db_Memo, 0);
                EnsureField(company, HTable, "ApplyBy", "ApplyBy", BoFieldTypes.db_Alpha, 50);
                EnsureField(company, HTable, "ApplyAt", "ApplyAt", BoFieldTypes.db_Alpha, 20); // yyyyMMddHHmmss
                EnsureField(company, HTable, "ApplyResult", "ApplyResult", BoFieldTypes.db_Memo, 0);

                // 3) UDFs línies (@DMJ_CRQ1)
                EnsureField(company, LTable, "Entity", "Entity", BoFieldTypes.db_Alpha, 20);
                EnsureField(company, LTable, "Action", "Action", BoFieldTypes.db_Alpha, 10);
                EnsureField(company, LTable, "TargetId", "TargetId", BoFieldTypes.db_Numeric, 11);
                EnsureField(company, LTable, "Field", "Field", BoFieldTypes.db_Alpha, 50);
                EnsureField(company, LTable, "OldValue", "OldValue", BoFieldTypes.db_Memo, 0);
                EnsureField(company, LTable, "NewValue", "NewValue", BoFieldTypes.db_Memo, 0);
                EnsureField(company, LTable, "IsSensitive", "IsSensitive", BoFieldTypes.db_Alpha, 1);
                EnsureField(company, LTable, "LineStatus", "LineStatus", BoFieldTypes.db_Alpha, 20);
                EnsureField(company, LTable, "LineError", "LineError", BoFieldTypes.db_Memo, 0);

                // 4) UDO Document
                EnsureUdo(company);

                return (true, "OK", "UDO DMJ_CRQ creat/ja existent (taules + camps + objecte).");
            }
            catch (System.Exception ex)
            {
                return (false, "EXCEPTION", ex.Message);
            }
        }

        private void EnsureTable(Company company, string tableName, string descr, BoUTBTableType tableType)
        {
            UserTablesMD? t = null;
            try
            {
                t = (UserTablesMD)company.GetBusinessObject(BoObjectTypes.oUserTables);
                if (t.GetByKey(tableName)) return;

                t.TableName = tableName;
                t.TableDescription = descr;
                t.TableType = tableType;

                int rc = t.Add();
                if (rc != 0)
                {
                    company.GetLastError(out int err, out string msg);
                    throw new System.Exception($"Error creant UDT {tableName}: ({err}) {msg}");
                }
            }
            finally
            {
                if (t != null) Marshal.ReleaseComObject(t);
            }
        }

        private void EnsureField(Company company, string tableName, string fieldNameNoU, string descr, BoFieldTypes type, int size)
        {
            // DI-API requereix el nom sense "U_" a Name, i posa U_ automàtic
            UserFieldsMD? f = null;
            try
            {
                f = (UserFieldsMD)company.GetBusinessObject(BoObjectTypes.oUserFields);
                if (SapFieldExists(Dades.oCompany, tableName, fieldNameNoU)) return;

                f.TableName = tableName;
                f.Name = fieldNameNoU;
                f.Description = descr;
                f.Type = type;

                if (type == BoFieldTypes.db_Alpha && size > 0)
                    f.EditSize = size;

                int rc = f.Add();
                if (rc != 0)
                {
                    company.GetLastError(out int err, out string msg);
                    throw new System.Exception($"Error creant UDF {tableName}.U_{fieldNameNoU}: ({err}) {msg}");
                }
            }
            finally
            {
                if (f != null) Marshal.ReleaseComObject(f);
            }
        }

        private void EnsureUdo(Company company)
        {
            UserObjectsMD? u = null;
            try
            {
                u = (UserObjectsMD)company.GetBusinessObject(BoObjectTypes.oUserObjectsMD);
                if (u.GetByKey(UdoCode)) return;

                u.Code = UdoCode;
                u.Name = UdoName;
                u.ObjectType = BoUDOObjType.boud_Document;
                u.TableName = HTable;

                // child table lines
                u.ChildTables.Add();
                u.ChildTables.TableName = LTable;

                // capacitats
                u.CanFind = BoYesNoEnum.tYES;
                u.ManageSeries = BoYesNoEnum.tYES;

                int rc = u.Add();
                if (rc != 0)
                {
                    company.GetLastError(out int err, out string msg);
                    throw new System.Exception($"Error creant UDO {UdoCode}: ({err}) {msg}");
                }
            }
            finally
            {
                if (u != null) Marshal.ReleaseComObject(u);
            }
        }

        private static bool SapFieldExists(Company company, string tableName, string fieldAliasNoU)
        {
            // fieldAliasNoU = "BOY_85_ECAT" (sense U_)
            var rs = (Recordset)company.GetBusinessObject(BoObjectTypes.BoRecordset);

            try
            {
                string alias = "U_" + fieldAliasNoU.Trim();

                rs.DoQuery($@"
                    SELECT 1
                    FROM CUFD
                    WHERE TableID = '{tableName.Replace("'", "''")}'
                      AND AliasID = '{alias.Replace("'", "''")}'
                ");

                return rs.RecordCount > 0;
            }
            finally
            {
                Marshal.ReleaseComObject(rs);
            }
        }


    }
}
