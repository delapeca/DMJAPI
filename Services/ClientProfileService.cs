using System;
using System.Data;
using XNDmjApi.Functions;
using XNDmjApi.Models.ClientProfile;

namespace XNDmjApi.Services
{
    public class ClientProfileService
    {
        private readonly Funcions _funcions = new Funcions();

        private void EnsureConnection()
        {
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                Dades.SetupDades();
            }
        }

        public ClientProfileDto GetProfile(string cardCode)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                throw new ArgumentException("CardCode is required.", nameof(cardCode));

            EnsureConnection();

            var parametres = new[]
            {
                $"CardCode:{cardCode.Trim()}"
            };

            var sql = Funcions.GetQuery("GetClientProfile.sql");
            var table = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, sql, parametres);

            if (table == null || table.Rows.Count == 0)
            {
                return new ClientProfileDto { CardCode = cardCode.Trim() };
            }

            var row = table.Rows[0];

            int? ReadInt(string col)
            {
                if (!table.Columns.Contains(col) || row[col] == DBNull.Value) return null;
                if (int.TryParse(Convert.ToString(row[col]), out var v)) return v;
                return null;
            }

            decimal? ReadDec(string col)
            {
                if (!table.Columns.Contains(col) || row[col] == DBNull.Value) return null;
                if (decimal.TryParse(Convert.ToString(row[col]), out var v)) return v;
                return null;
            }

            string ReadStr(string col)
            {
                if (!table.Columns.Contains(col) || row[col] == DBNull.Value) return string.Empty;
                return Convert.ToString(row[col]) ?? string.Empty;
            }

            return new ClientProfileDto
            {
                CardCode = ReadStr("CardCode"),
                CardName = ReadStr("CardName"),
                Nif = ReadStr("Nif"),

                GroupCode = ReadInt("GroupCode"),
                GroupName = ReadStr("GroupName"),

                PaymentGroupCode = ReadInt("PaymentGroupCode"),
                PaymentGroupName = ReadStr("PaymentGroupName"),

                Email = ReadStr("Email"),
                Phone = ReadStr("Phone"),
                Mobile = ReadStr("Mobile"),

                BalanceAccount = ReadDec("BalanceAccount"),
                BalanceOrders = ReadDec("BalanceOrders"),
                BalanceDeliveries = ReadDec("BalanceDeliveries")
            };
        }
    }
}
