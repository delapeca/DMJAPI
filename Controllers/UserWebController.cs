using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Globalization;
using XNDmjApi.Functions;
using XNDmjApi.Models;

namespace XNDmjApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserWebController : ControllerBase
    {
        /// <summary>
        /// Cerca d’usuaris web amb filtres opcionals.
        /// Filtres: Email, UserName, Rol, CardCode, IsAdmin, onlyActive.
        /// Nota: considerem "actiu" si U_ExpirationDate >= GETDATE().
        /// </summary>
        [HttpGet("SearchUsers")]
        public ActionResult SearchUsers([FromQuery] string userToken, [FromQuery] string? email, [FromQuery] string? userName, [FromQuery] string? rol, [FromQuery] string? cardCode, [FromQuery] string? isAdmin, [FromQuery] bool? onlyActive)
        {
            try
            {
                if (!RequireMinGroup(userToken, "Admin", out _, out _, out var fail)) return fail!;

                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                string sql = "SELECT * FROM [dbo].[@XNUSERWEB] WHERE 1=1";
                var prm = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(email))
                {
                    sql += " AND U_Email LIKE @Email";
                    prm.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 100) { Value = "%" + email + "%" });
                }

                if (!string.IsNullOrWhiteSpace(userName))
                {
                    sql += " AND U_UserName LIKE @UserName";
                    prm.Add(new SqlParameter("@UserName", SqlDbType.NVarChar, 100) { Value = "%" + userName + "%" });
                }

                if (!string.IsNullOrWhiteSpace(rol))
                {
                    sql += " AND U_Rol = @Rol";
                    prm.Add(new SqlParameter("@Rol", SqlDbType.NVarChar, 25) { Value = rol });
                }

                if (!string.IsNullOrWhiteSpace(cardCode))
                {
                    sql += " AND U_CardCode = @CardCode";
                    prm.Add(new SqlParameter("@CardCode", SqlDbType.NVarChar, 15) { Value = cardCode });
                }

                if (!string.IsNullOrWhiteSpace(isAdmin))
                {
                    string flag = isAdmin.ToUpperInvariant();
                    if (flag == "Y" || flag == "N")
                    {
                        sql += " AND U_IsAdmin = @IsAdmin";
                        prm.Add(new SqlParameter("@IsAdmin", SqlDbType.Char, 1) { Value = flag });
                    }
                }

                // Interpretació inicial d'"actiu": no caducat
                if (onlyActive.HasValue && onlyActive.Value)
                {
                    sql += " AND (U_ExpirationDate IS NULL OR U_ExpirationDate >= GETDATE())";
                }

                DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);

                var users = dt.AsEnumerable().Select(row => new
                {
                    code = row["Code"] != DBNull.Value ? Convert.ToInt32(row["Code"]) : 0,
                    name = row.Table.Columns.Contains("Name") && row["Name"] != DBNull.Value ? row["Name"].ToString() : null,
                    email = row["U_Email"] != DBNull.Value ? row["U_Email"].ToString() : null,
                    userName = row["U_UserName"] != DBNull.Value ? row["U_UserName"].ToString() : null,
                    cardCode = row["U_CardCode"] != DBNull.Value ? row["U_CardCode"].ToString() : null,
                    cardName = row["U_CardName"] != DBNull.Value ? row["U_CardName"].ToString() : null,
                    warehouse = row["U_Warehouse"] != DBNull.Value ? row["U_Warehouse"].ToString() : null,
                    role = row["U_Rol"] != DBNull.Value ? row["U_Rol"].ToString() : null,
                    isAdmin = row.Table.Columns.Contains("U_IsAdmin") && row["U_IsAdmin"] != DBNull.Value
                        ? row["U_IsAdmin"].ToString()
                        : "N",
                    startDate = row["U_StartDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["U_StartDate"]) : null,
                    expirationDate = row["U_ExpirationDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["U_ExpirationDate"]) : null,
                    lastAccess = row["U_LastAccess"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["U_LastAccess"]) : null
                }).ToList();

                return Ok(new
                {
                    success = true,
                    users
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Crea un usuari EMPLEAT via admin.
        /// Requereix que userToken sigui d'un Empleat amb U_IsAdmin = 'Y'.
        /// Per a Empleats, la SP fa servir Email + DNI per buscar a OHEM/OUSR.
        /// </summary>
        [HttpPost("CreateEmployeeUser")]
        public ActionResult CreateEmployeeUser([FromForm] string userToken,[FromForm] string email,[FromForm] string password,[FromForm] string dniNifCif,[FromForm] string isAdmin = "N")
        {
            if (!RequireMinGroup(userToken, "Admin", out _, out _, out var fail)) return fail!;

            try
            {
                var login = new Login();

                // Empleat → DocNum/DocDate no s’utilitzen a la SP
                string resultat = login.AdminInsertWebUser(
                    userToken,
                    email,
                    password,
                    dniNifCif,
                    null,          // DocNum
                    null,          // DocDate
                    "Empleat",
                    isAdmin
                );

                if (string.IsNullOrWhiteSpace(resultat))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "EMPTY_RESPONSE",
                        message = "AdminInsertWebUser ha retornat una resposta buida."
                    });
                }

                try
                {
                    var data = JsonConvert.DeserializeObject<object>(resultat);
                    return Ok(data);
                }
                catch
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "INVALID_JSON",
                        message = "Resposta no vàlida de Login.AdminInsertWebUser.",
                        raw = resultat
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a CreateEmployeeUser",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Crea un usuari CLIENT via admin.
        /// Requereix userToken d'admin i valida:
        ///   - Factura (DocNum/DocDate)
        ///   - NIF
        ///   - Email en algun contacte OCPR
        /// La SP s'encarrega d'omplir CardCode i validar-ho tot.
        /// </summary>
        [HttpPost("CreateClientUser")]
        public ActionResult CreateClientUser([FromForm] string userToken,[FromForm] string email,[FromForm] string password,[FromForm] string dniNifCif,[FromForm] int docNum,[FromForm] DateTime docDate)
        {
            try
            {
                var login = new Login();

                if (!RequireMinGroup(userToken, "Advanced", out _, out _, out var fail)) return fail!;


                string resultat = login.AdminInsertWebUser(
                    userToken,
                    email,
                    password,
                    dniNifCif,
                    docNum,
                    docDate,
                    "Client",
                    "N"       // Clients sempre N (la SP també ho força)
                );

                if (string.IsNullOrWhiteSpace(resultat))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "EMPTY_RESPONSE",
                        message = "AdminInsertWebUser ha retornat una resposta buida."
                    });
                }

                try
                {
                    var data = JsonConvert.DeserializeObject<object>(resultat);
                    return Ok(data);
                }
                catch
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "INVALID_JSON",
                        message = "Resposta no vàlida de Login.AdminInsertWebUser.",
                        raw = resultat
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a CreateClientUser",
                    detail = ex.Message
                });
            }
        }

        // ======================================================================
        // [MÒDUL 2] UserWebController.CreateClientUserByNifEmail
        // Objectiu (INTRANET - Opció 1):
        //   - Alta CLIENT validant per: Email + DNI/NIF + CardCode
        //   - Accessible per qualsevol Empleat logat (NO cal U_IsAdmin='Y')
        //   - Si password ve buit -> generem una password temporal i la retornem
        //     (la part del "link amb token per definir password" la farem després)
        // ======================================================================
        [HttpPost("CreateClientUserByNifEmail")]
        public ActionResult CreateClientUserByNifEmail([FromForm] string userToken,[FromForm] string email,[FromForm] string dniNifCif,[FromForm] string cardCode,[FromForm] string? password = null)
        {
            try
            {
                if (!RequireMinGroup(userToken, "Advanced", out _, out _, out var fail)) return fail!;

                // ======================================================================
                // [API-MÒDUL 1B] Validació de token (accepta token antic Login o token SAP)
                // - Primer intentem el token "antic" (Login)
                // - Si no passa, provem token SAP (SAPLogin)
                // ======================================================================
                var login = new Login();

                // 2) Validació mínima de camps (segons el teu requisit: Email + DNI + CardCode)
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(dniNifCif) || string.IsNullOrWhiteSpace(cardCode))
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "MISSING_DATA",
                        message = "Cal informar Email, DNI/NIF i CardCode."
                    });
                }

                // 3) Password: si ve buida, en generem una de temporal (per compatibilitat amb Laravel actual)
                bool generatedPassword = false;
                string passwordPlain = password ?? "";
                if (string.IsNullOrWhiteSpace(passwordPlain))
                {
                    passwordPlain = GenerateStrongPassword();
                    generatedPassword = true;
                }

                // 4) Connexió DOMENJÓ
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                // 5) Execució SP nova (Client per CardCode)
                var prm = new List<SqlParameter>
                {
                    new SqlParameter("Email", SqlDbType.VarChar, 250) { Value = email },
                    new SqlParameter("Password", SqlDbType.VarChar, 32) { Value = passwordPlain },
                    new SqlParameter("DniNifCif", SqlDbType.VarChar, 32) { Value = dniNifCif },
                    new SqlParameter("CardCode", SqlDbType.VarChar, 15) { Value = cardCode }
                };

                DataTable dt = DataAccess.ExecuteStoredProcedure(
                    Dades.ConnectionStringDOMENJO,
                    "dbo.XN_InsertWebUser_ClientByCardCode",
                    prm.ToArray()
                );

                if (dt.Rows.Count == 0)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "NO_RESULT",
                        message = "La SP no ha retornat cap fila."
                    });
                }

                DataRow row = dt.Rows[0];

                bool spSuccess = false;
                string spCode = "UNKNOWN";
                string spMessage = "Resposta desconeguda.";

                if (dt.Columns.Contains("Success") && row["Success"] != DBNull.Value)
                    spSuccess = Convert.ToInt32(row["Success"]) == 1;

                if (dt.Columns.Contains("Code") && row["Code"] != DBNull.Value)
                    spCode = row["Code"].ToString();

                if (dt.Columns.Contains("Message") && row["Message"] != DBNull.Value)
                    spMessage = row["Message"].ToString();

                // ======================================================================
                // [MÒDUL 4] Generació de token de configuració de password (setupToken)
                // - Només si la creació d'usuari ha anat bé (spSuccess = true)
                // - Desa identity a U_ForgotPassIdentity (taula @XNUSERWEB)
                // - Retorna setupToken + expiresUtc per poder enviar un link per email
                // ======================================================================
                string setupToken = null;
                DateTime? setupTokenExpiresUtc = null;

                if (spSuccess)
                {
                    // 1) Generem una identity única (cab dins nvarchar(50))
                    string forgotIdentity = Guid.NewGuid().ToString("N"); // 32 chars

                    // 2) Caducitat del token (48h) – ho ajustarem si vols, però ara deixem-ho fix
                    setupTokenExpiresUtc = DateTime.UtcNow.AddHours(48);

                    // 3) Guardem la identity al registre d'usuari
                    string sqlUpd = @"
                        UPDATE [dbo].[@XNUSERWEB]
                        SET U_ForgotPassIdentity = @Identity
                        WHERE U_Email = @Email;
                    ";

                                    var prmUpd = new List<SqlParameter>
                    {
                        new SqlParameter("@Identity", SqlDbType.NVarChar, 50) { Value = forgotIdentity },
                        new SqlParameter("@Email", SqlDbType.NVarChar, 250) { Value = email }
                    };

                    DataAccess.ExecuteQuery(Dades.ConnectionStringDOMENJO, sqlUpd, prmUpd.ToArray());

                    // 4) Construïm el token amb format: expires#email#identity i l'encriptem
                    var encrypt2 = new Encryption();
                    string raw = setupTokenExpiresUtc.Value.ToString("yyyyMMddHHmmss") + "#" + email + "#" + forgotIdentity;

                    // IMPORTANT: fem servir la mateixa key per encrypt/decrypt d'aquest token
                    setupToken = encrypt2.AES256_Encrypt(encrypt2.AES256_USER_Key, raw);
                }


                // 6) Resposta (si hem generat password, la retornem perquè la intranet pugui notificar)
                return Ok(new
                {
                    success = spSuccess,
                    code = spCode,
                    message = spMessage,
                    // Contrasenya: la podem generar internament per guardar hash,
                    // però NO s'ha d'exposar mai si el flux real és "setupToken"
                    generatedPassword = generatedPassword,
                    newPassword = (string)null,

                    setupToken = setupToken,
                    setupTokenExpiresUtc = setupTokenExpiresUtc

                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a CreateClientUserByNifEmail",
                    detail = ex.Message
                });
            }
        }

        // ======================================================================
        // [MÒDUL 5] UserWebController.SetPasswordBySetupToken
        // Objectiu (EXTRANET):
        //   - El client entra amb un link que porta setupToken
        //   - Defineix una nova contrasenya
        //   - No cal userToken ni old password
        // ======================================================================
        [HttpPost("SetPasswordBySetupToken")]
        public ActionResult SetPasswordBySetupToken([FromForm] string setupToken, [FromForm] string newPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(setupToken))
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "MISSING_TOKEN",
                        message = "Falta el setupToken."
                    });
                }

                if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Trim().Length < 8)
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "WEAK_PASSWORD",
                        message = "La nova contrasenya ha de tenir com a mínim 8 caràcters."
                    });
                }

                // Connexió DOMENJÓ
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                // 1) Desxifrem setupToken: expires#email#identity
                var encrypt = new Encryption();
                string decoded;

                try
                {
                    decoded = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, setupToken);
                }
                catch
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "TOKEN_DECRYPT_ERROR",
                        message = "setupToken no vàlid (no es pot desxifrar)."
                    });
                }

                var parts = decoded.Split('#');
                if (parts.Length != 3)
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "TOKEN_FORMAT_ERROR",
                        message = "setupToken no vàlid (format incorrecte)."
                    });
                }

                var expiresTxt = parts[0];
                var email = parts[1];
                var identity = parts[2];

                DateTime expiresUtc;
                try
                {
                    expiresUtc = DateTime.ParseExact(
                        expiresTxt,
                        "yyyyMMddHHmmss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal
                    );
                }
                catch
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "TOKEN_DATE_ERROR",
                        message = "setupToken no vàlid (data incorrecta)."
                    });
                }

                if (DateTime.UtcNow > expiresUtc)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        code = "TOKEN_EXPIRED",
                        message = "El link ha caducat. Demana'n un de nou."
                    });
                }

                // 2) Update password i invalidem el token (un sol ús)
                // 2) Update password i invalidem el token (un sol ús)
                // IMPORTANT: fem servir el mateix hashing que el login (GetSHA256 -> HEX -> bytes)
                string passwordHex = encrypt.GetSHA256(newPassword); // 64 chars HEX (SHA256)
                int len = passwordHex.Length / 2;
                byte[] passwordBytes = new byte[len];
                for (int i = 0; i < len; i++)
                {
                    passwordBytes[i] = Convert.ToByte(passwordHex.Substring(i * 2, 2), 16);
                }

                string sql = @"
                UPDATE [dbo].[@XNUSERWEB]
                SET
                    U_Password = @Password,
                    U_ForgotPassIdentity = NULL
                WHERE
                    U_Email = @Email
                    AND U_ForgotPassIdentity = @Identity;

                SELECT @@ROWCOUNT AS Rows;
            ";

                            var prm = new List<SqlParameter>
            {
                new SqlParameter("@Password", SqlDbType.VarBinary, passwordBytes.Length) { Value = passwordBytes },
                new SqlParameter("@Email", SqlDbType.NVarChar, 250) { Value = email },
                new SqlParameter("@Identity", SqlDbType.NVarChar, 50) { Value = identity }
            };


                DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);

                int rows = 0;
                if (dt.Rows.Count > 0 && dt.Columns.Contains("Rows") && dt.Rows[0]["Rows"] != DBNull.Value)
                    rows = Convert.ToInt32(dt.Rows[0]["Rows"]);

                if (rows == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        code = "TOKEN_NOT_MATCH",
                        message = "El link no és vàlid o ja s'ha utilitzat."
                    });
                }

                return Ok(new
                {
                    success = true,
                    code = "OK",
                    message = "Contrasenya establerta correctament."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a SetPasswordBySetupToken",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Genera un nou setupToken per un email existent (reenviament / auto-servei).
        /// - Sempre retorna 200 per evitar enumeració d'usuaris.
        /// - Si l'email no existeix: setupToken = null.
        /// </summary>
        [HttpPost("RequestSetupTokenByEmail")]
        public ActionResult RequestSetupTokenByEmail([FromForm] string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Ok(new
                    {
                        success = true,
                        code = "OK",
                        message = "Si existeix un compte amb aquest correu, rebràs un enllaç per configurar la contrasenya.",
                        setupToken = (string)null,
                        setupTokenExpiresUtc = (DateTime?)null
                    });
                }

                // Connexió DOMENJÓ
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                // 1) Generem identity i caducitat
                string forgotIdentity = Guid.NewGuid().ToString("N"); // 32 chars
                DateTime expiresUtc = DateTime.UtcNow.AddHours(48);

                // 2) Guardem la identity (si l'email existeix)
                string sql = @"
                    UPDATE [dbo].[@XNUSERWEB]
                    SET U_ForgotPassIdentity = @Identity
                    WHERE U_Email = @Email;

                    SELECT @@ROWCOUNT AS Rows;
                ";

                var prm = new List<SqlParameter>
                {
                    new SqlParameter("@Identity", SqlDbType.NVarChar, 50) { Value = forgotIdentity },
                    new SqlParameter("@Email", SqlDbType.NVarChar, 250) { Value = email.Trim() }
                };

                DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);

                int rows = 0;
                if (dt.Rows.Count > 0 && dt.Columns.Contains("Rows") && dt.Rows[0]["Rows"] != DBNull.Value)
                    rows = Convert.ToInt32(dt.Rows[0]["Rows"]);

                // 3) Si no existeix -> resposta neutra sense token
                if (rows == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        code = "OK",
                        message = "Si existeix un compte amb aquest correu, rebràs un enllaç per configurar la contrasenya.",
                        setupToken = (string)null,
                        setupTokenExpiresUtc = (DateTime?)null
                    });
                }

                // 4) Construïm el token: expires#email#identity i l'encriptem
                var encrypt = new Encryption();
                string raw = expiresUtc.ToString("yyyyMMddHHmmss") + "#" + email.Trim() + "#" + forgotIdentity;
                string setupToken = encrypt.AES256_Encrypt(encrypt.AES256_USER_Key, raw);

                return Ok(new
                {
                    success = true,
                    code = "OK",
                    message = "Si existeix un compte amb aquest correu, rebràs un enllaç per configurar la contrasenya.",
                    setupToken = setupToken,
                    setupTokenExpiresUtc = expiresUtc
                });
            }
            catch (Exception ex)
            {
                // També resposta neutra (sense detalls) per no filtrar informació
                return Ok(new
                {
                    success = true,
                    code = "OK",
                    message = "Si existeix un compte amb aquest correu, rebràs un enllaç per configurar la contrasenya.",
                    setupToken = (string)null,
                    setupTokenExpiresUtc = (DateTime?)null
                });
            }
        }


        /// <summary>
        /// Actualitza dades bàsiques d'un usuari web.
        /// Només per admins (Empleat amb U_IsAdmin = 'Y').
        /// De moment actualitza: Name, Email, UserName, CardCode, CardName, Warehouse, Rol, IsAdmin, Start/Expiration.
        /// </summary>
        [HttpPost("UpdateUser")]
        public ActionResult UpdateUser([FromForm] string userToken,[FromForm] int code,[FromForm] string? name,[FromForm] string? email,[FromForm] string? userName,[FromForm] string? cardCode,
            [FromForm] string? cardName,[FromForm] string? warehouse,[FromForm] string? rol,[FromForm] string? isAdmin,[FromForm] DateTime? startDate,[FromForm] DateTime? expirationDate)
        {
            try
            {
                if (!RequireMinGroup(userToken, "Admin", out _, out _, out var fail)) return fail!;

                var login = new Login();

                // Validació d'admin
                if (!login.ValidateUserToken(userToken))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        code = "INVALID_TOKEN",
                        message = "Token d'usuari no vàlid o caducat."
                    });
                }

                var adminUser = login.GetUserInfo(userToken);
                if (adminUser == null || adminUser.role != "Empleat" || adminUser.isAdmin != "Y")
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        code = "NOT_AUTHORIZED",
                        message = "L'usuari no té permisos d'administrador."
                    });
                }


                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                // Normalització de rol i isAdmin
                string finalRol = rol;
                string finalIsAdmin = isAdmin;

                if (!string.IsNullOrWhiteSpace(finalIsAdmin))
                {
                    finalIsAdmin = finalIsAdmin.ToUpperInvariant();
                    if (finalIsAdmin != "Y" && finalIsAdmin != "N")
                        finalIsAdmin = "N";
                }

                // Si el rol és Client → forcem isAdmin = 'N'
                if (finalRol == "Client")
                {
                    finalIsAdmin = "N";
                }

                string sql = @"
                    UPDATE [dbo].[@XNUSERWEB]
                    SET 
                        Name            = @Name,
                        U_Email         = @Email,
                        U_UserName      = @UserName,
                        U_CardCode      = @CardCode,
                        U_CardName      = @CardName,
                        U_Warehouse     = @Warehouse,
                        U_Rol           = @Rol,
                        U_IsAdmin       = @IsAdmin,
                        U_StartDate     = @StartDate,
                        U_ExpirationDate= @ExpirationDate
                    WHERE Code = @Code;
                    ";

                var prm = new List<SqlParameter>
                {
                    new SqlParameter("@Code", SqlDbType.Int) { Value = code },
                    new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = (object?)name ?? DBNull.Value },
                    new SqlParameter("@Email", SqlDbType.NVarChar, 100) { Value = (object?)email ?? DBNull.Value },
                    new SqlParameter("@UserName", SqlDbType.NVarChar, 100) { Value = (object?)userName ?? DBNull.Value },
                    new SqlParameter("@CardCode", SqlDbType.NVarChar, 15) { Value = (object?)cardCode ?? DBNull.Value },
                    new SqlParameter("@CardName", SqlDbType.NVarChar, 100) { Value = (object?)cardName ?? DBNull.Value },
                    new SqlParameter("@Warehouse", SqlDbType.NVarChar, 8) { Value = (object?)warehouse ?? DBNull.Value },
                    new SqlParameter("@Rol", SqlDbType.NVarChar, 25) { Value = (object?)finalRol ?? DBNull.Value },
                    new SqlParameter("@IsAdmin", SqlDbType.Char, 1) { Value = (object?)finalIsAdmin ?? DBNull.Value },
                    new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = (object?)startDate ?? DBNull.Value },
                    new SqlParameter("@ExpirationDate", SqlDbType.DateTime) { Value = (object?)expirationDate ?? DBNull.Value }
                };

                DataAccess.ExecuteQuery(Dades.ConnectionStringDOMENJO, sql, prm.ToArray());


                return Ok(new
                {
                    success = true,
                    code = "OK",
                    message = "Usuari actualitzat correctament."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a UpdateUser",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Reset de contrasenya d'un usuari (admin).
        /// Genera una nova contrasenya forta, actualitza U_Password (SHA2_256 → varbinary(32))
        /// i retorna la contrasenya en pla per poder-la enviar per correu.
        /// </summary>
        [HttpPost("ResetPassword")]
        public ActionResult ResetPassword([FromForm] string userToken,[FromForm] int code)
        {
            try
            {
                if (!RequireMinGroup(userToken, "Admin", out _, out _, out var fail)) return fail!;

                var login = new Login();

                // Validació d'admin
                if (!login.ValidateUserToken(userToken))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        code = "INVALID_TOKEN",
                        message = "Token d'usuari no vàlid o caducat."
                    });
                }

                var adminUser = login.GetUserInfo(userToken);
                if (adminUser == null || adminUser.role != "Empleat" || adminUser.isAdmin != "Y")
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        code = "NOT_AUTHORIZED",
                        message = "L'usuari no té permisos d'administrador."
                    });
                }


                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                // 1️⃣ Generem una nova contrasenya forta
                string newPasswordPlain = GenerateStrongPassword();

                // 2️⃣ Calculem el SHA256 en HEX, igual que getTokenLogin
                var encrypt = new Encryption();
                string passwordHex = encrypt.GetSHA256(newPasswordPlain);

                // 3️⃣ Convertim HEX → byte[] per varbinary(32)
                int len = passwordHex.Length / 2;
                byte[] passwordBytes = new byte[len];
                for (int i = 0; i < len; i++)
                {
                    passwordBytes[i] = Convert.ToByte(passwordHex.Substring(i * 2, 2), 16);
                }

                string sql = @"
                    UPDATE [dbo].[@XNUSERWEB]
                    SET U_Password = @Password
                    WHERE Code = @Code;
                    ";

                var prm = new List<SqlParameter>
                {
                    new SqlParameter("@Code", SqlDbType.Int) { Value = code },
                    new SqlParameter("@Password", SqlDbType.VarBinary, passwordBytes.Length) { Value = passwordBytes }
                };

                DataAccess.ExecuteQuery(Dades.ConnectionStringDOMENJO, sql, prm.ToArray());

                return Ok(new
                {
                    success = true,
                    code = "OK",
                    message = "Contrasenya resetejada correctament.",
                    newPassword = newPasswordPlain
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a ResetPassword",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Genera una contrasenya forta amb lletres majúscules, minúscules, números i símbols.
        /// </summary>
        private string GenerateStrongPassword(int length = 12)
        {
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string digits = "23456789";
            const string symbols = "!@$%?&";

            string all = lower + upper + digits + symbols;
            var rnd = new Random();

            char[] pwd = new char[length];
            for (int i = 0; i < length; i++)
            {
                pwd[i] = all[rnd.Next(all.Length)];
            }

            return new string(pwd);
        }

        /// <summary>
        /// Rep una sol·licitud d’alta (normalment des de l’extranet)
        /// i la desa en la UDT @XNWEBREG amb estat PENDENT.
        /// No requereix userToken.
        /// </summary>
        [HttpPost("RequestRegistration")]
        public ActionResult RequestRegistration([FromForm] string email,[FromForm] string fullName,[FromForm] string dniNifCif,[FromForm] int? docNum,[FromForm] DateTime? docDate,[FromForm] string? phone)
        {
            try
            {

                if (string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "MISSING_EMAIL",
                        message = "Cal informar un email."
                    });
                }

                // Assegurem connexió DOMENJÓ
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                // Generem Code numèric com a text i inserim la sol·licitud
                string sql = @"
                    DECLARE @NewCode NVARCHAR(20);

                    SELECT @NewCode =
                        RIGHT('0000000000' + CAST(ISNULL(MAX(CAST(Code AS INT)), 0) + 1 AS VARCHAR(10)), 10)
                    FROM [dbo].[@XNWEBREG];

                    IF @NewCode IS NULL
                        SET @NewCode = '0000000001';

                    INSERT INTO [dbo].[@XNWEBREG]
                        (Code, Name,
                         U_Email, U_FullName, U_Phone, U_DniNifCif,
                         U_DocNum, U_DocDate, U_Role, U_Status,
                         U_CardCode, U_CardName,
                         U_CreatedAt, U_UpdatedAt,
                         U_ProcessedByUserCode, U_ProcessedAt, U_RejectReason)
                    VALUES
                        (@NewCode, @NewCode,
                         @Email, @FullName, @Phone, @DniNifCif,
                         @DocNum, @DocDate, @Role, 'PENDENT',
                         NULL, NULL,
                         GETDATE(), NULL,
                         NULL, NULL, NULL);

                    SELECT @NewCode AS Code;
                    ";

                var prm = new List<SqlParameter>
                {
                    new SqlParameter("@Email", SqlDbType.NVarChar, 250) { Value = email },
                    new SqlParameter("@FullName", SqlDbType.NVarChar, 100) { Value = (object?)fullName ?? DBNull.Value },
                    new SqlParameter("@Phone", SqlDbType.NVarChar, 50) { Value = (object?)phone ?? DBNull.Value },
                    new SqlParameter("@DniNifCif", SqlDbType.NVarChar, 32) { Value = (object?)dniNifCif ?? DBNull.Value },
                    new SqlParameter("@DocNum", SqlDbType.Int) { Value = (object?)docNum ?? DBNull.Value },
                    new SqlParameter("@DocDate", SqlDbType.DateTime) { Value = (object?)docDate ?? DBNull.Value },
                    new SqlParameter("@Role", SqlDbType.NVarChar, 25) { Value = "Client" }
                };

                DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);

                string newCode = null;
                if (dt.Rows.Count > 0 && dt.Columns.Contains("Code"))
                {
                    newCode = dt.Rows[0]["Code"]?.ToString();
                }

                return Ok(new
                {
                    success = true,
                    code = "OK",
                    message = "Sol·licitud d'alta rebuda correctament.",
                    requestCode = newCode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a RequestRegistration",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Llista sol·licituds d’alta de la UDT @XNWEBREG.
        /// Requereix userToken d'un Empleat admin (U_IsAdmin = 'Y').
        /// Filtres opcionals: status, email, cardCode.
        /// </summary>
        [HttpGet("ListRegistrationRequests")]
        public ActionResult ListRegistrationRequests([FromQuery] string userToken,[FromQuery] string? status,[FromQuery] string? email,[FromQuery] string? cardCode)
        {
            try
            {
                if (!RequireMinGroup(userToken, "Advanced", out _, out _, out var fail)) return fail!;

                // ======================================================================
                // [API-MÒDUL 2] Validació de token (accepta token antic Login o token SAP)
                // ======================================================================
                var login = new Login();

                bool isEmployee = false;

                // Per a ProcessedByUserCode (si tenim token antic)
                int? processedByUserCode = null;

                // 1) Token antic (Login)
                if (login.ValidateUserToken(userToken))
                {
                    var empUser = login.GetUserInfo(userToken);
                    if (empUser != null && empUser.role == "Empleat")
                    {
                        isEmployee = true;
                        processedByUserCode = empUser.code; // per si ho necessites en el futur
                    }
                }
                else
                {
                    // 2) Token SAP (SAPLogin)
                    if (TryDecodeSapUserToken(userToken, out var sapUserCode, out var sapUserId, out var sapExpiresUtc))
                    {
                        isEmployee = true;
                        processedByUserCode = sapUserId;
                    }
                    else
                    {
                        return Unauthorized(new
                        {
                            success = false,
                            code = "INVALID_TOKEN",
                            message = "Token d'usuari no vàlid o caducat."
                        });
                    }
                }

                if (!isEmployee)
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        code = "NOT_AUTHORIZED",
                        message = "L'usuari no té permisos d'empleat."
                    });
                }


                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                string sql = "SELECT * FROM [dbo].[@XNWEBREG] WHERE 1=1";
                var prm = new List<SqlParameter>();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    sql += " AND U_Status = @Status";
                    prm.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 20) { Value = status });
                }

                if (!string.IsNullOrWhiteSpace(email))
                {
                    sql += " AND U_Email LIKE @Email";
                    prm.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 250) { Value = "%" + email + "%" });
                }

                if (!string.IsNullOrWhiteSpace(cardCode))
                {
                    sql += " AND U_CardCode = @CardCode";
                    prm.Add(new SqlParameter("@CardCode", SqlDbType.NVarChar, 15) { Value = cardCode });
                }

                DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);

                var requests = dt.AsEnumerable().Select(row => new
                {
                    code = row["Code"]?.ToString(),
                    email = row["U_Email"]?.ToString(),
                    fullName = row["U_FullName"]?.ToString(),
                    phone = row["U_Phone"]?.ToString(),
                    dniNifCif = row["U_DniNifCif"]?.ToString(),
                    docNum = row["U_DocNum"] != DBNull.Value ? (int?)Convert.ToInt32(row["U_DocNum"]) : null,
                    docDate = row["U_DocDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["U_DocDate"]) : null,
                    role = row["U_Role"]?.ToString(),
                    status = row["U_Status"]?.ToString(),
                    cardCode = row["U_CardCode"]?.ToString(),
                    cardName = row["U_CardName"]?.ToString(),
                    createdAt = row["U_CreatedAt"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["U_CreatedAt"]) : null,
                    updatedAt = row["U_UpdatedAt"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["U_UpdatedAt"]) : null,
                    processedByUserCode = row["U_ProcessedByUserCode"] != DBNull.Value ? (int?)Convert.ToInt32(row["U_ProcessedByUserCode"]) : null,
                    processedAt = row["U_ProcessedAt"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["U_ProcessedAt"]) : null,
                    rejectReason = row["U_RejectReason"]?.ToString()
                }).ToList();

                return Ok(new
                {
                    success = true,
                    requests
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a ListRegistrationRequests",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Aprova una sol·licitud d’alta:
        ///   - Crea l'usuari real (normalment Client) via AdminInsertWebUser / XN_InsertWebUser
        ///   - Genera una contrasenya forta
        ///   - Marca la sol·licitud com APROVAT
        /// Retorna la nova contrasenya en pla per poder-la enviar per correu.
        /// </summary>
        [HttpPost("ApproveRegistration")]
        public ActionResult ApproveRegistration([FromForm] string userToken, [FromForm] string requestCode)
        {
            try
            {
                if (!RequireMinGroup(userToken, "Advanced", out var processedByUserCode, out _, out var fail)) return fail!;

                var login = new Login();

                // Connexió DOMENJÓ
                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                // ==========================================================
                // [MÒDUL 8.2.1] Recuperem la sol·licitud @XNWEBREG
                // ==========================================================
                string sqlSelect = "SELECT TOP 1 * FROM [dbo].[@XNWEBREG] WHERE Code = @Code";
                var prmSelect = new List<SqlParameter>
                {
                    new SqlParameter("@Code", SqlDbType.NVarChar, 20) { Value = requestCode }
                };

                DataTable dtReq = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sqlSelect, prmSelect);

                if (dtReq.Rows.Count == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        code = "REQUEST_NOT_FOUND",
                        message = "No s'ha trobat la sol·licitud indicada."
                    });
                }

                DataRow r = dtReq.Rows[0];
                string status = r["U_Status"]?.ToString();

                if (!string.Equals(status, "PENDENT", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "INVALID_STATUS",
                        message = "Només es poden aprovar sol·licituds en estat PENDENT."
                    });
                }

                // Dades de la sol·licitud
                string email = r["U_Email"]?.ToString();
                string fullName = r["U_FullName"]?.ToString();
                string dniNifCif = r["U_DniNifCif"]?.ToString();
                string role = r["U_Role"]?.ToString() ?? "Client";

                int? docNum = r["U_DocNum"] != DBNull.Value ? (int?)Convert.ToInt32(r["U_DocNum"]) : null;
                DateTime? docDate = r["U_DocDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["U_DocDate"]) : null;

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(dniNifCif) || !docNum.HasValue || !docDate.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "MISSING_DATA",
                        message = "La sol·licitud no té prou dades (email, NIF, DocNum, DocDate) per crear l'usuari."
                    });
                }

                // ==========================================================
                // [MÒDUL 8.2.2] Creem l'usuari amb password TEMPORAL intern
                // IMPORTANT:
                //  - La SP requereix Password
                //  - NO retornem password en pla a la intranet
                //  - El client l'establirà via setupToken (one-time)
                // ==========================================================
                string tempPasswordPlain = GenerateStrongPassword();

                string resultat = login.AdminInsertWebUser(
                    userToken,
                    email,
                    tempPasswordPlain,   // password temporal intern (NO s'exposa)
                    dniNifCif,
                    docNum,
                    docDate,
                    role,
                    "N"
                );

                if (string.IsNullOrWhiteSpace(resultat))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "EMPTY_RESPONSE",
                        message = "AdminInsertWebUser ha retornat una resposta buida."
                    });
                }

                bool spSuccess = false;
                string spCode = null;
                string spMessage = null;

                try
                {
                    var jo = JObject.Parse(resultat);
                    spSuccess = jo["success"]?.Value<bool>() ?? false;
                    spCode = jo["code"]?.Value<string>();
                    spMessage = jo["message"]?.Value<string>();
                }
                catch
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        code = "INVALID_JSON",
                        message = "Resposta no vàlida de Login.AdminInsertWebUser.",
                        raw = resultat
                    });
                }

                if (!spSuccess)
                {
                    // No toquem la sol·licitud; seguim en PENDENT
                    return BadRequest(new
                    {
                        success = false,
                        code = spCode ?? "CREATE_USER_ERROR",
                        message = spMessage ?? "Error en crear l'usuari des de la sol·licitud."
                    });
                }

                // ==========================================================
                // [MÒDUL 8.2.3] Generem setupToken (igual que CreateClientUserByNifEmail)
                //  - Guardem U_ForgotPassIdentity
                //  - Token format: expires#email#identity (xifrat AES256_USER_Key)
                // ==========================================================
                string setupToken = null;
                DateTime? setupTokenExpiresUtc = null;

                string forgotIdentity = Guid.NewGuid().ToString("N"); // 32 chars
                setupTokenExpiresUtc = DateTime.UtcNow.AddHours(48);

                string sqlUpd = @"
                    UPDATE [dbo].[@XNUSERWEB]
                    SET U_ForgotPassIdentity = @Identity
                    WHERE U_Email = @Email;
                ";

                var prmUpd = new List<SqlParameter>
                {
                    new SqlParameter("@Identity", SqlDbType.NVarChar, 50) { Value = forgotIdentity },
                    new SqlParameter("@Email", SqlDbType.NVarChar, 250) { Value = email }
                };

                DataAccess.ExecuteQuery(Dades.ConnectionStringDOMENJO, sqlUpd, prmUpd.ToArray());

                var encrypt2 = new Encryption();
                string raw = setupTokenExpiresUtc.Value.ToString("yyyyMMddHHmmss") + "#" + email + "#" + forgotIdentity;
                setupToken = encrypt2.AES256_Encrypt(encrypt2.AES256_USER_Key, raw);

                // ==========================================================
                // [MÒDUL 8.2.4] Marquem la sol·licitud com APROVAT
                // ==========================================================
                string sqlUpdate = @"
                    UPDATE [dbo].[@XNWEBREG]
                    SET 
                        U_Status = 'APROVAT',
                        U_UpdatedAt = GETDATE(),
                        U_ProcessedAt = GETDATE(),
                        U_ProcessedByUserCode = @ProcessedByUserCode
                    WHERE Code = @Code;
                ";

                var prmUpdate = new[]
                {
                    new SqlParameter("@Code", SqlDbType.NVarChar, 20) { Value = requestCode },
                    new SqlParameter("@ProcessedByUserCode", SqlDbType.Int) { Value = processedByUserCode }
                };

                DataAccess.ExecuteQuery(Dades.ConnectionStringDOMENJO, sqlUpdate, prmUpdate);

                // ==========================================================
                // [MÒDUL 8.2.5] Retorn: setupToken (NO password en pla)
                // ==========================================================
                return Ok(new
                {
                    success = true,
                    code = "OK",
                    message = "Sol·licitud aprovada i usuari creat correctament.",

                    // Perquè la intranet pugui enviar el correu sense “inventar” res
                    email = email,
                    fullName = fullName,

                    // setup token (link de set-password)
                    setupToken = setupToken,
                    setupTokenExpiresUtc = setupTokenExpiresUtc,

                    // info SP (diagnòstic)
                    spCode,
                    spMessage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a ApproveRegistration",
                    detail = ex.Message
                });
            }
        }


        /// <summary>
        /// Rebutja una sol·licitud d’alta a la UDT @XNWEBREG.
        /// Només per admins. Marca U_Status = 'REBUTJAT' i desa el motiu.
        /// </summary>
        [HttpPost("RejectRegistration")]
        public ActionResult RejectRegistration([FromForm] string userToken,[FromForm] string requestCode,[FromForm] string? reason)
        {
            try
            {
                if (!RequireMinGroup(userToken, "Advanced", out var processedByUserCode, out _, out var fail)) return fail!;

                // ==========================================================
                // [API-MÒDUL 4] Validació token (accepta token Login o token SAP)
                // i obtenim ProcessedByUserCode (int) per guardar a @XNWEBREG
                // ==========================================================
                var login = new Login();

                if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
                {
                    Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                    Dades.SetupDades();
                }

                string sql = @"
                    UPDATE [dbo].[@XNWEBREG]
                    SET 
                        U_Status = 'REBUTJAT',
                        U_UpdatedAt = GETDATE(),
                        U_ProcessedAt = GETDATE(),
                        U_ProcessedByUserCode = @ProcessedByUserCode,
                        U_RejectReason = @Reason
                    WHERE Code = @Code;
                    ";

                var prm = new[]
                {
                    new SqlParameter("@Code", SqlDbType.NVarChar, 20) { Value = requestCode },
                    new SqlParameter("@ProcessedByUserCode", SqlDbType.Int) { Value = processedByUserCode },
                    new SqlParameter("@Reason", SqlDbType.NVarChar, 255) { Value = (object?)reason ?? DBNull.Value }
                };

                DataAccess.ExecuteQuery(Dades.ConnectionStringDOMENJO, sql, prm);

                return Ok(new
                {
                    success = true,
                    code = "OK",
                    message = "Sol·licitud rebutjada correctament."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    code = "EXCEPTION",
                    message = "Error intern a RejectRegistration",
                    detail = ex.Message
                });
            }
        }

        // ======================================================================
        // AUTH GROUPS (OUGR) - Escala: Admin > Advanced > Standard > Web
        // ======================================================================
        private static int GroupLevel(string? groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return 1; // sense grup -> Web (mínim)
            var g = groupName.Trim();

            if (g.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return 4;
            if (g.Equals("Advanced", StringComparison.OrdinalIgnoreCase)) return 3;
            if (g.Equals("Standard", StringComparison.OrdinalIgnoreCase)) return 2;
            if (g.Equals("Web", StringComparison.OrdinalIgnoreCase)) return 1;

            // Desconegut -> el tractem com a mínim
            return 1;
        }

        private bool TryGetEmployeeAuthFromToken(string userToken,out int processedByUserCode,out string effectiveGroupName,out int effectiveGroupLevel,out ActionResult? failResult)
        {
            processedByUserCode = 0;
            effectiveGroupName = "Web";
            effectiveGroupLevel = 1;
            failResult = null;

            // Connexió
            if (string.IsNullOrEmpty(Dades.ConnectionStringDOMENJO))
            {
                Dades.DOMENJO_BBDD = "SBO_DOMENJO";
                Dades.SetupDades();
            }

            // 1) Primer provem token SAP (USER_CODE#USERID#yyyyMMddHHmmss)
            if (TryDecodeSapUserToken(userToken, out var sapUserCode, out var sapUserId, out var sapExpiresUtc))
            {
                processedByUserCode = sapUserId;

                // Llegim tots els grups del user i ens quedem amb el màxim
                string sql = @"
            SELECT G.GroupName
            FROM USR7 U7
            INNER JOIN OUGR G ON G.GroupId = U7.GroupId
            WHERE U7.UserId = @UserId;
        ";

                var prm = new List<SqlParameter>
        {
            new SqlParameter("@UserId", SqlDbType.Int) { Value = sapUserId }
        };

                DataTable dt = DataAccess.GetTmpDataTable(Dades.ConnectionStringDOMENJO, sql, prm);

                string bestGroup = "Web";
                int bestLevel = 1;

                if (dt != null && dt.Rows.Count > 0 && dt.Columns.Contains("GroupName"))
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        var gn = r["GroupName"]?.ToString();
                        var lvl = GroupLevel(gn);
                        if (lvl > bestLevel)
                        {
                            bestLevel = lvl;
                            bestGroup = string.IsNullOrWhiteSpace(gn) ? "Web" : gn!;
                        }
                    }
                }

                effectiveGroupName = bestGroup;
                effectiveGroupLevel = bestLevel;
                return true;
            }

            // 2) Si NO és token SAP, provem token antic (@XNUSERWEB)
            var login = new Login();
            if (!login.ValidateUserToken(userToken))
            {
                failResult = Unauthorized(new
                {
                    success = false,
                    code = "INVALID_TOKEN",
                    message = "Token d'usuari no vàlid o caducat."
                });
                return false;
            }

            var empUser = login.GetUserInfo(userToken);
            if (empUser == null || empUser.role != "Empleat")
            {
                failResult = StatusCode(403, new
                {
                    success = false,
                    code = "NOT_AUTHORIZED",
                    message = "L'usuari no té permisos d'empleat."
                });
                return false;
            }

            processedByUserCode = empUser.code;

            // Sistema antic només tenia isAdmin Y/N -> mapegem a Admin/Standard
            effectiveGroupName = (empUser.isAdmin == "Y") ? "Admin" : "Standard";
            effectiveGroupLevel = GroupLevel(effectiveGroupName);
            return true;
        }

        private bool RequireMinGroup(string userToken, string requiredGroup, out int processedByUserCode, out string effectiveGroupName, out ActionResult? failResult)
        {
            processedByUserCode = 0;
            effectiveGroupName = "Web";
            failResult = null;

            if (!TryGetEmployeeAuthFromToken(userToken, out processedByUserCode, out effectiveGroupName, out var lvl, out var fail))
            {
                failResult = fail;
                return false;
            }

            int requiredLevel = GroupLevel(requiredGroup);

            if (lvl < requiredLevel)
            {
                failResult = StatusCode(403, new
                {
                    success = false,
                    code = "NOT_AUTHORIZED",
                    message = $"No tens permisos. Requerit: {requiredGroup}. Tens: {effectiveGroupName}."
                });
                return false;
            }

            return true;
        }

        // ======================================================================
        // [API-MÒDUL 1A] Helper - Validar i decodificar un userToken de SAPLogin
        // Format desencriptat (AES256_USER_Key):
        //   USER_CODE#USERID#yyyyMMddHHmmss
        // ======================================================================
        private bool TryDecodeSapUserToken(string userToken, out string userCode, out int userId, out DateTime expiresUtc)
        {
            userCode = null;
            userId = 0;
            expiresUtc = default;

            try
            {
                var encrypt = new Encryption();

                // Mateix pas que fa SAPLoginService.ValidateUserToken
                userToken = encrypt.TokenModify(userToken);

                string decoded = encrypt.AES256_Decrypt(encrypt.AES256_USER_Key, userToken);
                var parts = decoded.Split('#');

                if (parts.Length < 3) return false;

                userCode = parts[0];

                if (!int.TryParse(parts[1], out userId)) return false;

                if (!DateTime.TryParseExact(
                        parts[2],
                        "yyyyMMddHHmmss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out expiresUtc))
                {
                    return false;
                }

                if (DateTime.UtcNow > expiresUtc) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }


    }
}

