using System;

namespace PayoffEngine;

public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this WebApplication app)
    {
        app.MapPost("/price", (PricingRequest req, IEnumerable<IInstrumentPricer> pricers) =>
        {
            foreach (var p in pricers)
            {
                if (p.InstrumentType == req.InstrumentType)
                    return Results.Ok(p.Price(req));
            }

            return Results.BadRequest($"Unknown instrument type: {req.InstrumentType}");
        });
    }
}
