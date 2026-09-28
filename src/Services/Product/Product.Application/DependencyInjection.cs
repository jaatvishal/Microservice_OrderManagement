using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.Catalog;

namespace Product.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProductApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<RenameCategoryHandler>();
        services.AddScoped<ListCategoriesHandler>();
        services.AddScoped<DeactivateCategoryHandler>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<SetProductActivationHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<ListProductsHandler>();
        services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();
        return services;
    }
}
