using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Commerce.Application;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Commerce.Api.Controllers;

public sealed record LoginRequest([Required,EmailAddress,MaxLength(200)] string Email,[Required,MaxLength(128)] string Password);
public sealed record RegisterRequest([Required,MaxLength(80)] string Name,[Required,EmailAddress,MaxLength(200)] string Email,[Required,MinLength(10),MaxLength(128)] string Password);
public sealed record UserUpdate([Required] string Role,bool Active);
public sealed record StatusUpdate([Required] string Status);
public sealed record SimulationRequest([Required] string Outcome);

[ApiController,Route("api/auth")]
public sealed class AuthController(CommerceService shop,IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens=antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN",tokens.RequestToken!,new CookieOptions{HttpOnly=false,SameSite=SameSiteMode.Strict,Secure=Request.IsHttps,Path="/"});
        return NoContent();
    }
    [HttpPost("login"),EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest input) { var user=shop.Login(input.Email,input.Password);await SignIn(user);return Ok(CommerceService.Public(user)); }
    [HttpPost("register"),EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest input) { var user=await shop.Register(input.Name,input.Email,input.Password);await SignIn(user);return Ok(CommerceService.Public(user)); }
    private Task SignIn(Commerce.Domain.User user)
    {
        var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Name,user.Name),new Claim(ClaimTypes.Role,user.Role),new Claim("version",user.Version.ToString())},CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity));
    }
    [HttpGet("me"),Authorize]
    public IActionResult Me()=>Ok(CommerceService.Public(shop.User(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))!));
    [HttpPost("logout"),Authorize]
    public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync();return NoContent(); }
}
[ApiController,Authorize,Route("api/products")]
public sealed class ProductsController(CommerceService shop) : ControllerBase
{
    [HttpGet] public IActionResult All()=>Ok(shop.Products());
    [HttpGet("{id:guid}")] public IActionResult Get(Guid id)=>Ok(shop.Product(id));
}
[ApiController,Authorize,Route("api/orders")]
public sealed class OrdersController(CommerceService shop,IPaymentGateway gateway) : ControllerBase
{
    private Guid UserId=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public IActionResult All()=>Ok(shop.Orders(UserId,false));
    [HttpGet("payment-mode")] public IActionResult Mode()=>Ok(new { mode=gateway.Mode });
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutInput input,CancellationToken ct)=>Ok(await shop.Checkout(UserId,Request.Headers["Idempotency-Key"].ToString(),input,ct));
    [HttpPost("{id:guid}/simulate")]
    public async Task<IActionResult> Simulate(Guid id,SimulationRequest input)=>Ok(await shop.Simulate(UserId,id,input.Outcome));
}
[ApiController,Authorize(Roles="Admin"),Route("api/admin")]
public sealed class AdminController(CommerceService shop) : ControllerBase
{
    [HttpGet("products")] public IActionResult Products()=>Ok(shop.Products(true));
    [HttpPost("products")] public async Task<IActionResult> Add(ProductInput input) { var p=await shop.SaveProduct(null,input);return Created($"/api/products/{p.Id}",p); }
    [HttpPut("products/{id:guid}")] public async Task<IActionResult> Edit(Guid id,ProductInput input)=>Ok(await shop.SaveProduct(id,input));
    [HttpDelete("products/{id:guid}")] public async Task<IActionResult> Delete(Guid id) { await shop.DeactivateProduct(id);return NoContent(); }
    [HttpGet("users")] public IActionResult Users()=>Ok(shop.Users());
    [HttpPut("users/{id:guid}")] public async Task<IActionResult> UserUpdate(Guid id,UserUpdate input) { await shop.UpdateUser(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),id,input.Role,input.Active);return NoContent(); }
    [HttpGet("orders")] public IActionResult Orders()=>Ok(shop.Orders(Guid.Empty,true));
    [HttpPut("orders/{id:guid}/fulfillment")] public async Task<IActionResult> Status(Guid id,StatusUpdate input) { await shop.UpdateFulfillment(id,input.Status);return NoContent(); }
}
[ApiController,Route("api/payments")]
public sealed class PaymentsController(CommerceService shop,IPaymentGateway gateway) : ControllerBase
{
    [HttpPost("stripe/webhook"),AllowAnonymous,IgnoreAntiforgeryToken,RequestSizeLimit(1048576)]
    public async Task<IActionResult> Webhook()
    {
        using var reader=new StreamReader(Request.Body);var raw=await reader.ReadToEndAsync();
        var evt=gateway.VerifyEvent(raw,Request.Headers["Stripe-Signature"].ToString());
        if(evt is not null) await shop.PaymentEvent(evt);
        return Ok();
    }
}
