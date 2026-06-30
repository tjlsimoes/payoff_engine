using System;

namespace PayoffEngine;

public static class PricerExtensions
{
    public static void AddInstrumentPricers(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IInstrumentPricer, BarrierReverseConvertiblePricer>();
        builder.Services.AddSingleton<IInstrumentPricer, AutocallablePricer>();
    }
}
