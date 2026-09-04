using System.Text;
using HOMSys.API.Middleware;
using HOMSys.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("users",         p => p.RequireClaim("permission", "users"))
    .AddPolicy("roles",         p => p.RequireClaim("permission", "roles", "authorization"))
    .AddPolicy("authorization", p => p.RequireClaim("permission", "authorization"))
    .AddPolicy("companies",     p => p.RequireClaim("permission", "companies"))
    .AddPolicy("departments",   p => p.RequireClaim("permission", "departments"))
    .AddPolicy("sites",         p => p.RequireClaim("permission", "sites"))
    .AddPolicy("site-types",    p => p.RequireClaim("permission", "site-types"))
    .AddPolicy("sales-orders",  p => p.RequireClaim("permission", "sales-orders"))
    .AddPolicy("customer-search", p => p.RequireClaim("permission", "sales-orders", "pricelist-export"))
    .AddPolicy("legacy-monitoring", p => p.RequireClaim("permission", "legacy-monitoring"));

// CORS — allow specific origins and credentials (required for HttpOnly cookie)
builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins(
                  "http://localhost:4200",
                  "http://localhost:4400",
                  "https://icy-bay-07811fc00.7.azurestaticapps.net",
                  "https://homsys.ramfoods.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// OpenAPI (built-in .NET 10)
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "HOMSys API";
        document.Info.Version = "v1";
        document.Info.Description = "Head Office Monitoring System API";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "HOMSys API";
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Strict
});
app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HOMSys.Infrastructure.Data.AppDbContext>();
    db.Database.Migrate();
}

// One-off reference-data import from the staged BMS DBF snapshots.
//   dotnet run --project HOMSys.API -- import-reference-data [path-to-dbf-folder]
// Runs the import and exits without starting the web host. Kept as a CLI verb
// rather than an endpoint because it truncates and reloads the reference tables.
if (args.Length > 0 && args[0].Equals("import-reference-data", StringComparison.OrdinalIgnoreCase))
{
    var path = args.Length > 1
        ? args[1]
        : HOMSys.Infrastructure.Data.ReferenceDataImporter.DefaultDbfPath;

    using var scope = app.Services.CreateScope();
    var importer = scope.ServiceProvider
        .GetRequiredService<HOMSys.Infrastructure.Data.ReferenceDataImporter>();

    Console.WriteLine($"Reference data import from: {path}");
    var result = await importer.ImportAllAsync(path, Console.WriteLine);
    Console.WriteLine($"Done. {result}");
    return;
}

// One-off head-office masters import, read directly from HO's production drive
// (F:\PMDM, F:\AUTOPROG\ADDON\{branch}, F:\AUTOPROG\CUSTOMER\{branch}) across
// every branch found on disk — not a single branch's staged copy. Runs only on
// the HO VM (needs F:\ access), never per-branch.
//   dotnet run --project HOMSys.API -- import-HoMaster-data [root, default F:\]
// See C:\Users\RDEGUZMAN\.claude\plans\can-you-see-this-jaunty-puffin.md.
if (args.Length > 0 && args[0].Equals("import-HoMaster-data", StringComparison.OrdinalIgnoreCase))
{
    var root = args.Length > 1
        ? args[1]
        : HOMSys.Infrastructure.Data.PricingDataImporter.DefaultRoot;

    using var scope = app.Services.CreateScope();
    var importer = scope.ServiceProvider
        .GetRequiredService<HOMSys.Infrastructure.Data.PricingDataImporter>();

    Console.WriteLine($"HO master data import (all branches) from: {root}");
    var result = await importer.ImportAllAsync(root, Console.WriteLine);
    Console.WriteLine($"Done. {result}");
    return;
}

app.Run();
