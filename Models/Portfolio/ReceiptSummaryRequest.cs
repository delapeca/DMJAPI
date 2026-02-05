using System;

namespace XNDmjApi.Models.Portfolio
{
    /// <summary>
    /// Paràmetres de filtre per a la cartera de rebuts d'un client.
    /// </summary>
    public class ReceiptSummaryRequest
    {
        /// <summary>
        /// Codi de client (CardCode, ex: "C000123").
        /// </summary>
        public string CardCode { get; set; } = string.Empty;

        /// <summary>
        /// Estat dels rebuts (PENDING | TRANSIT | PAID).
        /// </summary>
        public string? ReceiptStatus { get; set; } = "PENDING";

        /// <summary>
        /// Any de filtre (opcional). Si és null, s'usaran FromDate/ToDate.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Data inici del rang (opcional).
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Data fi del rang (opcional).
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Núm. de rebut (opcional).
        /// </summary>
        public string? ReceiptNum { get; set; }

        /// <summary>
        /// Referència de client (NumAtCard, opcional).
        /// </summary>
        public string? NumAtCard { get; set; }

        /// <summary>
        /// Registres màxims a retornar (per evitar allaus).
        /// </summary>
        public int? MaxRows { get; set; }
    }
}
