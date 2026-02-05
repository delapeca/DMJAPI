using System;

namespace XNDmjApi.Models.Portfolio
{
    /// <summary>
    /// Línia de resum de rebuts de la cartera d'un client.
    /// </summary>
    public class ReceiptSummaryDto
    {
        /// <summary>
        /// Data principal del rebut (p. ex. data document o data de venciment).
        /// </summary>
        public DateTime? Date { get; set; }

        /// <summary>
        /// Data de venciment del rebut.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Data de cobrament (si aplica).
        /// </summary>
        public DateTime? PaidDate { get; set; }

        /// <summary>
        /// Núm. de rebut (referència de cartera / remesa / etc.).
        /// </summary>
        public string Number { get; set; }

        /// <summary>
        /// Tipus de document origen (ex: FACTURA, ABONAMENT).
        /// </summary>
        public string OriginDocType { get; set; }

        /// <summary>
        /// Núm. document origen (ex: número de factura).
        /// </summary>
        public string OriginDocNum { get; set; }

        /// <summary>
        /// Estat del rebut (PENDING / TRANSIT / PAID / ...).
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Import en moneda local o en la moneda del rebut.
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// Moneda (ex: EUR).
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// Banc / compte bancari / forma de cobrament (si vols mostrar-ho).
        /// </summary>
        public string BankAccount { get; set; }

        /// <summary>
        /// Observacions o comentaris del rebut.
        /// </summary>
        public string Remarks { get; set; }
    }
}
