using SAPbobsCOM;
using System;
using System.Runtime.InteropServices;
using XNDmjApi.Models;

namespace XNDmjApi.Functions
{
    public static class SapUdtHelper
    {
        /// <summary>
        /// Crea una UDT segons la definició rebuda.
        /// Crea la taula i, a continuació, tots els camps U_*.
        /// </summary>
        public static SapUdtCreationResult CreateUdt(Company company, UdtDefinition def)
        {
            if (company == null || !company.Connected)
            {
                return new SapUdtCreationResult
                {
                    Success = false,
                    Code = "NO_SAP_CONNECTION",
                    Message = "No hi ha connexió SAP (Company no connectada)."
                };
            }

            if (def == null || string.IsNullOrWhiteSpace(def.TableName))
            {
                return new SapUdtCreationResult
                {
                    Success = false,
                    Code = "INVALID_DEFINITION",
                    Message = "Definició de UDT no vàlida (TableName buit)."
                };
            }

            string tableNameNoAt = def.TableName.Trim();
            string tableNameWithAt = "@" + tableNameNoAt;

            UserTablesMD oTable = null;

            try
            {
                oTable = (UserTablesMD)company.GetBusinessObject(BoObjectTypes.oUserTables);

                // Si ja existeix, retornem codi específic
                if (oTable.GetByKey(tableNameNoAt))
                {
                    return new SapUdtCreationResult
                    {
                        Success = false,
                        Code = "ALREADY_EXISTS",
                        Message = $"La UDT {tableNameWithAt} ja existeix."
                    };
                }

                oTable.TableName = tableNameNoAt;
                oTable.TableDescription = string.IsNullOrWhiteSpace(def.TableDescription)
                    ? tableNameNoAt
                    : def.TableDescription;
                oTable.TableType = ParseTableType(def.TableType);

                int res = oTable.Add();
                if (res != 0)
                {
                    company.GetLastError(out int errCode, out string errMsg);
                    return new SapUdtCreationResult
                    {
                        Success = false,
                        Code = "TABLE_ERROR",
                        Message = $"Error creant UDT {tableNameWithAt}: {errCode} - {errMsg}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new SapUdtCreationResult
                {
                    Success = false,
                    Code = "EXCEPTION",
                    Message = $"Excepció creant UDT {tableNameWithAt}: {ex.Message}"
                };
            }
            finally
            {
                if (oTable != null) Marshal.ReleaseComObject(oTable);
            }

            // Ara creem els camps U_*
            foreach (var field in def.Fields)
            {
                var fieldResult = AddField(company, tableNameWithAt, field);
                if (!fieldResult.Success)
                {
                    // En cas d'error en un camp, retornem directament
                    return fieldResult;
                }
            }

            return new SapUdtCreationResult
            {
                Success = true,
                Code = "OK",
                Message = $"UDT {tableNameWithAt} creada correctament amb {def.Fields.Count} camps."
            };
        }

        private static BoUTBTableType ParseTableType(string tableType)
        {
            switch (tableType?.Trim().ToLowerInvariant())
            {
                case "masterdata":
                    return BoUTBTableType.bott_MasterData;
                case "masterdatalines":
                    return BoUTBTableType.bott_MasterDataLines;
                case "document":
                    return BoUTBTableType.bott_Document;
                case "documentlines":
                    return BoUTBTableType.bott_DocumentLines;
                case "noobject":
                default:
                    return BoUTBTableType.bott_NoObject;
            }
        }

        private static BoFieldTypes ParseFieldType(string type)
        {
            switch (type?.Trim().ToLowerInvariant())
            {
                case "numeric":
                    return BoFieldTypes.db_Numeric;
                case "date":
                    return BoFieldTypes.db_Date;
                case "memo":
                    return BoFieldTypes.db_Memo;
                case "alpha":
                default:
                    return BoFieldTypes.db_Alpha;
            }
        }

        /// <summary>
        /// Crea un camp U_ per a la taula indicada.
        /// </summary>
        private static SapUdtCreationResult AddField(Company company, string tableNameWithAt, UdfDefinition fieldDef)
        {
            if (fieldDef == null || string.IsNullOrWhiteSpace(fieldDef.Name))
            {
                return new SapUdtCreationResult
                {
                    Success = false,
                    Code = "INVALID_FIELD",
                    Message = "Definició de camp no vàlida (Name buit)."
                };
            }

            UserFieldsMD oField = null;

            try
            {
                oField = (UserFieldsMD)company.GetBusinessObject(BoObjectTypes.oUserFields);
                oField.TableName = tableNameWithAt;                // p.ex. "@XNWEBREG"
                oField.Name = fieldDef.Name.Trim();                // p.ex. "Email" → U_Email
                oField.Description = string.IsNullOrWhiteSpace(fieldDef.Description)
                    ? fieldDef.Name.Trim()
                    : fieldDef.Description.Trim();
                oField.Type = ParseFieldType(fieldDef.Type);

                if (oField.Type == BoFieldTypes.db_Alpha || oField.Type == BoFieldTypes.db_Memo)
                {
                    int size = (fieldDef.Size.HasValue && fieldDef.Size.Value > 0)
                        ? fieldDef.Size.Value
                        : 50;
                    oField.EditSize = size;
                }

                int res = oField.Add();
                if (res != 0)
                {
                    company.GetLastError(out int errCode, out string errMsg);
                    return new SapUdtCreationResult
                    {
                        Success = false,
                        Code = "FIELD_ERROR",
                        Message = $"Error creant camp {fieldDef.Name} a {tableNameWithAt}: {errCode} - {errMsg}"
                    };
                }

                return new SapUdtCreationResult
                {
                    Success = true,
                    Code = "OK",
                    Message = $"Camp {fieldDef.Name} creat correctament a {tableNameWithAt}."
                };
            }
            catch (Exception ex)
            {
                return new SapUdtCreationResult
                {
                    Success = false,
                    Code = "FIELD_EXCEPTION",
                    Message = $"Excepció creant camp {fieldDef.Name} a {tableNameWithAt}: {ex.Message}"
                };
            }
            finally
            {
                if (oField != null) Marshal.ReleaseComObject(oField);
            }
        }
    }
}
