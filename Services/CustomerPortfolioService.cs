using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XNDmjApi.Models.Portfolio;
using System.Data;
using XNDmjApi.Functions;


namespace XNDmjApi.Services
{
    /// <summary>
    /// Servei encarregat de recuperar la cartera de rebuts d'un client
    /// (pendents / en trànsit / pagats).
    ///
    /// ⚠ Ara mateix:
    ///    - NO consulta encara SAP / SQL.
    ///    - Retorna DADES DE DEMO per provar el wiring Laravel + DMJAPI.
    ///    - Més endavant substituirem el bloc DEMO per la lògica real (SQL/DI-API).
    /// </summary>
    public class CustomerPortfolioService
    {
        public CustomerPortfolioService()
        {
            // TODO: quan fem la implementació real,
            //       injectarem aquí dependències (Funcions, connexió SQL, etc.).
        }

        private readonly Funcions _funcions = new Funcions();

        /// <summary>
        /// Garanteix que la connexió DOMENJÓ està inicialitzada
        /// (mateix patró que SalesOrdersService / OutletService).
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
        /// Retorna el resum de rebuts per a un client i filtres donats.
        /// Ara mateix torna una llista simulada per poder provar la UX.
        /// </summary>
        public Task<IReadOnlyList<ReceiptSummaryDto>> GetReceiptsSummaryAsync(ReceiptSummaryRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // 1️⃣ Normalitzem estat (encara no l’usem a SQL, però el deixem preparat)
            var status = (request.ReceiptStatus ?? "PENDING").ToUpperInvariant();
            if (status != "PENDING" && status != "TRANSIT" && status != "PAID")
            {
                status = "PENDING";
            }

            // 2️⃣ Determinem rang de dates
            DateTime from;
            DateTime to;

            if (request.Year.HasValue)
            {
                var year = request.Year.Value;
                from = new DateTime(year, 1, 1);
                to = new DateTime(year, 12, 31);
            }
            else
            {
                var today = DateTime.Today;

                from = request.FromDate?.Date ?? today.AddMonths(-6).Date;
                to = request.ToDate?.Date ?? today.Date;

                if (to < from)
                {
                    // Si vénen creuades, intercanviem
                    var tmp = from;
                    from = to;
                    to = tmp;
                }
            }

            // 3️⃣ MaxRows defensiu
            var maxRows = request.MaxRows.GetValueOrDefault(500);
            if (maxRows <= 0) maxRows = 500;

            // 4️⃣ Ens assegurem connexió DOMENJÓ
            EnsureConnection();

            // 5️⃣ Muntem paràmetres per al .sql
            var parametres = new[]
            {
                $"CardCode:{request.CardCode}",
                $"ReceiptStatus:{request.ReceiptStatus}",   // 👈 NOU
                $"FromDate:{from:yyyy-MM-dd}",
                $"ToDate:{to:yyyy-MM-dd}",
                $"ReceiptNum:{request.ReceiptNum ?? string.Empty}",
                $"NumAtCard:{request.NumAtCard ?? string.Empty}",
                $"MaxRows:{maxRows}"
            };

            // 6️⃣ Llegim el .sql
            var sql = Funcions.GetQuery("GetCustomerReceiptsSummary.sql");

            // 7️⃣ Executem i llegim resultats
            var table = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, sql, parametres);

            var list = new List<ReceiptSummaryDto>();

            foreach (DataRow row in table.Rows)
            {
                var dto = new ReceiptSummaryDto
                {
                    Date = row.Table.Columns.Contains("Date")
                        ? row.Field<DateTime?>("Date")
                        : null,

                    DueDate = row.Table.Columns.Contains("DueDate")
                        ? row.Field<DateTime?>("DueDate")
                        : null,

                    PaidDate = row.Table.Columns.Contains("PaidDate")
                        ? row.Field<DateTime?>("PaidDate")
                        : null,

                    Number = row.Table.Columns.Contains("Number")
                        ? Convert.ToString(row["Number"] ?? string.Empty)
                        : string.Empty,

                    OriginDocType = row.Table.Columns.Contains("OriginDocType")
                        ? Convert.ToString(row["OriginDocType"] ?? string.Empty)
                        : string.Empty,

                    OriginDocNum = row.Table.Columns.Contains("OriginDocNum")
                        ? Convert.ToString(row["OriginDocNum"] ?? string.Empty)
                        : string.Empty,

                    // ⚠ De moment, si no ens ve cap "Status" des de SQL,
                    //    fem servir l’estat demanat al filtre.
                    Status = row.Table.Columns.Contains("Status")
                        ? (Convert.ToString(row["Status"] ?? string.Empty)?.ToUpperInvariant() ?? status)
                        : status,

                    Amount = row.Table.Columns.Contains("Amount")
                        ? row.Field<decimal?>("Amount")
                        : null,

                    Currency = row.Table.Columns.Contains("Currency")
                        ? Convert.ToString(row["Currency"] ?? string.Empty)
                        : string.Empty,

                    BankAccount = row.Table.Columns.Contains("BankAccount")
                        ? Convert.ToString(row["BankAccount"] ?? string.Empty)
                        : string.Empty,

                    Remarks = row.Table.Columns.Contains("Remarks")
                        ? Convert.ToString(row["Remarks"] ?? string.Empty)
                        : string.Empty,
                };

                list.Add(dto);
            }

            return Task.FromResult<IReadOnlyList<ReceiptSummaryDto>>(list);
        }

    }
}
