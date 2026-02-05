using XNDmjApi.Functions;
using XNDmjApi.Models;

namespace XNDmjApi.Services
{
    /// <summary>
    /// Lògica de negoci per gestionar media (imatges + fitxes tècniques)
    /// d'articles i les seves variants.
    /// 
    /// 👉 Ara només és un esquelet. Encara NO hi ha DI-API ni NAS.
    /// </summary>
    public class ItemMediaService
    {
        private readonly Funcions _funcions;
        private readonly NasService _nasService;
        private readonly SapItemMediaService _sapItemMediaService;

        public ItemMediaService()
        {
            _funcions = new Funcions();
            _nasService = new NasService();
            _sapItemMediaService = new SapItemMediaService();
        }


        /// <summary>
        /// Processa la media per a un article origen i, opcionalment,
        /// per a les seves variants.
        /// 
        /// De moment:
        ///   - No fa cap acció real.
        ///   - Només retorna un placeholder perquè el controlador pugui
        ///     tenir un contracte estable amb la intranet.
        /// </summary>
        public ProcessItemMediaResponse ProcessItemMedia(ProcessItemMediaRequest request)
        {
            var response = new ProcessItemMediaResponse
            {
                Success = false,
                SourceItemCode = request?.ItemCode,
                SourceItem = new ItemMediaResultDetail
                {
                    ItemCode = request?.ItemCode
                }
            };

            // 1️⃣ Validacions bàsiques
            if (request == null)
            {
                response.SourceItem.Messages.Add("La petició és nul·la.");
                return response;
            }

            if (string.IsNullOrWhiteSpace(request.ItemCode))
            {
                response.SourceItem.Messages.Add("Falta el camp obligatori ItemCode.");
                return response;
            }

            // 2️⃣ Normalitzar variants
            var targetCodes = ParseTargetItems(request.TargetItems);

            foreach (var code in targetCodes)
            {
                response.TargetItems.Add(new ItemMediaResultDetail
                {
                    ItemCode = code,
                    Success = false,
                    Messages =
            {
                "Variant preparada per al processament de media (DI-API pendent d'implementar)."
            }
                });
            }

            // 3️⃣ Detectar què ens arriba de media
            bool hasImageFile = request.ImageFile != null && request.ImageFile.Length > 0;
            bool hasImageUrl = HasValidUrl(request.ImageUrl);
            bool hasTechFile = request.TechFile != null && request.TechFile.Length > 0;
            bool hasFileUrl = HasValidUrl(request.FileUrl);

            // LongDesc: distingim “text nou” vs “clear”
            bool hasLongDescText = !string.IsNullOrWhiteSpace(request.LongDesc);

            // IMPORTANT:
            //  - Aquest flag requereix la propietat bool NoLongDesc al ProcessItemMediaRequest.
            //  - Si NoLongDesc=true, s'ha de considerar una acció de LongDesc encara que LongDesc vingui null/buit.
            bool wantsClearLongDesc = request.NoLongDesc;

            // Acció de LongDesc (set o clear)
            bool wantsAnyLongDescAction = hasLongDescText || wantsClearLongDesc;

            // 👉 NO exigim ni imatge ni fitxa: pot ser un cas de només copiar o només desc.
            // Si no hi ha imatge, ni fitxa, ni cap acció de descripció llarga, ni variants → res a fer
            if (!hasImageFile && !hasImageUrl && !hasTechFile && !hasFileUrl && !wantsAnyLongDescAction && !targetCodes.Any())
            {
                response.SourceItem.Messages.Add(
                    "No s'ha indicat cap imatge, fitxa tècnica, descripció llarga ni variants a processar."
                );
                return response;
            }

            // 4️⃣ Info diagnòstica
            string longDescMode =
                wantsClearLongDesc ? "clear" :
                hasLongDescText ? "set" :
                "no";

            response.SourceItem.Messages.Add(
                $"Entrada rebuda: " +
                $"imageFile={(hasImageFile ? "sí" : "no")}, " +
                $"imageUrl={(hasImageUrl ? "sí" : "no")}, " +
                $"techFile={(hasTechFile ? "sí" : "no")}, " +
                $"fileUrl={(hasFileUrl ? "sí" : "no")}, " +
                $"longDesc={longDescMode}, " +
                $"copyImage={(request.CopyImage ? "sí" : "no")}, " +
                $"copyFicha={(request.CopyFicha ? "sí" : "no")}, " +
                $"copyDesc={(request.CopyDesc ? "sí" : "no")}."
            );

            if (targetCodes.Any())
            {
                response.SourceItem.Messages.Add(
                    "Variants a processar: " + string.Join(", ", targetCodes)
                );
            }
            else
            {
                response.SourceItem.Messages.Add("No s'han indicat variants (TargetItems buit).");
            }

            // --------------------------------------------------
            // 5️⃣ Pujada al NAS per l'article origen (SENSE SAP encara)
            // --------------------------------------------------
            string? hiResPath = null;
            string? thumbPath = null;
            string? techPath = null;

            bool sourceSuccess = true;

            // 🖼️ Imatge (HiRes + Thumb) si copyImage està actiu
            if (request.CopyImage && (hasImageFile || hasImageUrl))
            {
                try
                {
                    if (hasImageFile)
                    {
                        hiResPath = _nasService.UploadImageFromFile(request.ItemCode, request.ImageFile);
                    }
                    else if (hasImageUrl)
                    {
                        hiResPath = _nasService.UploadImageFromUrl(request.ItemCode, request.ImageUrl);
                    }

                    if (!string.IsNullOrWhiteSpace(hiResPath))
                    {
                        // Derivem el path de la Thumb igual que al PHP: _HR.jpg -> _TM.jpg
                        thumbPath = hiResPath.Replace("_HR.jpg", "_TM.jpg", StringComparison.OrdinalIgnoreCase);

                        response.SourceItem.UpdatedFields.Add("U_XN_HiRes");
                        response.SourceItem.UpdatedFields.Add("U_XN_Thumb");

                        response.SourceItem.Messages.Add(
                            $"Imatge pujada al NAS. HiRes={hiResPath}, Thumb={thumbPath} (pendent d'actualitzar SAP)."
                        );
                    }
                    else
                    {
                        sourceSuccess = false;
                        response.SourceItem.Messages.Add("No s'ha obtingut cap path de la imatge després de pujar-la al NAS.");
                    }
                }
                catch (Exception ex)
                {
                    sourceSuccess = false;
                    response.SourceItem.Messages.Add($"Error en pujar la imatge al NAS: {ex.Message}");
                }
            }
            else if (request.CopyImage)
            {
                response.SourceItem.Messages.Add(
                    "copyImage està actiu però no s'ha proporcionat ni imageFile ni imageUrl."
                );
            }

            // 📄 Fitxa tècnica (PDF) si copyFicha està actiu
            if (request.CopyFicha && (hasTechFile || hasFileUrl))
            {
                try
                {
                    if (hasTechFile)
                    {
                        techPath = _nasService.UploadTechFileFromFile(request.ItemCode, request.TechFile);
                    }
                    else if (hasFileUrl)
                    {
                        techPath = _nasService.UploadTechFileFromUrl(request.ItemCode, request.FileUrl);
                    }

                    if (!string.IsNullOrWhiteSpace(techPath))
                    {
                        response.SourceItem.UpdatedFields.Add("U_XN_FT");
                        response.SourceItem.Messages.Add(
                            $"Fitxa tècnica pujada al NAS: {techPath} (pendent d'actualitzar SAP)."
                        );
                    }
                    else
                    {
                        sourceSuccess = false;
                        response.SourceItem.Messages.Add("No s'ha obtingut cap path de la fitxa tècnica després de pujar-la al NAS.");
                    }
                }
                catch (Exception ex)
                {
                    sourceSuccess = false;
                    response.SourceItem.Messages.Add($"Error en pujar la fitxa tècnica al NAS: {ex.Message}");
                }
            }
            else if (request.CopyFicha)
            {
                response.SourceItem.Messages.Add(
                    "copyFicha està actiu però no s'ha proporcionat ni techFile ni fileUrl."
                );
            }

            // ℹ️ copyDesc: indicatiu de còpia/acció de descripció llarga a variants.
            // - Si NoLongDesc=true i CopyDesc=true: s'esborrarà també a variants.
            // - Si LongDesc ve informada i CopyDesc=true: es copiarà el text a variants.
            if (request.CopyDesc)
            {
                if (wantsClearLongDesc)
                {
                    response.SourceItem.Messages.Add(
                        "copyDesc està actiu i NoLongDesc=true. S'esborrarà la descripció llarga a les variants (si n'hi ha) un cop aplicada a SAP."
                    );
                }
                else
                {
                    response.SourceItem.Messages.Add(
                        "copyDesc està actiu. Es copiarà la descripció llarga de l'article origen a les variants (si n'hi ha) un cop actualitzada a SAP."
                    );
                }
            }

            // --------------------------------------------------
            // 6️⃣ Actualització SAP (DI-API) per article origen
            // --------------------------------------------------
            // IMPORTANT:
            // Abans només entrava si LongDesc tenia text. Ara també ha d’entrar si “NoLongDesc=true”.
            if (!string.IsNullOrWhiteSpace(hiResPath)
                || !string.IsNullOrWhiteSpace(techPath)
                || wantsAnyLongDescAction)
            {
                var sapResult = _sapItemMediaService.UpdateSourceItemMedia(request, hiResPath, techPath);

                // Afegim missatges de SAP al detall de l'article origen
                if (sapResult.Messages != null && sapResult.Messages.Count > 0)
                {
                    response.SourceItem.Messages.AddRange(sapResult.Messages);
                }

                // Combinar UpdatedFields (evitem duplicats)
                if (sapResult.UpdatedFields != null)
                {
                    foreach (var field in sapResult.UpdatedFields)
                    {
                        if (!response.SourceItem.UpdatedFields.Contains(field))
                        {
                            response.SourceItem.UpdatedFields.Add(field);
                        }
                    }
                }

                // L'èxit de SAP afecta l'èxit global de l'article origen
                sourceSuccess = sourceSuccess && sapResult.Success;
            }

            // --------------------------------------------------
            // 7️⃣ Actualització SAP (DI-API) per a cada variant
            // --------------------------------------------------
            bool globalSuccess = sourceSuccess;

            if (targetCodes.Any())
            {
                foreach (var code in targetCodes)
                {
                    var variantResult = _sapItemMediaService.UpdateVariantItemMedia(request, code);

                    // Busquem l'entrada ja creada a response.TargetItems per aquest codi
                    var existing = response.TargetItems.FirstOrDefault(t => t.ItemCode == code);
                    if (existing != null)
                    {
                        existing.Success = variantResult.Success;

                        existing.UpdatedFields.Clear();
                        foreach (var f in variantResult.UpdatedFields)
                        {
                            existing.UpdatedFields.Add(f);
                        }

                        existing.Messages.Clear();
                        foreach (var msg in variantResult.Messages)
                        {
                            existing.Messages.Add(msg);
                        }
                    }
                    else
                    {
                        // Per si de cas, l'afegim de nou
                        response.TargetItems.Add(variantResult);
                    }

                    globalSuccess = globalSuccess && variantResult.Success;
                }
            }

            // --------------------------------------------------
            // 8️⃣ Estat de success i missatges finals
            // --------------------------------------------------
            response.SourceItem.Success = sourceSuccess;

            // L'èxit global reflecteix l'estat de l'article origen i de les variants.
            response.Success = globalSuccess;

            if (sourceSuccess)
            {
                response.SourceItem.Messages.Add(
                    "Processament de media completat per l'article origen (NAS/SAP)."
                );
            }
            else
            {
                response.SourceItem.Messages.Add(
                    "Hi ha hagut errors en la pujada a NAS o en l'actualització a SAP. Revisa els missatges anteriors."
                );
            }

            return response;
        }


        /// <summary>
        /// Converteix la cadena de TargetItems (separats per comes) en una llista
        /// normalitzada i sense duplicats.
        /// </summary>
        private List<string> ParseTargetItems(string? targetItemsRaw)
        {
            if (string.IsNullOrWhiteSpace(targetItemsRaw))
                return new List<string>();

            return targetItemsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }


        /// <summary>
        /// Comprova si una URL és realment vàlida a nivell de dades d'entrada,
        /// descartant valors de placeholder típics de Swagger com "string".
        /// </summary>
        private bool HasValidUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            var trimmed = url.Trim();

            // Swagger sol posar "string" com a valor per defecte
            if (string.Equals(trimmed, "string", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

    }
}
