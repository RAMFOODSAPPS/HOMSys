using HOMSys.Application.Interfaces;
using HOMSys.Application.Services;
using HOMSys.Infrastructure.Data;
using HOMSys.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HOMSys.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                sql => sql.CommandTimeout(120)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddHttpContextAccessor();
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<ISiteTypeRepository, SiteTypeRepository>();

        // Sales order encoding
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IPoLogRepository, PoLogRepository>();
        services.AddScoped<IDocClassRepository, DocClassRepository>();
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
        services.AddScoped<IOosSyncRepository, OosSyncRepository>();
        services.AddScoped<IPricingRepository, PricingRepository>();
        services.AddScoped<ICustomerIdentifierMapRepository, CustomerIdentifierMapRepository>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<SiteService>();
        services.AddScoped<SiteTypeService>();
        services.AddScoped<SalesOrderService>();
        services.AddScoped<SalesOrderBridgeService>();
        services.AddScoped<CustomerBranchResolver>();
        services.AddScoped<PriceCalculationService>();
        services.AddScoped<PricelistExportService>();
        services.AddScoped<PricelistExcelBuilder>();
        services.AddScoped<PricingDataImporter>();
        services.AddScoped<PricingDeltaImporter>();
        services.AddScoped<SyncStatusService>();

        return services;
    }
}
