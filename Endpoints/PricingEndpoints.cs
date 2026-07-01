using System;

namespace PayoffEngine;

public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this WebApplication app)
    {

        app.MapGet("/instruments", (IEnumerable<IInstrumentPricer> pricers) =>
        {
            return Results.Ok(pricers.Select(p => p.InstrumentType));
        });

        // POST /price
        app.MapPost("/price", (PricingRequest req, IEnumerable<IInstrumentPricer> pricers) =>
        {
            foreach (var p in pricers)
            {
                if (p.InstrumentType == req.InstrumentType)
                    return Results.Ok(p.Price(req));
            }

            return Results.BadRequest($"Unknown instrument type: {req.InstrumentType}");
        });



        // POST /price/batch
        app.MapPost("/price/batch", async (PricingRequest[] requests, IEnumerable<IInstrumentPricer> pricers) =>
        {
            List<Task<PricingResult>> allTasks = [];

            try
            {
                foreach (var req in requests)
                    allTasks.Add(PriceOneAsync(req, pricers));

                await Task.WhenAll(allTasks);
                return Results.Ok(allTasks.Select(task => task.Result));
            }
            catch (BadHttpRequestException e)
            {
                return Results.BadRequest($"{e.Message}");
            }
        });
    }

    async private static Task<PricingResult> PriceOneAsync(PricingRequest req, IEnumerable<IInstrumentPricer> pricers)
    {
        await Task.Delay(30000);    // 30 seconds
        foreach (var p in pricers)
        {
            if (p.InstrumentType == req.InstrumentType)
                return p.Price(req);
        }
        throw new BadHttpRequestException($"Unknown instrument type: {req.InstrumentType}");
    }
}
