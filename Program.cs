using PayoffEngine;

var builder = WebApplication.CreateBuilder(args);

builder.AddInstrumentPricers();
builder.Services.AddValidation();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPricingEndpoints();

app.Run();
