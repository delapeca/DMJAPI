using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using System;
using System.Data;
using XNDmjApi.Functions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace XNDmjApi.Services
{
    public class BoyumEmailService
    {
        public class EmailResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        public EmailResult SendDocumentEmail(int docEntry, int objectType, string? emailAddress = null)
        {
            try
            {
                // Obtenir el Report Action code segons el tipus de document
                string actionCode = GetReportActionCode(objectType);
                if (string.IsNullOrEmpty(actionCode))
                {
                    return new EmailResult
                    {
                        Success = false,
                        Message = $"No s'ha trobat configuració per ObjectType {objectType}"
                    };
                }

                // Executar el stored procedure de Boyum
                string connectionString = Dades.ConnectionStringDOMENJO;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("B1Print_ExecuteAction", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ObjectType", objectType);
                        cmd.Parameters.AddWithValue("@DocEntry", docEntry);
                        cmd.Parameters.AddWithValue("@ActionCode", actionCode);

                        // Si s'especifica email, afegir-lo com a paràmetre
                        if (!string.IsNullOrEmpty(emailAddress))
                        {
                            cmd.Parameters.AddWithValue("@EmailAddress", emailAddress);
                        }

                        cmd.ExecuteNonQuery();
                    }
                }

                return new EmailResult
                {
                    Success = true,
                    Message = "Email enviat correctament"
                };
            }
            catch (SqlException ex) when (ex.Message.Contains("Could not find stored procedure"))
            {
                // El stored procedure no existeix, provar amb un nom alternatiu
                return TryAlternativeMethod(docEntry, objectType, emailAddress);
            }
            catch (Exception ex)
            {
                return new EmailResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        private string GetReportActionCode(int objectType)
        {
            // Mapeig dels ObjectTypes a les configuracions de Boyum
            // Basat en les teves configuracions (BOY_85_REPORTCONFIG)

            string boyumType = objectType switch
            {
                23 => "00000000",  // Quotation (Oferta)
                17 => "00000001",  // Order (Pedido)
                15 => "00000002",  // Delivery (Entrega)
                16 => "00000003",  // Return (Devolución)
                21 => "00000042",  // Sales Return Request
                13 => "00000006",  // Invoice (Factura) ⭐
                14 => "00000009",  // Credit Note (Abono)
                22 => "00000010",  // Purchase Order (Pedido compra)
                _ => null
            };

            if (boyumType == null) return null;

            // Consultar quin Report Action està configurat per aquest tipus
            string query = @"
                SELECT TOP 1 T1.U_BOY_EMAIL
                FROM [@BOY_85_REP_CONFIG] T0
                INNER JOIN [@BOY_85_REP_CONFIGL] T1 ON T0.DocEntry = T1.DocEntry
                WHERE T0.U_BOY_TYPE = @BoyumType 
                  AND T0.U_BOY_ACTIVE = 'Y'
                  AND T1.U_BOY_EMAIL IS NOT NULL
            ";

            string[] parametres = { $"BoyumType:{boyumType}" };
            DataTable dt = DataAccess.GetDataTable(Dades.ConnectionStringDOMENJO, query, parametres);

            if (dt != null && dt.Rows.Count > 0)
            {
                return dt.Rows[0]["U_BOY_EMAIL"]?.ToString();
            }

            // Fallback: intentar amb el Report Action per defecte
            return "RA-D004";  // Email Document Report (per defecte)
        }

        private EmailResult TryAlternativeMethod(int docEntry, int objectType, string? emailAddress)
        {
            // Noms alternatius de stored procedures de Boyum
            string[] possibleSPNames = new[]
            {
                "B1Print_SendEmail",
                "B1UP_SendDocument",
                "BOY_SendEmail"
            };

            string connectionString = Dades.ConnectionStringDOMENJO;

            foreach (string spName in possibleSPNames)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        using (SqlCommand cmd = new SqlCommand(spName, conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@ObjectType", objectType);
                            cmd.Parameters.AddWithValue("@DocEntry", docEntry);

                            if (!string.IsNullOrEmpty(emailAddress))
                            {
                                cmd.Parameters.AddWithValue("@EmailAddress", emailAddress);
                            }

                            cmd.ExecuteNonQuery();
                        }
                    }

                    return new EmailResult
                    {
                        Success = true,
                        Message = $"Email enviat correctament (via {spName})"
                    };
                }
                catch
                {
                    // Continuar amb el següent stored procedure
                    continue;
                }
            }

            return new EmailResult
            {
                Success = false,
                Message = "No s'ha trobat cap stored procedure de Boyum disponible. Contacta amb l'administrador per verificar la instal·lació de B1 Print & Delivery."
            };
        }
    }
}
