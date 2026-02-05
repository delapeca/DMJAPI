// Controllers/QRController.cs
//
// DMJAPI - QR multipropòsit (PNG)
//  - GET  /api/QR/Png?...         -> retorna image/png (sense upload, només logoKey)
//  - POST /api/QR/Png (form-data) -> retorna image/png (permet logoPng upload)
//
// Modes suportats:
//  - mode=url  + data=...
//  - mode=text + data=...
//  - mode=wifi + ssid=... + password=... + auth=WPA|WEP|NOPASS + hidden=true|false
//
// Mode "estricte" (tal com has demanat):
//  - Si hi ha logo (logoKey o logoPng), exigeix ecc=H
//  - logoPng: només PNG, màxim 300KB i màxim 512x512, i validació de signatura PNG
//  - iconSizePercent limitat per mida QR i ECC (i rebutja si excedeix)
//
// Notes:
//  - System.Drawing és adequat en Windows (IIS). En Linux caldria alternativa.
//  - Retorna PNG amb headers no-cache per evitar caches “estranyes” al navegador.

using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace XNDmjApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public partial class QRController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        // ==========================================================
        // LIMITS (OPCIÓ 1 / ESTRICTE)
        // ==========================================================
        private const long MaxUploadedLogoBytes = 300L * 1024L; // 300 KB
        private const int MaxUploadedLogoDim = 512;             // 512x512 màxim (abans de normalitzar)
        private const int LogoDownscaleMaxPx = 256;             // downscale intern del logo (per rendiment)

        private static readonly byte[] PngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

        internal QRController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // ==========================================================
        // ENDPOINTS
        // ==========================================================

        // GET: /api/QR/Png?mode=url&data=...&sizeMm=22&dpi=203&logoKey=domenjo50
        // Per GET, NO acceptem fitxer. Només logoKey.
        [HttpGet("Png")]
        [Produces("image/png")]
        public IActionResult PngGet([FromQuery] QrPngRequest req)
        {
            return BuildPng(req, uploadedLogo: null);
        }

        // POST: /api/QR/Png (multipart/form-data)
        // Permet enviar logoPng (fitxer) si vols.
        //
        // IMPORTANT: límit del POST (marge extra per multipart + camps)
        [HttpPost("Png")]
        [Consumes("multipart/form-data")]
        [Produces("image/png")]
        [RequestSizeLimit(400_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 400_000)]
        public IActionResult PngPost([FromForm] QrPngPostRequest req)
        {
            return BuildPng(req, uploadedLogo: req.LogoPng);
        }

        // ==========================================================
        // CORE
        // ==========================================================
        private IActionResult BuildPng(QrPngRequest req, IFormFile? uploadedLogo)
        {
            try
            {
                req.Normalize();

                // 1) Payload
                string payload = BuildPayload(req);

                // 2) Mida final en píxels (sizePx o sizeMm/dpi)
                int targetPx = ResolveTargetPx(req);

                // 3) ECC
                var ecc = ParseEcc(req.Ecc);

                // 4) Logo (opcional)
                Bitmap? logoBmp = null;

                try
                {
                    // Carrega logo (upload o logoKey) i valida límits (estricte)
                    logoBmp = LoadLogoBitmap(req, uploadedLogo, targetPx);

                    // Mode estricte: amb logo, ECC ha de ser H
                    if (logoBmp != null && ecc != QRCodeGenerator.ECCLevel.H)
                        return BadRequest(new { error = "Amb logo, el QR ha d'usar ecc=H (correcció d'errors alta)." });

                    // Mode estricte: iconSizePercent limitat segons mida QR i ECC
                    if (logoBmp != null)
                    {
                        int maxPct = ResolveMaxIconPercent(targetPx, ecc);
                        if (req.IconSizePercent > maxPct)
                        {
                            return BadRequest(new
                            {
                                error =
                                    $"iconSizePercent={req.IconSizePercent} és massa gran per sizePx={targetPx} i ecc={ecc}. " +
                                    $"Màxim recomanat: {maxPct}. (Solució: baixa iconSizePercent o puja sizePx/sizeMm; amb logo, ecc=H)."
                            });
                        }
                        if (req.IconSizePercent < 1)
                        {
                            return BadRequest(new { error = "iconSizePercent ha de ser >= 1." });
                        }
                    }

                    using var generator = new QRCodeGenerator();
                    using QRCodeData qrData = generator.CreateQrCode(payload, ecc);

                    // Pixels per mòdul (aprox). Després escalarem a mida exacta (nearest-neighbor).
                    int modules = qrData.ModuleMatrix.Count;
                    int ppm = Math.Max(1, targetPx / Math.Max(1, modules));

                    using var qr = new QRCode(qrData);

                    bool drawQuietZones = req.QuietZones;

                    using Bitmap rawBmp = (logoBmp == null)
                        ? qr.GetGraphic(ppm, Color.Black, Color.White, drawQuietZones)
                        : qr.GetGraphic(
                            ppm,
                            Color.Black,
                            Color.White,
                            logoBmp,
                            req.IconSizePercent,
                            req.IconBorderWidth,
                            drawQuietZones
                        );

                    // Ajust final a quadrat exactament targetPx x targetPx (important per impressió)
                    using Bitmap finalBmp = ResizeToSquareNearest(rawBmp, targetPx);

                    byte[] png = BitmapToPng(finalBmp);

                    // Evitem caches (sobretot en navegadors / intranet)
                    Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                    Response.Headers["Pragma"] = "no-cache";

                    return File(png, "image/png");
                }
                finally
                {
                    logoBmp?.Dispose();
                }
            }
            catch (ArgumentException ex)
            {
                // Input dolent -> 400
                return BadRequest(new { error = ex.Message });
            }
        }

        // ==========================================================
        // PAYLOAD (url/text/wifi)
        // ==========================================================
        private static string BuildPayload(QrPngRequest req)
        {
            switch (req.Mode)
            {
                case "url":
                case "text":
                    if (string.IsNullOrWhiteSpace(req.Data))
                        throw new ArgumentException("Falta 'data' (mode=url|text).");
                    return req.Data.Trim();

                case "wifi":
                    if (string.IsNullOrWhiteSpace(req.Ssid))
                        throw new ArgumentException("Falta 'ssid' (mode=wifi).");

                    // Format WiFi QR:
                    // WIFI:T:WPA;S:MySSID;P:MyPass;H:false;;
                    // Escape bàsic per ; , : i \
                    static string esc(string s) => s
                        .Replace("\\", "\\\\")
                        .Replace(";", "\\;")
                        .Replace(",", "\\,")
                        .Replace(":", "\\:");

                    string auth = (req.Auth ?? "WPA").Trim().ToUpperInvariant();
                    if (auth != "WPA" && auth != "WEP" && auth != "NOPASS")
                        auth = "WPA";

                    string ssid = esc(req.Ssid.Trim());
                    string pwd = esc((req.Password ?? "").Trim());
                    string hidden = req.Hidden ? "true" : "false";

                    if (auth == "NOPASS") pwd = "";

                    return $"WIFI:T:{auth};S:{ssid};P:{pwd};H:{hidden};;";

                default:
                    throw new ArgumentException("mode ha de ser: url | text | wifi");
            }
        }

        // ==========================================================
        // MIDA QR (px)
        // ==========================================================
        private static int ResolveTargetPx(QrPngRequest req)
        {
            // sizePx té prioritat
            if (req.SizePx.HasValue && req.SizePx.Value >= 64 && req.SizePx.Value <= 2000)
                return req.SizePx.Value;

            // sizeMm + dpi -> px
            if (req.SizeMm.HasValue && req.SizeMm.Value > 0)
            {
                int dpi = req.Dpi <= 0 ? 203 : req.Dpi;
                double px = (double)req.SizeMm.Value * dpi / 25.4;
                int outPx = (int)Math.Round(px, MidpointRounding.AwayFromZero);
                return Math.Clamp(outPx, 64, 2000);
            }

            // Default raonable
            return 240;
        }

        // ==========================================================
        // ECC (Error Correction Capacity)
        //
        // L: baixa  (7%)   -> més espai però menys tolerant
        // M: mitja  (15%)
        // Q: alta   (25%)
        // H: molt alta (30%) -> recomanada/obligada si hi ha logo (estricte)
        // ==========================================================
        private static QRCodeGenerator.ECCLevel ParseEcc(string? ecc)
        {
            string e = (ecc ?? "").Trim().ToUpperInvariant();
            return e switch
            {
                "L" => QRCodeGenerator.ECCLevel.L,
                "M" => QRCodeGenerator.ECCLevel.M,
                "Q" => QRCodeGenerator.ECCLevel.Q,
                "H" => QRCodeGenerator.ECCLevel.H,
                _ => QRCodeGenerator.ECCLevel.Q,
            };
        }

        // ==========================================================
        // LOGO: upload (opció 1) o logoKey (assets)
        //
        // Restriccions:
        //  - upload: PNG, signatura PNG, <= 300KB
        //  - dimensió màxima (dinàmica): min(512, max(64, targetPx))
        //    (això fa que el límit depengui del QR triat)
        //  - normalització: crop al centre (quadrat) + downscale a <= 256px
        // ==========================================================
        private Bitmap? LoadLogoBitmap(QrPngRequest req, IFormFile? uploadedLogo, int targetPx)
        {
            // Límit dinàmic per dimensió del logo segons mida del QR
            int dynMaxDim = Math.Min(MaxUploadedLogoDim, Math.Max(64, targetPx));

            // 1) Si ve fitxer per POST
            if (uploadedLogo != null && uploadedLogo.Length > 0)
            {
                if (uploadedLogo.Length > MaxUploadedLogoBytes)
                    throw new ArgumentException($"logoPng supera el límit: {MaxUploadedLogoBytes} bytes (300KB).");

                // En mode estricte, si ContentType ve informat i no és PNG -> fora
                if (!string.IsNullOrWhiteSpace(uploadedLogo.ContentType) &&
                    !string.Equals(uploadedLogo.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("logoPng ha de ser un PNG (Content-Type image/png).");

                // Validació signatura PNG (8 bytes)
                byte[] sig = new byte[8];
                using (var rs = uploadedLogo.OpenReadStream())
                {
                    int n = rs.Read(sig, 0, sig.Length);
                    if (n < 8) throw new ArgumentException("logoPng no és un PNG vàlid (fitxer massa curt).");
                }
                if (!HasPngSignature(sig))
                    throw new ArgumentException("logoPng no és un PNG vàlid (signatura incorrecta).");

                // Carregar bitmap (desenganxat de l'stream)
                using var ms = new MemoryStream((int)uploadedLogo.Length);
                uploadedLogo.CopyTo(ms);
                ms.Position = 0;

                using var tmp = new Bitmap(ms);

                if (tmp.Width > dynMaxDim || tmp.Height > dynMaxDim)
                {
                    throw new ArgumentException(
                        $"logoPng massa gran: {tmp.Width}x{tmp.Height}. " +
                        $"Màxim permès: {dynMaxDim}x{dynMaxDim} (depèn de sizePx del QR)."
                    );
                }

                using var normalized = NormalizeLogo(tmp, targetPx);
                return new Bitmap(normalized); // clone final
            }

            // 2) Si ve logoKey (assets locals)
            if (!string.IsNullOrWhiteSpace(req.LogoKey))
            {
                string key = req.LogoKey.Trim().ToLowerInvariant();
                string file = key switch
                {
                    "domenjo50" => Path.Combine(_env.ContentRootPath, "Assets", "Qr", "domenjo_50.png"),
                    "domenjo16" => Path.Combine(_env.ContentRootPath, "Assets", "Qr", "domenjo_16.png"),
                    _ => ""
                };

                if (!string.IsNullOrWhiteSpace(file) && System.IO.File.Exists(file))
                {
                    using var tmp = new Bitmap(file);

                    if (tmp.Width > dynMaxDim || tmp.Height > dynMaxDim)
                    {
                        throw new ArgumentException(
                            $"Logo asset massa gran: {tmp.Width}x{tmp.Height}. " +
                            $"Màxim permès: {dynMaxDim}x{dynMaxDim} (depèn de sizePx del QR)."
                        );
                    }

                    using var normalized = NormalizeLogo(tmp, targetPx);
                    return new Bitmap(normalized); // clone (evita lock del fitxer)
                }
            }

            return null;
        }

        // Normalitza logo:
        //  - si no és quadrat: crop al centre (quadrat)
        //  - downscale a un màxim (per rendiment)
        private static Bitmap NormalizeLogo(Bitmap src, int targetPx)
        {
            using var square = (src.Width == src.Height) ? new Bitmap(src) : CropCenterSquare(src);

            // Downscale màxim: 256 (o menys si el QR és petit)
            int maxSide = Math.Min(LogoDownscaleMaxPx, Math.Max(64, targetPx));
            if (square.Width <= maxSide)
                return new Bitmap(square);

            return ResizeHighQuality(square, maxSide, maxSide);
        }

        // ==========================================================
        // RENDER: resize final “nearest neighbor”
        // (evita artefactes i manté mòduls del QR nets)
        // ==========================================================
        private static Bitmap ResizeToSquareNearest(Bitmap src, int sizePx)
        {
            var dst = new Bitmap(sizePx, sizePx, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;
                g.Clear(Color.White);
                g.DrawImage(src, 0, 0, sizePx, sizePx);
            }
            return dst;
        }

        private static byte[] BitmapToPng(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        // ==========================================================
        // VALIDACIONS / HELPERS IMATGE
        // ==========================================================
        private static bool HasPngSignature(byte[] first8Bytes)
        {
            if (first8Bytes.Length < 8) return false;
            for (int i = 0; i < 8; i++)
                if (first8Bytes[i] != PngSignature[i]) return false;
            return true;
        }

        private static Bitmap CropCenterSquare(Bitmap src)
        {
            int side = Math.Min(src.Width, src.Height);
            int x = (src.Width - side) / 2;
            int y = (src.Height - side) / 2;

            var dst = new Bitmap(side, side, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.Clear(Color.Transparent);
                g.DrawImage(
                    src,
                    new Rectangle(0, 0, side, side),
                    new Rectangle(x, y, side, side),
                    GraphicsUnit.Pixel
                );
            }
            return dst;
        }

        private static Bitmap ResizeHighQuality(Bitmap src, int w, int h)
        {
            var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.Clear(Color.Transparent);
                g.DrawImage(src, 0, 0, w, h);
            }
            return dst;
        }

        // Límit màxim recomanat per iconSizePercent (segons ECC + mida QR)
        private static int ResolveMaxIconPercent(int targetPx, QRCodeGenerator.ECCLevel ecc)
        {
            int baseMax = ecc switch
            {
                QRCodeGenerator.ECCLevel.H => 22,
                QRCodeGenerator.ECCLevel.Q => 18,
                QRCodeGenerator.ECCLevel.M => 12,
                QRCodeGenerator.ECCLevel.L => 8,
                _ => 18
            };

            // Si el QR és petit, retallem més el percentatge
            if (targetPx < 180) baseMax = Math.Min(baseMax, 18);
            if (targetPx < 140) baseMax = Math.Min(baseMax, 15);

            return baseMax;
        }
    }

    // ==========================================================
    // MODELS REQUEST
    // ==========================================================
    public class QrPngPostRequest : QrPngRequest
    {
        // Upload (opció 1)
        public IFormFile? LogoPng { get; set; }
    }

    public class QrPngRequest
    {
        // url | text | wifi
        public string? Mode { get; set; }

        // per url/text
        public string? Data { get; set; }

        // per wifi
        public string? Ssid { get; set; }
        public string? Password { get; set; }
        public string? Auth { get; set; }   // WPA | WEP | NOPASS
        public bool Hidden { get; set; }

        // mida
        public int? SizePx { get; set; }
        public decimal? SizeMm { get; set; }
        public int Dpi { get; set; } = 203;

        // QR tuning
        public string? Ecc { get; set; } = "Q";
        public bool QuietZones { get; set; } = true;

        // logo
        public string? LogoKey { get; set; } // domenjo50 | domenjo16
        public int IconSizePercent { get; set; } = 18;   // recomanat 15-20
        public int IconBorderWidth { get; set; } = 0;    // 0 = sense marc

        public void Normalize()
        {
            Mode = (Mode ?? "url").Trim().ToLowerInvariant();
            if (Auth != null) Auth = Auth.Trim();
            if (Ecc != null) Ecc = Ecc.Trim();
            if (LogoKey != null) LogoKey = LogoKey.Trim();
        }
    }
}
