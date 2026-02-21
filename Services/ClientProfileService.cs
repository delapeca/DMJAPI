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

            //return new ClientProfileDto
            //{
            //    CardCode = ReadStr("CardCode"),
            //    CardName = ReadStr("CardName"),
            //    Nif = ReadStr("Nif"),

            //    GroupCode = ReadInt("GroupCode"),
            //    GroupName = ReadStr("GroupName"),

            //    PaymentGroupCode = ReadInt("PaymentGroupCode"),
            //    PaymentGroupName = ReadStr("PaymentGroupName"),

            //    Email = ReadStr("Email"),
            //    Phone = ReadStr("Phone"),
            //    Mobile = ReadStr("Mobile"),

            //    BalanceAccount = ReadDec("BalanceAccount"),
            //    BalanceOrders = ReadDec("BalanceOrders"),
            //    BalanceDeliveries = ReadDec("BalanceDeliveries")
            //};

            return new ClientProfileDto
            {
                DBNAME = ReadStr("DatabaseName"),
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

                // Método de pago por defecto
                PayMethCod = ReadStr("PayMethCod"),
                PayMethName = ReadStr("PayMethName"),

                // Límites y saldos
                CreditLine = ReadDec("CreditLine"),
                BalanceAccount = ReadDec("BalanceAccount"),
                BalanceOrders = ReadDec("BalanceOrders"),
                BalanceDeliveries = ReadDec("BalanceDeliveries"),

                // IVA
                VatStatus = ReadStr("VatStatus"),
                ECVatGroup = ReadStr("ECVatGroup"),

                // Dirección fiscal por defecto (Bill-to)
                BillStreet = ReadStr("BillStreet"),
                BillBlock = ReadStr("BillBlock"),
                BillZipCode = ReadStr("BillZipCode"),
                BillCity = ReadStr("BillCity"),
                BillCountry = ReadStr("BillCountry"),
                BillState = ReadStr("BillState"),
                BillAddressName = ReadStr("BillAddressName")
            };
        }
    }
}
