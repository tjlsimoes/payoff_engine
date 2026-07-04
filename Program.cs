using PayoffEngine;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddInstrumentPricers();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<PayoffEngineDbContext>();
builder.AddPayoffEngineDb();

var app = builder.Build();

// if (app.Environment.IsDevelopment())
app.MapOpenApi();                       //  /openapi/v1.json
app.MapScalarApiReference();            //  /scalar/v1
app.MapHealthChecks("/health");

app.MapGet("/", () => "Hello World!").WithDescription("Default endpoint. Returns 'Hello World!'");

app.MapPricingEndpoints();

app.MigrateDb();
app.Run();
