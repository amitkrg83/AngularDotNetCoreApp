using System.Security.Claims;
using System.Threading.RateLimiting;
using Commerce.Application;
using Commerce.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder=WebApplication.CreateBuilder(args);
// Intentionally local-only: seeded passwords + volatile data are not production hosting.
if(!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Run this seeded demonstration in Development. See production checklist before deploying.");
// MVC view-feature services register the built-in antiforgery filter; endpoints still return JSON only.
builder.Services.AddControllersWithViews(options=>options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options=>{options.HeaderName="X-XSRF-TOKEN";options.Cookie.Name="Commerce.AntiForgery";options.Cookie.SameSite=SameSiteMode.Strict;});
builder.Services.AddSingleton<IPasswordService,PasswordService>();
builder.Services.AddSingleton<IProductRepository,MemoryProducts>();
builder.Services.AddSingleton<IUserRepository,MemoryUsers>();
builder.Services.AddSingleton<IOrderRepository,MemoryOrders>();
builder.Services.AddSingleton<IMutationLock,MutationLock>();
builder.Services.AddScoped<CommerceService>();
var origin=(builder.Configuration["FrontendOrigin"] ?? "http://localhost:4200").TrimEnd('/');
var mode=builder.Configuration["Payments:Mode"] ?? "Simulated";
if(mode=="Stripe")
{
    var key=builder.Configuration["Stripe:SecretKey"] ?? "";var secret=builder.Configuration["Stripe:WebhookSecret"] ?? "";
    if(!key.StartsWith("sk_test_") || !secret.StartsWith("whsec_")) throw new InvalidOperationException("Stripe requires a test secret key and webhook signing secret; use dotnet user-secrets.");
    builder.Services.AddHttpClient("stripe",client=>client.Timeout=TimeSpan.FromSeconds(20));
    builder.Services.AddScoped<IPaymentGateway>(sp=>new StripeGateway(sp.GetRequiredService<IHttpClientFactory>().CreateClient("stripe"),key,secret,origin));
}
else if(mode=="Simulated") builder.Services.AddSingleton<IPaymentGateway>(new SimulatedGateway(origin));
else throw new InvalidOperationException("Payments:Mode must be Simulated or Stripe.");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options=>{
    options.Cookie.Name="Commerce.Session";options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Strict;
    options.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;options.ExpireTimeSpan=TimeSpan.FromHours(2);options.SlidingExpiration=false;
    options.Events.OnRedirectToLogin=ctx=>{ctx.Response.StatusCode=401;return Task.CompletedTask;};
    options.Events.OnRedirectToAccessDenied=ctx=>{ctx.Response.StatusCode=403;return Task.CompletedTask;};
    options.Events.OnValidatePrincipal=async ctx=>{
        var repo=ctx.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user=Guid.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier),out var id) ? repo.Get(id) : null;
        if(user is null || !user.Active || ctx.Principal?.FindFirstValue("version")!=user.Version.ToString()) { ctx.RejectPrincipal();await ctx.HttpContext.SignOutAsync(); }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options=>{
    options.RejectionStatusCode=429;
    options.AddPolicy("auth",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "local",_=>new FixedWindowRateLimiterOptions{PermitLimit=15,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
var app=builder.Build();
app.UseExceptionHandler(error=>error.Run(async ctx=>{
    var ex=ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status=ex is ShopException shop ? shop.Status : 500;
    if(status==500) app.Logger.LogError(ex,"Unhandled request error");
    ctx.Response.StatusCode=status;
    await ctx.Response.WriteAsJsonAsync(new ProblemDetails{Status=status,Title=status==500 ? "Unexpected server error." : ex!.Message});
}));
app.Use(async(ctx,next)=>{ctx.Response.Headers.XContentTypeOptions="nosniff";ctx.Response.Headers.CacheControl="no-store";await next();});
app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
app.Run();
public partial class Program { }
