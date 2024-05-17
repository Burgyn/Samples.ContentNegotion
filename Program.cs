
using Microsoft.AspNetCore.Http.HttpResults;
using Samples.ContentNegotion;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

ContentNegotiationProvider.AddNegotiator<JsonNegotiator>();
ContentNegotiationProvider.AddNegotiator<XmlNegotiator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/products", () =>
{
    return Negotiation.Negotiate(new List<Product>() { new(1, "Product 1", 100) });
});

app.MapGet("/products/{id}", GetProduct);

app.Run();

static Results<ContentNegotiationResult<Product>, NotFound> GetProduct(int id)
{
    if (id == 1)
    {
        return Negotiation.Negotiate(new Product(1, "Product 1", 100));
    }
    else
    {
        return TypedResults.NotFound();
    }
}

public class Product
{
    public Product(int id, string name, decimal price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    public Product()
    {
    }

    public int? Id { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
}