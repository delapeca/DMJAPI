using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Net.Http.Headers;
using System.Text;
using XNDmjApi.Functions;
using XNDmjApi.Infrastructure.Options;
using XNDmjApi.Infrastructure.Services;
using XNDmjApi.Infrastructure.Middleware;
using XNDmjApi.Loxone.Data;
using XNDmjApi.Loxone.Options;
using XNDmjApi.Loxone.Services;
using XNDmjApi.Models;
using XNDmjApi.QrMulti.Services;
using XNDmjApi.Services;

// IMPORTANT: en el teu setup, aquest és el model que compila
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(t => t.FullName ?? t.Name);

    // IFormFile upload support (model Microsoft.OpenApi)
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Format = "binary"
    });

    // Swagger · API Keys (només documentació/UI; NO aplica auth real)
    c.AddSecurityDefinition("AdminApiKey", new OpenApiSecurityScheme
    {
        Description = "Admin API Key (header AdminApiKey).",
        Name = "AdminApiKey",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityDefinition("ProfileApiKey", new OpenApiSecurityScheme
    {
        Description = "Profile API Key (header ProfileApiKey).",
        Name = "ProfileApiKey",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    // Header només als endpoints que toca (no global)
    c.OperationFilter<AddPerEndpointApiKeyHeaderParameter>();


    //// Definició API Key (botó Authorize existirà, però no ens hi refiem per injectar headers)
    //c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    //{
    //    Description = "Introdueix la clau d'admin (header X-Api-Key).",
    //    Name = "X-Api-Key",
    //    In = ParameterLocation.Header,
    //    Type = SecuritySchemeType.ApiKey
    //});

    // En lloc de SecurityRequirement (que et porta a conflictes), afegim header param a totes les operacions
    //c.OperationFilter<AddApiKeyHeaderParameter>();
});

builder.Configuration.GetSection("BBDD").Get<ApplicationSettings>();

builder.Services.AddCors();

// Serveis existents
builder.Services.AddScoped<CustomerPortfolioService>();

// ──────────────────────────────────────────────────────────────
// LOXONE · DI + Config
// ──────────────────────────────────────────────────────────────
builder.Services.Configure<LoxoneOptions>(builder.Configuration.GetSection("Loxone"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<TokenPolicyOptions>(builder.Configuration.GetSection("TokenPolicy"));
builder.Services.Configure<DbMigrationsOptions>(builder.Configuration.GetSection("DbMigrations"));
builder.Services.Configure<XNDmjApi.Infrastructure.ApiKeys.ApiKeyProfilesOptions>(builder.Configuration.GetSection("ApiKeyProfiles"));
builder.Services.AddScoped<XNDmjApi.Services.SapChangeRequestsUdoService>();

builder.Services.AddScoped<DbMigrationRunner>();
builder.Services.AddScoped<QrMultiDbInitializer>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("LoxoneDb");
    if (string.IsNullOrWhiteSpace(cs))
        cs = "Data Source=App_Data\\qrloxone.db";

    options.UseSqlite(cs);
});

builder.Services.AddHttpClient("loxone").ConfigureHttpClient((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<LoxoneOptions>>().Value;

    if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
    {
        var baseUrl = opt.BaseUrl.TrimEnd('/') + "/";
        client.BaseAddress = new Uri(baseUrl);
    }

    if (!string.IsNullOrWhiteSpace(opt.Username))
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opt.Username}:{opt.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }
});

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<LoxoneCommandService>();

var app = builder.Build();

// DB bootstrap (idempotent)
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<DbMigrationRunner>();
    await runner.ApplyOnStartupAsync(CancellationToken.None);
}

app.UseMiddleware<ApiExceptionJsonMiddleware>();

// Swagger sempre ON
app.UseSwagger();
app.UseStaticFiles();
app.UseSwaggerUI(c =>
{
    c.ConfigObject.AdditionalItems["persistAuthorization"] = "true";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "XNDmjApi v1");
    // Sense interceptors (en el teu cas no s'aplicaven)
});

app.UseHttpsRedirection();

// X-Api-Key → perfil (PROD/TEST) · només per /api/SapChangeRequestsUdo
app.UseAuthentication();
app.UseAuthorization();

app.UseCors(options =>
    options.WithOrigins("*")
           .AllowAnyHeader()
           .AllowAnyMethod());
app.UseMiddleware<XNDmjApi.Infrastructure.ApiKeys.ApiKeyProfileMiddleware>();

app.MapControllers();
app.Run();


// ──────────────────────────────────────────────────────────────
// OperationFilter compatible amb Microsoft.OpenApi (IOpenApiParameter + JsonSchemaType)
// ──────────────────────────────────────────────────────────────
//public sealed class AddApiKeyHeaderParameter : IOperationFilter
//{
//    public void Apply(OpenApiOperation operation, OperationFilterContext context)
//    {
//        // En aquest model: IList<IOpenApiParameter>
//        if (operation.Parameters == null)
//            operation.Parameters = new List<IOpenApiParameter>();

//        // Evita duplicats
//        foreach (var p in operation.Parameters)
//        {
//            if (string.Equals(p.Name, "X-Api-Key", StringComparison.OrdinalIgnoreCase) &&
//                p.In == ParameterLocation.Header)
//            {
//                return;
//            }
//        }

//        operation.Parameters.Add(new OpenApiParameter
//        {
//            Name = "X-Api-Key",
//            In = ParameterLocation.Header,
//            Required = false,
//            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
//            Description = "Clau d'admin per endpoints protegits"
//        });
//    }
//}

    class AddPerEndpointApiKeyHeaderParameter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                operation.Parameters = new List<IOpenApiParameter>();

            // Detecta controller (forma estàndard a ASP.NET Core)
            var ctrl =
                context.ApiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var c)
                    ? c
                    : null;

            if (string.IsNullOrWhiteSpace(ctrl))
                return;

            // Admin endpoints (Admin:ApiKey)
            if (IsAdminController(ctrl))
            {
                AddHeaderIfMissing(operation, "AdminApiKey", "Admin API Key per endpoints admin (temporalment també pot existir X-Api-Key al backend).");
                return;
            }

            // Profile endpoints (ApiKeyProfiles)
            if (IsProfileController(ctrl))
            {
                AddHeaderIfMissing(operation, "ProfileApiKey", "Profile API Key per endpoints de perfil (temporalment també pot existir X-Api-Key al backend).");
                return;
            }
        }

        private static bool IsAdminController(string controllerName)
        {
            // Controllers que al teu dump van amb Admin:ApiKey
            // QRController, QrMultiAdminController, QrResolveController, ClientsSelfController
            return controllerName.Equals("QR", StringComparison.OrdinalIgnoreCase)
                || controllerName.Equals("QrMultiAdmin", StringComparison.OrdinalIgnoreCase)
                || controllerName.Equals("QrResolve", StringComparison.OrdinalIgnoreCase)
                || controllerName.Equals("ClientsSelf", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProfileController(string controllerName)
        {
            // Controller que al teu dump va amb ApiKeyProfiles
            return controllerName.Equals("SapChangeRequestsUdo", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddHeaderIfMissing(OpenApiOperation operation, string headerName, string description)
        {
            foreach (var p in operation.Parameters)
            {
                if (string.Equals(p.Name, headerName, StringComparison.OrdinalIgnoreCase) &&
                    p.In == ParameterLocation.Header)
                    return;
            }

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = headerName,
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                Description = description
            });
        }
    }






