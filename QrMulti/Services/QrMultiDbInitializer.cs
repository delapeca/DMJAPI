using Microsoft.EntityFrameworkCore;
using XNDmjApi.Loxone.Data;

namespace XNDmjApi.QrMulti.Services
{
    /// <summary>
    /// Inicialitzador idempotent per a la taula QrManifests (SQLite).
    /// IMPORTANT: NO depèn de API. Només crea estructura si no existeix.
    /// </summary>
    public class QrMultiDbInitializer
    {
        private readonly AppDbContext _db;

        public QrMultiDbInitializer(AppDbContext db)
        {
            _db = db;
        }

        public async Task ApplyOnStartupAsync(CancellationToken ct)
        {
            const string sql = @"
            CREATE TABLE IF NOT EXISTS QrManifests (
                Token TEXT PRIMARY KEY NOT NULL,
                PayloadJson TEXT NOT NULL,
                ItemCode TEXT NULL,
                PanelCode TEXT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_QrManifests_ItemCode ON QrManifests(ItemCode);
            CREATE INDEX IF NOT EXISTS IX_QrManifests_PanelCode ON QrManifests(PanelCode);
            ";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }
    }
}

