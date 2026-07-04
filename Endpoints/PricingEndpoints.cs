using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace PayoffEngine;

public static class PricingEndpoints
{

    static PricingRecord ToRecord(PricingRequest req, PricingResult ret) => new()
    {
        InstrumentType  = req.InstrumentType,
        Notional        = req.Notional,
        Strike          = req.Strike,
        Barrier         = req.Barrier,
        CouponRate      = req.CouponRate,
        PricePath       = JsonSerializer.Serialize(req.PricePath),
        Redemption      = ret.Redemption,
        CouponPaid      = ret.CouponPaid,
        Scenario        = ret.Scenario,
        BarrierBreached = ret.BarrierBreached
    };

    public static void MapPricingEndpoints(this WebApplication app)
    {

        app.MapGet("/instruments", (IEnumerable<IInstrumentPricer> pricers) =>
        {
            return Results.Ok(pricers.Select(p => p.InstrumentType));
        }).WithDescription("List available instrument pricers.");

        // POST /price
        app.MapPost("/price", async (PayoffEngineDbContext db, PricingRequest req, IEnumerable<IInstrumentPricer> pricers, ILogger<Program> logger) =>
        {

                foreach (var p in pricers)
                {
                    if (p.InstrumentType == req.InstrumentType)
                    {
                        PricingResult ret = p.Price(req);
                        PricingRecord record = ToRecord(req, ret);
                        try
                        {
                            logger.LogInformation($"Calculated {req.InstrumentType} payoff - Scenario: {ret.Scenario} - Redemption: {ret.Redemption} - CouponPaid: {ret.CouponPaid}");
                            db.Add(record);
                            await db.SaveChangesAsync();
                            return Results.Ok(ret);
                        }
                        catch (Exception e) when ( e is Microsoft.EntityFrameworkCore.DbUpdateException || e is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
                        {
                            logger.LogError(e, "Falied to persist pricing record");
                            return Results.Problem("Error on database persistence");
                        }
                    }
                }

            return Results.BadRequest($"Unknown instrument type: {req.InstrumentType}");
        }).WithDescription("Calculate payoff for a given instrument pricer and price history data.");



        // POST /price/batch
        app.MapPost("/price/batch", async (PayoffEngineDbContext db, PricingRequest[] requests, IEnumerable<IInstrumentPricer> pricers, ILogger<Program> logger) =>
        {
            List<Task<PricingResult>> allTasks = [];

            try
            {
                foreach (var req in requests)
                    allTasks.Add(PriceOneAsync(req, pricers));

                await Task.WhenAll(allTasks);
                var results = allTasks.Select(task => task.Result);
                var records = requests.Zip(results, (req, ret) => ToRecord(req, ret));
                foreach (var rec in records)
                    logger.LogInformation($"Calculated {rec.InstrumentType} payoff - Scenario: {rec.Scenario} - Redemption: {rec.Redemption} - CouponPaid: {rec.CouponPaid}");
                db.AddRange(records);
                await db.SaveChangesAsync();
                return Results.Ok(results);
            }
            catch (BadHttpRequestException e)
            {
                return Results.BadRequest($"{e.Message}");
            }
            catch (Exception e) when ( e is Microsoft.EntityFrameworkCore.DbUpdateException || e is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                logger.LogError(e, "Falied to persist pricing record");
                return Results.Problem("Error on database persistence");
            }
        }).WithDescription("Calculate payoff for a given instrument pricer and price history data in bulk.");

        app.MapGet("/history", async (PayoffEngineDbContext db, string? instrumentType) =>
        {
            IQueryable<PricingRecord> query = db.PricingRecords.OrderByDescending(p => p.PricedAtUtc);
            if (!String.IsNullOrEmpty(instrumentType))
                query = query.Where(p => p.InstrumentType == instrumentType);
            return Results.Ok(await query.ToListAsync());
        }).WithDescription("Get history of previous pricing requests and respective responses.");


    }

    async private static Task<PricingResult> PriceOneAsync(PricingRequest req, IEnumerable<IInstrumentPricer> pricers)
    {
        await Task.Delay(20000);    // 30 seconds
        foreach (var p in pricers)
        {
            if (p.InstrumentType == req.InstrumentType)
                return p.Price(req);
        }
        throw new BadHttpRequestException($"Unknown instrument type: {req.InstrumentType}");
    }
}
