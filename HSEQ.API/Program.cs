using HSEQ.API.ServiceConfiguration;
using HSEQ.Common;
using HSEQ.Domain;
using HSEQ.Service;
using HSEQ.Service.Interfaces.Services;
using HSEQ.Service.Services.Services;
using HSEQ.Shared.Interfaces.Services;
using HSEQ.Shared.Middlewares;
using HSEQ.Shared.Services.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// تنظیمات اولیه
AppSettingFactory.Initialize(builder.Configuration);
var appSettings = AppSettingFactory.AppSetting;
var configuration = builder.Configuration;

// ثبت سرویس‌ها
builder.Services.AddControllers();
builder.Services.AddApplicationLayerServices()
                .AddServiceLayerServices()
                .AddDomainLayerServices(configuration);
builder.Services.AddScoped<IUMService, UmService>();
builder.Services.AddHttpClient<IUMService, UmService>();

// تنظیمات Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = appSettings.JwtSettings.Issuer,
        ValidAudience = appSettings.JwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.JwtSettings.Key))
    };
});

builder.Services.AddSwaggerGen();

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Cors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Pipeline تنظیمات
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HSEQ");
    options.RoutePrefix = string.Empty;
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("Cors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
    if (context is null)
        throw new Exception("Database Context Not Found");

    await context.Database.MigrateAsync();

    var seedService = scope.ServiceProvider.GetRequiredService<ISeedDatabase>();
    await seedService.Seed();
}

app.Run();
