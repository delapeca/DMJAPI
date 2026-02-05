using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XNDmjApi.QrMulti.Models;

namespace XNDmjApi.Controllers
{
    // Controllers/QRController.Device.cs
    //
    // Endpoints (base route: /api/QR):
    //  - GET /api/QR/Device/{deviceName}     -> JSON "STATE"
    //  - GET /api/QR/Device/{deviceName}/Png -> image/png (codifica ScanValue)
    //
    // Regla:
    //  - El PNG sempre codifica ScanValue (tal qual). El "mode" només defineix si ho codifiquem com url o text.
    //  - Rotació / refresh token després d'ús: NO aquí (patch posterior).

    public partial class QRController : ControllerBase
    {
        [HttpGet("Device/{deviceName}")]
        public async Task<IActionResult> GetDeviceState([FromRoute] string deviceName, CancellationToken ct)
        {
            deviceName = (deviceName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(deviceName))
                return BadRequest(new { error = "MISSING_DEVICE_NAME" });

            // Assegura DB creada / esquema aplicat (mateix patró que Loxone/Tokens)
            await EnsureLoxoneDbAsync(ct);

            var d = await _loxDb.QrDevices
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.DeviceName == deviceName, ct);

            if (d == null)
                return NotFound(new { error = "DEVICE_NOT_FOUND", deviceName });

            // cache-buster: ticks UTC
            var v = d.UpdatedAt.ToUniversalTime().Ticks.ToString();
            var pngUrl = $"/api/QR/Device/{Uri.EscapeDataString(d.DeviceName)}/Png?v={v}";

            return Ok(new
            {
                deviceName = d.DeviceName,

                assignment = new
                {
                    kind = d.AssignmentKind,
                    id = d.AssignmentId,
                    label = d.AssignmentLabel
                },

                scan = new
                {
                    mode = d.ScanMode,
                    value = d.ScanValue
                },

                display = new
                {
                    pngUrl,
                    updatedAt = d.UpdatedAt.ToUniversalTime()
                }
            });
        }

        [HttpGet("Device/{deviceName}/Png")]
        [Produces("image/png")]
        public async Task<IActionResult> GetDevicePng([FromRoute] string deviceName, CancellationToken ct)
        {
            deviceName = (deviceName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(deviceName))
                return BadRequest(new { error = "MISSING_DEVICE_NAME" });

            await EnsureLoxoneDbAsync(ct);

            var d = await _loxDb.QrDevices
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.DeviceName == deviceName, ct);

            if (d == null)
                return NotFound();

            var mode = (d.ScanMode ?? string.Empty).Trim().ToLowerInvariant();
            var payload = (d.ScanValue ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(payload))
                return BadRequest(new { error = "MISSING_SCAN_VALUE" });

            // Map mode -> QrPngRequest.Mode
            // - url/plain_url => "url"
            // - la resta => "text"
            var qrMode = (mode == "url" || mode == "plain_url") ? "url" : "text";

            var req = new QrPngRequest
            {
                Mode = qrMode,
                Data = payload,

                // Defaults (es pot ampliar més endavant via DB / config)
                SizePx = 512,
                Ecc = "M"
            };

            // Evitar cache a clients/pantalles
            Response.Headers["Cache-Control"] = "no-store";

            // BuildPng ja retorna IActionResult (image/png)
            return BuildPng(req, uploadedLogo: null);
        }
    }
}
