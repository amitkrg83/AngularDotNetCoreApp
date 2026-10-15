using Commerce.Application.Products;
using Commerce.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddAuthorization();

// One shared store for the lifetime of this application process.
builder.Services.AddSingleton<
    IProductRepository,
    InMemoryProductRepository>();

// A service instance per HTTP request.
builder.Services.AddScoped<
    IProductService,
    ProductService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
