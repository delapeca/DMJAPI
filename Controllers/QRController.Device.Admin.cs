using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XNDmjApi.QrMulti.Models;

namespace XNDmjApi.Controllers
{
    // Controllers/QRController.Device.Admin.cs
    //
    // ADMIN (X-Api-Key):
    //  - GET    ~/api/qr/devices
    //  - GET    ~/api/qr/devices/{deviceName}
    //  - PUT    ~/api/qr/devices/{deviceName}
    //  - DELETE ~/api/qr/devices/{deviceName}

    public partial class QRController : ControllerBase
    {
        public sealed class UpsertQrDeviceRequest
        {
            // Assignment
            public string? AssignmentKind { get; set; }     // ex: loxone_action | qr_multi | ...
            public int? AssignmentId { get; set; }          // id intern segons kind
            public string? AssignmentLabel { get; set; }    // text lliure (UI)

            // Scan
            public string? ScanMode { get; set; }           // opaque_token | plain_url | url | wifi | text
            public string? ScanValue { get; set; }          // payload
        }

        // ----------------------------------------------------------
        // ADMIN: GET /api/qr/devices
        // ----------------------------------------------------------
        [HttpGet("~/api/qr/devices")]
        public async Task<IActionResult> Admin_ListDevices(CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            var items = await _loxDb.QrDevices
                .AsNoTracking()
                .OrderBy(x => x.DeviceName)
                .Select(x => new
                {
                    deviceName = x.DeviceName,
                    assignment = new { kind = x.AssignmentKind, id = x.AssignmentId, label = x.AssignmentLabel },
                    scan = new { mode = x.ScanMode, value = x.ScanValue },
                    updatedAt = x.UpdatedAt.ToUniversalTime()
                })
                .ToListAsync(ct);

            return Ok(new { items, count = items.Count });
        }

        // ----------------------------------------------------------
        // ADMIN: GET /api/qr/devices/{deviceName}
        // ----------------------------------------------------------
        [HttpGet("~/api/qr/devices/{deviceName}")]
        public async Task<IActionResult> Admin_GetDevice([FromRoute] string deviceName, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            deviceName = (deviceName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(deviceName))
                return BadRequest(new { error = "MISSING_DEVICE_NAME" });

            var d = await _loxDb.QrDevices.AsNoTracking()
                .SingleOrDefaultAsync(x => x.DeviceName == deviceName, ct);

            if (d == null)
                return NotFound(new { error = "NOT_FOUND", deviceName });

            return Ok(new
            {
                deviceName = d.DeviceName,
                assignment = new { kind = d.AssignmentKind, id = d.AssignmentId, label = d.AssignmentLabel },
                scan = new { mode = d.ScanMode, value = d.ScanValue },
                updatedAt = d.UpdatedAt.ToUniversalTime()
            });
        }

        // ----------------------------------------------------------
        // ADMIN: PUT /api/qr/devices/{deviceName}  (UPSERT)
        // ----------------------------------------------------------
        [HttpPut("~/api/qr/devices/{deviceName}")]
        public async Task<IActionResult> Admin_UpsertDevice([FromRoute] string deviceName, [FromBody] UpsertQrDeviceRequest req, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            deviceName = (deviceName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(deviceName))
                return BadRequest(new { error = "MISSING_DEVICE_NAME" });

            var assignmentKind = (req?.AssignmentKind ?? "").Trim();
            var scanMode = (req?.ScanMode ?? "").Trim();
            var scanValue = (req?.ScanValue ?? "").Trim();

            if (string.IsNullOrWhiteSpace(assignmentKind))
                return BadRequest(new { error = "MISSING_ASSIGNMENT_KIND" });

            if (string.IsNullOrWhiteSpace(scanMode))
                return BadRequest(new { error = "MISSING_SCAN_MODE" });

            if (string.IsNullOrWhiteSpace(scanValue))
                return BadRequest(new { error = "MISSING_SCAN_VALUE" });

            // UPSERT
            var row = await _loxDb.QrDevices.SingleOrDefaultAsync(x => x.DeviceName == deviceName, ct);
            var now = DateTime.UtcNow;

            if (row == null)
            {
                row = new QrDevice
                {
                    DeviceName = deviceName
                };
                _loxDb.QrDevices.Add(row);
            }

            row.AssignmentKind = assignmentKind;
            row.AssignmentId = req?.AssignmentId;
            row.AssignmentLabel = string.IsNullOrWhiteSpace(req?.AssignmentLabel) ? null : req!.AssignmentLabel!.Trim();

            row.ScanMode = scanMode;
            row.ScanValue = scanValue;

            row.UpdatedAt = now;

            await _loxDb.SaveChangesAsync(ct);

            return Ok(new
            {
                ok = true,
                deviceName = row.DeviceName,
                assignment = new { kind = row.AssignmentKind, id = row.AssignmentId, label = row.AssignmentLabel },
                scan = new { mode = row.ScanMode, value = row.ScanValue },
                updatedAt = row.UpdatedAt.ToUniversalTime()
            });
        }

        // ----------------------------------------------------------
        // ADMIN: DELETE /api/qr/devices/{deviceName}
        // ----------------------------------------------------------
        [HttpDelete("~/api/qr/devices/{deviceName}")]
        public async Task<IActionResult> Admin_DeleteDevice([FromRoute] string deviceName, CancellationToken ct)
        {
            if (!IsAdminAuthorized(out var fail)) return fail!;
            await EnsureLoxoneDbAsync(ct);

            deviceName = (deviceName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(deviceName))
                return BadRequest(new { error = "MISSING_DEVICE_NAME" });

            var row = await _loxDb.QrDevices.SingleOrDefaultAsync(x => x.DeviceName == deviceName, ct);
            if (row == null)
                return NotFound(new { error = "NOT_FOUND", deviceName });

            _loxDb.QrDevices.Remove(row);
            await _loxDb.SaveChangesAsync(ct);

            return Ok(new { ok = true, deviceName });
        }
    }
}
