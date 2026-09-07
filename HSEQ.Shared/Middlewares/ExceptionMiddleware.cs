using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HSEQ.Common;

namespace HSEQ.Shared.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (CustomException ex)
            {
                httpContext.Response.StatusCode = ex.StatusCode;
                httpContext.Response.ContentType = "application/json";
                var result = JsonSerializer.Serialize(new { message = ex.Message });
                await httpContext.Response.WriteAsync(result);
            }
            catch (Exception ex)
            {
                // خطای پیش‌بینی‌نشده سمت سرور لاگ می‌شود، ولی چیزی از آن به کاربر
                // برنمی‌گردد. بدون این، خطای ۵۰۰ هیچ ردی از خودش باقی نمی‌گذاشت و
                // عیب‌یابی پس از استقرار عملاً ناممکن بود. مقصد لاگ را میزبان تعیین
                // می‌کند؛ روی IIS همان stdout ماژول ASP.NET Core است.
                _logger.LogError(ex, "خطای مدیریت‌نشده در {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);

                httpContext.Response.StatusCode = 500;
                httpContext.Response.ContentType = "application/json";
                var result = JsonSerializer.Serialize(new { message = "Internal server error" });
                await httpContext.Response.WriteAsync(result);
            }
        }
    }
}
