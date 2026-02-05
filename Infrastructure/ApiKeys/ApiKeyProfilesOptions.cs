using System.Collections.Generic;

namespace XNDmjApi.Infrastructure.ApiKeys
{
    // appsettings / env:
    // "ApiKeyProfiles": {
    //   "prod": { "ApiKey":"...", "CompanyDb":"SBO_DOMENJO", "SapUser":"...", "SapPassword":"..." },
    //   "test": { "ApiKey":"...", "CompanyDb":"TEST",        "SapUser":"...", "SapPassword":"..." }
    // }
    public sealed class ApiKeyProfilesOptions
    {
        public Dictionary<string, ApiKeyProfileConfig> Profiles { get; set; } = new();
    }

    public sealed class ApiKeyProfileConfig
    {
        public string ApiKey { get; set; } = "";
        public string CompanyDb { get; set; } = "";
        public string SapUser { get; set; } = "";
        public string SapPassword { get; set; } = "";
    }

    public sealed class ApiKeyProfile
    {
        public string Name { get; }
        public string CompanyDb { get; }
        public string SapUser { get; }
        public string SapPassword { get; }

        public ApiKeyProfile(string name, string companyDb, string sapUser, string sapPassword)
        {
            Name = name ?? "";
            CompanyDb = companyDb ?? "";
            SapUser = sapUser ?? "";
            SapPassword = sapPassword ?? "";
        }
    }
}
