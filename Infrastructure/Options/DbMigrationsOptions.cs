namespace XNDmjApi.Infrastructure.Options
{
    /// <summary>
    /// Opcions de bootstrap/manteniment de la SQLite (sense EF migrations).
    /// Es llegeix de la secció "DbMigrations" del appsettings.json.
    /// </summary>
    public class DbMigrationsOptions
    {
        /// <summary>
        /// Si és false, no s'aplica res a l'arrencada.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Si és true, fa EnsureCreated (crea taules EF si no existeixen).
        /// </summary>
        public bool EnsureCreated { get; set; } = true;

        /// <summary>
        /// Si és true, aplica inicialitzadors idempotents addicionals (ex: QrManifests).
        /// </summary>
        public bool ApplyCustomInitializers { get; set; } = true;
    }
}
