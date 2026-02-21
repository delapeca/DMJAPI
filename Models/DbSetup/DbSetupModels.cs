using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.Models.DbSetup
{
    public sealed class EnsureSchemaRequest
    {
        /// <summary>userToken SAP (USER_CODE#USERID#expiry) encriptat (AES256_USER_Key)</summary>
        [Required]
        public string UserToken { get; set; } = string.Empty;

        /// <summary>Esquema de taules a garantir (idempotent)</summary>
        [Required]
        public DbSchema Schema { get; set; } = new DbSchema();
    }

    public sealed class DbSchema
    {
        [Required]
        public List<DbTableSpec> Tables { get; set; } = new();

        /// <summary>
        /// Opcional: UDOs a crear/assegurar (idempotent).
        /// HeaderTable i ChildTables han d'existir dins Tables.
        /// </summary>
        public List<DbUdoSpec> Udos { get; set; } = new();
    }

    public sealed class DbUdoSpec
    {
        /// <summary>Codi del UDO (recomanat: igual que HeaderTable)</summary>
        [Required]
        public string Code { get; set; } = string.Empty;

        /// <summary>Nom descriptiu del UDO</summary>
        public string? Name { get; set; } = null;

        /// <summary>Taula header (sense '@')</summary>
        [Required]
        public string HeaderTable { get; set; } = string.Empty;

        /// <summary>Taules child (sense '@')</summary>
        public List<string> ChildTables { get; set; } = new();
    }

    public sealed class DbTableSpec
    {
        /// <summary>Nom de taula (sense schema). Ex: DMJ_CRQ_SQL</summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>Opcional. Si vols forçar schema (per defecte: dbo). (Ignorat en mode UDT/UDO)</summary>
        public string? Schema { get; set; } = null;

        /// <summary>Columnes</summary>
        [Required]
        public List<DbColumnSpec> Columns { get; set; } = new();

        /// <summary>Clau primària (llista de columnes). (Ignorat en mode UDT/UDO)</summary>
        public List<string>? PrimaryKey { get; set; } = null;

        /// <summary>Índexos (opc.) (Ignorat en mode UDT/UDO)</summary>
        public List<DbIndexSpec>? Indexes { get; set; } = null;
    }

    public sealed class DbColumnSpec
    {
        /// <summary>Nom columna</summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>Tipus SQL allowlist (ex: int, nvarchar(50), nvarchar(max), datetime2...)</summary>
        [Required]
        public string SqlType { get; set; } = string.Empty;

        /// <summary>Nullable? (default: true)</summary>
        public bool Nullable { get; set; } = true;

        /// <summary>IDENTITY(1,1) (Ignorat en mode UDT/UDO)</summary>
        public bool Identity { get; set; } = false;

        /// <summary>Default SQL expression (Ignorat en mode UDT/UDO)</summary>
        public string? Default { get; set; } = null;
    }

    public sealed class DbIndexSpec
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public List<string> Columns { get; set; } = new();

        public bool Unique { get; set; } = false;
    }

    public sealed class EnsureSchemaResponse
    {
        public bool Ok { get; set; }
        public string Code { get; set; } = "OK";
        public string Message { get; set; } = "";

        public List<string> CreatedTables { get; set; } = new();
        public List<string> AddedColumns { get; set; } = new();
        public List<string> CreatedIndexes { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }
}
