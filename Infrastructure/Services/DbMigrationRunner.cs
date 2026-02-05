using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using XNDmjApi.Infrastructure.Options;
using XNDmjApi.Loxone.Data;
using XNDmjApi.QrMulti.Services;

namespace XNDmjApi.Infrastructure.Services
{
    /// <summary>
    /// Runner d'arrencada (idempotent) per assegurar l'estructura de la SQLite.
    /// NO depèn de .
    /// </summary>
    public class DbMigrationRunner
    {
        private readonly AppDbContext _db;
        private readonly DbMigrationsOptions _opt;
        private readonly QrMultiDbInitializer _qrMultiInit;

        public DbMigrationRunner(
            AppDbContext db,
            IOptions<DbMigrationsOptions> opt,
            QrMultiDbInitializer qrMultiInit)
        {
            _db = db;
            _opt = opt.Value;
            _qrMultiInit = qrMultiInit;
        }

        public async Task ApplyOnStartupAsync(CancellationToken ct)
        {
            Debug.WriteLine($"[DbMigrationRunner] Enabled={_opt.Enabled} EnsureCreated={_opt.EnsureCreated} ApplyCustomInitializers={_opt.ApplyCustomInitializers}");
            Debug.WriteLine($"[DbMigrationRunner] DataSource={_db.Database.GetDbConnection().DataSource}");

            if (_opt == null || !_opt.Enabled)
                return;

            // 1) Crea taules EF (LoxoneActions, Locations, LocationActions, QrTokens, etc.)
            if (_opt.EnsureCreated)
            {
                await _db.Database.EnsureCreatedAsync(ct);
            }

            Debug.WriteLine("[DbMigrationRunner] EnsureCreated DONE");


            // 2) Inicialitzadors idempotents custom (QrManifests)
            if (_opt.ApplyCustomInitializers)
            {
                await _qrMultiInit.ApplyOnStartupAsync(ct);
                await EnsureQrTokensTableAsync(ct);
                
                await EnsureQrDevicesTableAsync(ct);
await EnsureLoxoneActionsTableAsync(ct);
                await EnsureLocationsTableAsync(ct);
                await EnsureLocationActionsTableAsync(ct);
            }
        }

        private async Task EnsureQrTokensTableAsync(CancellationToken ct)
        {
            const string sql = @"
            CREATE TABLE IF NOT EXISTS QrTokens (
                Token TEXT PRIMARY KEY NOT NULL,
                ActionCode TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT NULL,
                MaxUses INTEGER NOT NULL DEFAULT 0,
                UsesCount INTEGER NOT NULL DEFAULT 0,
                Revoked INTEGER NOT NULL DEFAULT 0,
                LastUsedAt TEXT NULL,
                LastUsedIp TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_QrTokens_ActionCode ON QrTokens(ActionCode);
            ";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        private async Task EnsureQrDevicesTableAsync(CancellationToken ct)
        {
            const string sql = @"
            CREATE TABLE IF NOT EXISTS QrDevices (
                DeviceName TEXT PRIMARY KEY NOT NULL,
                AssignmentKind TEXT NOT NULL,
                AssignmentId INTEGER NULL,
                AssignmentLabel TEXT NULL,
                ScanMode TEXT NOT NULL,
                ScanValue TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_QrDevices_DeviceName ON QrDevices(DeviceName);
            ";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }

        private async Task EnsureLoxoneActionsTableAsync(CancellationToken ct)
        {
            const string sql = @"
            CREATE TABLE IF NOT EXISTS LoxoneActions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL,
                Name TEXT NOT NULL,
                LoxoneCommand TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UX_LoxoneActions_Code ON LoxoneActions(Code);
            ";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }

        private async Task EnsureLocationsTableAsync(CancellationToken ct)
        {
            const string sql = @"
            CREATE TABLE IF NOT EXISTS Locations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL,
                Name TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                SortOrder INTEGER NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UX_Locations_Code ON Locations(Code);
            CREATE INDEX IF NOT EXISTS IX_Locations_IsActive ON Locations(IsActive);
            CREATE INDEX IF NOT EXISTS IX_Locations_SortOrder ON Locations(SortOrder);
            ";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }

        private async Task EnsureLocationActionsTableAsync(CancellationToken ct)
        {
            // IMPORTANT: a SQLite, les FK només s'apliquen si PRAGMA foreign_keys=ON.
            const string sql = @"
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS LocationActions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LocationId INTEGER NOT NULL,
                LoxoneActionId INTEGER NOT NULL,
                IsDefault INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(LocationId) REFERENCES Locations(Id) ON DELETE CASCADE,
                FOREIGN KEY(LoxoneActionId) REFERENCES LoxoneActions(Id) ON DELETE CASCADE
            );

            -- Segons OnModelCreating: LocationId és únic (una acció per ubicació)
            CREATE UNIQUE INDEX IF NOT EXISTS UX_LocationActions_LocationId ON LocationActions(LocationId);

            CREATE INDEX IF NOT EXISTS IX_LocationActions_LoxoneActionId ON LocationActions(LoxoneActionId);
            CREATE INDEX IF NOT EXISTS IX_LocationActions_IsDefault ON LocationActions(IsDefault);
            ";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }


    }
}

