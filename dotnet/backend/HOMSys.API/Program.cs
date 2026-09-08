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
    MinimumSameSitePolicy = SameSiteMode.None
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

// One-off CLI entry point for the Grouped-by-Price report, now a real
// Pricelist Export mode (GroupedPricelistService/GroupedPricelistExcelBuilder,
// also used by PricelistController) — kept as a dev convenience.
//   dotnet run --project HOMSys.API -- export-grouped-pricelist <siteName> [baselineCustKey] [effDate yyyy-MM-dd] [srpMarkup] [outputPath]
if (args.Length > 0 && args[0].Equals("export-grouped-pricelist", StringComparison.OrdinalIgnoreCase))
{
    var siteName = args.Length > 1 ? args[1] : throw new ArgumentException("Site name is required.");
    var baselineCustKey = args.Length > 2 ? args[2] : null;
    var effDate = args.Length > 3 ? DateOnly.Parse(args[3]) : DateOnly.FromDateTime(DateTime.Today);
    var srpMarkup = args.Length > 4 ? decimal.Parse(args[4]) : 3m;
    var outputPath = args.Length > 5 ? args[5] : $@"C:\claude\output\GroupedPricelist_{siteName}_{effDate:yyyyMMdd}.xlsx";

    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    var groupedService = sp.GetRequiredService<HOMSys.Application.Services.GroupedPricelistService>();
    var excelBuilder = sp.GetRequiredService<HOMSys.Application.Services.PricelistExcelBuilder>();
    var diffExcelBuilder = sp.GetRequiredService<HOMSys.Application.Services.GroupedPricelistExcelBuilder>();

    var (result, baseline) = await groupedService.BuildAsync(siteName, effDate, srpMarkup, baselineCustKey);
    Console.WriteLine($"{result.Customers.Count:N0} distinct price groups for {siteName}");

    var bytes = excelBuilder.Build(result);
    if (baseline is not null)
        bytes = diffExcelBuilder.AddDiffSheets(bytes, result, baseline);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllBytesAsync(outputPath, bytes);
    Console.WriteLine($"Wrote {outputPath}");
    return;
}

app.Run();
