namespace XNDmjApi.Infrastructure.Options
{
    public sealed class ApiKeyProfilesOptions
    {
        public ApiKeyProfileOptions Prod { get; set; } = new ApiKeyProfileOptions();
        public ApiKeyProfileOptions Test { get; set; } = new ApiKeyProfileOptions();
    }

    public sealed class ApiKeyProfileOptions
    {
        public string ApiKey { get; set; } = "";
        public string DbName { get; set; } = "";
        public string SapUser { get; set; } = "";
        public string SapPassword { get; set; } = "";
    }

    /// <summary>
    /// Perfil resolt a partir de X-Api-Key (no ve del client; ve del mapping server-side).
    /// Es guarda a HttpContext.Items per ús dels controllers/serveis.
    /// </summary>
    public sealed class ResolvedApiKeyProfile
    {
        public string Name { get; }
        public string DbName { get; }
        public string SapUser { get; }
        public string SapPassword { get; }

        public ResolvedApiKeyProfile(string name, ApiKeyProfileOptions opt)
        {
            Name = name;
            DbName = opt?.DbName ?? "";
            SapUser = opt?.SapUser ?? "";
            SapPassword = opt?.SapPassword ?? "";
        }
    }
}
