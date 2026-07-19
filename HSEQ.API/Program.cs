using HSEQ.API.Service;
using HSEQ.API.ServiceConfiguration;
using HSEQ.Common;
using HSEQ.Domain;
using HSEQ.Service;
using HSEQ.Service.Interfaces.Services;
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
//builder.Services.AddScoped<IUnitService UnitService>();
// تنظیمات Swagger
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

app.UseHttpsRedirection();
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
