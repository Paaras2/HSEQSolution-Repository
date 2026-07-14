namespace HSEQ.API.ServiceConfiguration
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationLayerServices(this IServiceCollection services)
        {
            //services.AddScoped<JwtAuthorizeAttribute>(provider =>
            //{
            //    var jwtService = provider.GetRequiredService<IJwtService>();
            //    return new JwtAuthorizeAttribute(jwtService);
            //});
            return services;
        }


    }
}
