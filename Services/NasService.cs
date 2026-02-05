using System;
using System.IO;
using System.Net.Http;
using System.Linq;               // 🔹 per FirstOrDefault a SaveJpeg
using Microsoft.AspNetCore.Http;
using XNDmjApi.Functions;
// 🔹 NO afegim using System.Drawing; ni System.Drawing.Imaging;
//     farem servir noms completament qualificats al codi


namespace XNDmjApi.Services
{
    /// <summary>
    /// Servei encarregat de gestionar la pujada de fitxers al NAS (Synology)
    /// fent servir el share de xarxa definit a Dades.DOMEDOC_IMAGES_ROOT.
    /// 
    /// 🔹 Escriu directament a \\10.10.60.13\imatgesweb\...
    /// 🔹 Retorna rutes relatives tipus /imatges_articles/XXX_HR.jpg
    ///     perquè coincideixin amb les UDFs que espera SAP.
    /// </summary>
    public class NasService
    {
        private readonly Funcions _funcions;
        private readonly HttpClient _httpClient;

        // Mateixes rutes lògiques que al PHP
        private const string RemotePathImages = "/imatges_articles";
        private const string RemotePathDocs = "/fitxes_tecniques";

        public NasService()
        {
            _funcions = new Funcions();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        // --------------------------------------------------
        //  MÈTODES PÚBLICS (mateix contracte que abans)
        // --------------------------------------------------

        /// <summary>
        /// Pujar imatge al NAS a partir d'un fitxer pujat (imageFile).
        /// Genera:
        ///   - {ItemCode}_HR.jpg
        ///   - {ItemCode}_TM.jpg
        /// a \\10.10.60.13\imatgesweb\imatges_articles
        /// 
        /// Retorna el path relatiu de la HiRes: /imatges_articles/{ItemCode}_HR.jpg
        /// </summary>
        public string UploadImageFromFile(string itemCode, IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                throw new ArgumentException("imageFile és nul o buit.", nameof(imageFile));

            using var ms = new MemoryStream();
            imageFile.CopyTo(ms);
            var bytes = ms.ToArray();

            return UploadImageCommon(itemCode, bytes);
        }

        /// <summary>
        /// Pujar imatge al NAS a partir d'una URL d'origen.
        /// </summary>
        public string UploadImageFromUrl(string itemCode, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("imageUrl és buit.", nameof(imageUrl));

            var bytes = DownloadFileBytesFromUrl(imageUrl);
            return UploadImageCommon(itemCode, bytes);
        }

        /// <summary>
        /// Pujar fitxa tècnica (PDF) des d'un fitxer pujat.
        /// Genera {ItemCode}_FT.pdf a \\10.10.60.13\imatgesweb\fitxes_tecniques
        /// i retorna /fitxes_tecniques/{ItemCode}_FT.pdf
        /// </summary>
        public string UploadTechFileFromFile(string itemCode, IFormFile techFile)
        {
            if (techFile == null || techFile.Length == 0)
                throw new ArgumentException("techFile és nul o buit.", nameof(techFile));

            using var ms = new MemoryStream();
            techFile.CopyTo(ms);
            var bytes = ms.ToArray();

            return UploadTechFileCommon(itemCode, bytes);
        }

        /// <summary>
        /// Pujar fitxa tècnica (PDF) des d'una URL d'origen.
        /// </summary>
        public string UploadTechFileFromUrl(string itemCode, string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                throw new ArgumentException("fileUrl és buit.", nameof(fileUrl));

            var bytes = DownloadFileBytesFromUrl(fileUrl);
            return UploadTechFileCommon(itemCode, bytes);
        }

        // --------------------------------------------------
        //  HELPERS PRIVATS
        // --------------------------------------------------

        /// <summary>
        /// Escriu imatges de producte al NAS amb control de resolució i redimensionat:
        ///   - Requereix que el costat més curt >= 1080 px.
        ///   - Genera:
        ///       * {ItemCode}_HR.jpg  → HiRes (costat més curt ~1600 px)
        ///       * {ItemCode}_TM.jpg  → Thumbnail (costat més curt ~400 px)
        ///
        /// Retorna el path relatiu de la HiRes: /imatges_articles/{ItemCode}_HR.jpg
        /// </summary>
        private string UploadImageCommon(string itemCode, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("itemCode és buit.", nameof(itemCode));

            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes de la imatge buits.", nameof(bytes));

            // 1️⃣ Carreguem la imatge en memòria
            using (var ms = new MemoryStream(bytes))
            using (var original = System.Drawing.Image.FromStream(ms))
            {
                int width = original.Width;
                int height = original.Height;
                int shortSide = Math.Min(width, height);
                int longSide = Math.Max(width, height);

                // 2️⃣ Ens assegurem que DOMEDOC_IMAGES_ROOT està inicialitzat
                EnsureDomedocRoot();

                var root = Dades.DOMEDOC_IMAGES_ROOT.TrimEnd('\\', '/');
                var imagesDir = Path.Combine(root, "imatges_articles");
                Directory.CreateDirectory(imagesDir);

                var baseName = itemCode;
                var hiResName = $"{baseName}_HR.jpg";
                var tmName = $"{baseName}_TM.jpg";

                var hiResFullPath = Path.Combine(imagesDir, hiResName);
                var tmFullPath = Path.Combine(imagesDir, tmName);

                // 3️⃣ Redimensionat:
                const int HiResMaxSide = 1080;
                const int ThumbMaxSide = 250;


                using (var hiRes = ResizeToMaxSide(original, HiResMaxSide))
                {
                    SaveJpeg(hiRes, hiResFullPath, 90L);
                }

                using (var thumb = ResizeToMaxSide(original, ThumbMaxSide))
                {
                    SaveJpeg(thumb, tmFullPath, 85L);
                }


                // 4️⃣ Path relatiu que després guardarem a SAP
                return $"{RemotePathImages}/{hiResName}";
            }
        }

        /// <summary>
        /// Crea una nova imatge redimensionada mantenint la relació d'aspecte
        /// de manera que el costat més curt sigui igual a targetShortSide.
        /// </summary>
        /// <summary>
        /// Redimensiona mantenint aspecte perquè el costat MÉS LLARG (max side)
        /// sigui igual a targetMaxSide. No fa upscale.
        /// </summary>
        private static System.Drawing.Image ResizeToMaxSide(System.Drawing.Image original, int targetMaxSide)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            if (targetMaxSide <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetMaxSide));

            int origWidth = original.Width;
            int origHeight = original.Height;
            int longSide = Math.Max(origWidth, origHeight);

            // Si ja és <= target, no fem upscale: retornem còpia
            if (longSide <= targetMaxSide)
            {
                var copy = new System.Drawing.Bitmap(origWidth, origHeight);
                using (var g = System.Drawing.Graphics.FromImage(copy))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(original, 0, 0, origWidth, origHeight);
                }
                return copy;
            }

            double scale = targetMaxSide / (double)longSide;
            int newWidth = (int)Math.Round(origWidth * scale);
            int newHeight = (int)Math.Round(origHeight * scale);

            var result = new System.Drawing.Bitmap(newWidth, newHeight);
            using (var g = System.Drawing.Graphics.FromImage(result))
            {
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(original, 0, 0, newWidth, newHeight);
            }

            return result;
        }


        /// <summary>
        /// Desa una imatge en format JPEG amb el nivell de qualitat indicat.
        /// </summary>
        private static void SaveJpeg(System.Drawing.Image image, string path, long quality)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            if (quality < 0L || quality > 100L)
                quality = 90L;

            var jpegCodec = System.Drawing.Imaging.ImageCodecInfo
                .GetImageEncoders()
                .FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

            if (jpegCodec == null)
            {
                // Fallback senzill si per algun motiu no trobem el còdec
                image.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
                return;
            }

            using (var encoderParams = new System.Drawing.Imaging.EncoderParameters(1))
            {
                encoderParams.Param[0] =
                    new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                image.Save(path, jpegCodec, encoderParams);
            }
        }

        private string UploadTechFileCommon(string itemCode, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("itemCode és buit.", nameof(itemCode));

            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes de la fitxa tècnica buits.", nameof(bytes));

            // Ens assegurem que DOMEDOC_IMAGES_ROOT està inicialitzat
            EnsureDomedocRoot();

            var root = Dades.DOMEDOC_IMAGES_ROOT.TrimEnd('\\', '/');
            var docsDir = Path.Combine(root, "fitxes_tecniques");
            Directory.CreateDirectory(docsDir);

            var fileName = $"{itemCode}_FT.pdf";
            var fullFilePath = Path.Combine(docsDir, fileName);

            File.WriteAllBytes(fullFilePath, bytes);

            // Path relatiu per SAP
            return $"{RemotePathDocs}/{fileName}";
        }


        private byte[] DownloadFileBytesFromUrl(string url)
        {
            var resp = _httpClient.GetAsync(url).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"No s'ha pogut descarregar el fitxer des de la URL: {url}. HTTP {(int)resp.StatusCode} - {resp.ReasonPhrase}"
                );
            }

            return resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Assegura que Dades.DOMEDOC_IMAGES_ROOT està inicialitzat
        /// cridant Dades.SetupDades() si cal.
        /// 
        /// Fem servir el mateix patró que a ItemsService.EnsureConnection().
        /// </summary>
        private void EnsureDomedocRoot()
        {
            if (!string.IsNullOrWhiteSpace(Dades.DOMEDOC_IMAGES_ROOT))
                return;

            // Si encara no s'ha informat la BBDD de DOMENJÓ, seguim el patró existent
            if (string.IsNullOrWhiteSpace(Dades.DOMENJO_BBDD))
            {
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";
            }

            Dades.SetupDades();

            if (string.IsNullOrWhiteSpace(Dades.DOMEDOC_IMAGES_ROOT))
            {
                throw new InvalidOperationException(
                    "No s'ha pogut inicialitzar Dades.DOMEDOC_IMAGES_ROOT a partir de SetupDades()."
                );
            }
        }

        /// <summary>
        /// Retorna la miniatura (_TM.jpg) d'un article com a byte[].
        /// 
        /// Regles:
        ///   - Si existeix {ItemCode}_TM.jpg a imatges_articles → la retorna.
        ///   - Si NO existeix la Thumb però sí {ItemCode}_HR.jpg → genera una
        ///     miniatura a partir de la HiRes i la retorna (no cal guardar-la a disc).
        ///   - Si no hi ha cap de les dues → retorna un array buit.
        /// </summary>
        public byte[] GetItemThumbnailBytes(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("itemCode és buit.", nameof(itemCode));

            // Ens assegurem que DOMEDOC_IMAGES_ROOT està inicialitzat
            EnsureDomedocRoot();

            var root = Dades.DOMEDOC_IMAGES_ROOT.TrimEnd('\\', '/');
            var imagesDir = Path.Combine(root, "imatges_articles");

            var tmName = $"{itemCode}_TM.jpg";
            var hrName = $"{itemCode}_HR.jpg";

            var tmFullPath = Path.Combine(imagesDir, tmName);
            var hrFullPath = Path.Combine(imagesDir, hrName);

            // 1️⃣ Si ja existeix la Thumb, retornem directament els bytes
            if (File.Exists(tmFullPath))
            {
                return File.ReadAllBytes(tmFullPath);
            }

            // 2️⃣ Si no hi ha Thumb però sí HiRes, generem una miniatura "al vol"
            if (File.Exists(hrFullPath))
            {
                using (var original = System.Drawing.Image.FromFile(hrFullPath))
                using (var thumb = ResizeToMaxSide(original, 250))
                {
                    return SaveJpegToBytes(thumb, 85L);
                }
            }

                // 3️⃣ No hi ha ni Thumb ni HiRes
                return Array.Empty<byte>();
        }

        /// <summary>
        /// Converteix una imatge a JPEG en memòria i retorna els bytes,
        /// amb el nivell de qualitat indicat.
        /// </summary>
        private static byte[] SaveJpegToBytes(System.Drawing.Image image, long quality)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (quality < 0L || quality > 100L)
                quality = 90L;

            var jpegCodec = System.Drawing.Imaging.ImageCodecInfo
                .GetImageEncoders()
                .FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

            using (var ms = new MemoryStream())
            {
                if (jpegCodec == null)
                {
                    // Fallback si no trobem el còdec específic
                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                }
                else
                {
                    using (var encoderParams = new System.Drawing.Imaging.EncoderParameters(1))
                    {
                        encoderParams.Param[0] =
                            new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                        image.Save(ms, jpegCodec, encoderParams);
                    }
                }

                return ms.ToArray();
            }
        }


    }
}
