using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XNDmjApi.Models.Portfolio;
using XNDmjApi.Services;

namespace XNDmjApi.Controllers
{
    /// <summary>
    /// Endpoints de cartera de clients (rebuts pendents / trànsit / pagats).
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class CustomerPortfolioController : ControllerBase
    {
        private readonly CustomerPortfolioService _service;

        public CustomerPortfolioController(CustomerPortfolioService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Retorna el resum de rebuts del client segons els filtres indicats.
        ///
        /// POST /CustomerPortfolio/GetReceiptsSummary
        /// Body (JSON):
        /// {
        ///   "cardCode": "C000123",
        ///   "receiptStatus": "PENDING",
        ///   "year": 2025,
        ///   "fromDate": "2025-01-01",
        ///   "toDate": "2025-12-31",
        ///   "receiptNum": null,
        ///   "numAtCard": null,
        ///   "maxRows": 500
        /// }
        /// </summary>
        [HttpPost("GetReceiptsSummary")]
        [ProducesResponseType(typeof(IEnumerable<ReceiptSummaryDto>), 200)]
        public async Task<IActionResult> GetReceiptsSummary([FromBody] ReceiptSummaryRequest request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(request.CardCode))
                return BadRequest("CardCode is required.");

            // Normalitzem estat
            request.ReceiptStatus = (request.ReceiptStatus ?? "PENDING").ToUpperInvariant();

            var allowed = new[] { "PENDING", "TRANSIT", "PAID" };
            if (Array.IndexOf(allowed, request.ReceiptStatus) < 0)
            {
                request.ReceiptStatus = "PENDING";
            }

            // Si no hi ha cap filtre de dates ni any → per defecte any actual
            if (request.Year == null && request.FromDate == null && request.ToDate == null)
            {
                request.Year = DateTime.Today.Year;
            }

            // Si cal, pots aplicar aquí normes extra tipus:
            // - si no hi ha Year però hi ha From/To, assegurar parelles, etc.

            var result = await _service.GetReceiptsSummaryAsync(request);
            return Ok(result);
        }
    }
}

