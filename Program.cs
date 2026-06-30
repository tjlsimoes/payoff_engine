using PayoffEngine;

var builder = WebApplication.CreateBuilder(args);

builder.AddInstrumentPricers();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPricingEndpoints();

app.Run();
