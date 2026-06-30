using System;

namespace PayoffEngine;

public interface IInstrumentPricer
{
    public string InstrumentType { get; }

    public PricingResult   Price(PricingRequest req);
}
