using System.Collections.Generic;

namespace XNDmjApi.Models
{
    public sealed class SqlSchemaSpec
    {
        public string? Database { get; set; }              // opcional (informatiu)
        public List<SqlTableSpec> Tables { get; set; } = new();
    }

    public sealed class SqlTableSpec
    {
        public string Schema { get; set; } = "dbo";
        public string Name { get; set; } = "";
        public List<SqlColumnSpec> Columns { get; set; } = new();
        public List<SqlIndexSpec> Indexes { get; set; } = new();
        public List<SqlForeignKeySpec> ForeignKeys { get; set; } = new();
    }

    public sealed class SqlColumnSpec
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";            // ex: "int", "nvarchar(50)", "datetime2"
        public bool Nullable { get; set; } = true;
        public bool Identity { get; set; } = false;       // només per int/bigint habitualment
        public bool PrimaryKey { get; set; } = false;     // PK simple (una columna)
        public string? DefaultSql { get; set; } = null;   // ex: "GETUTCDATE()", "'PENDING'", "0"
    }

    public sealed class SqlIndexSpec
    {
        public string Name { get; set; } = "";
        public bool Unique { get; set; } = false;
        public List<string> Columns { get; set; } = new();
    }

    public sealed class SqlForeignKeySpec
    {
        public string Name { get; set; } = "";
        public string Column { get; set; } = "";
        public string RefSchema { get; set; } = "dbo";
        public string RefTable { get; set; } = "";
        public string RefColumn { get; set; } = "";
        public bool OnDeleteCascade { get; set; } = false;
    }
}
