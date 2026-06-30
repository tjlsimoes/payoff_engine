namespace PayoffEngine;

public record class PricingRequest(
    string      InstrumentType,     // .e.g "BarrierReverseConvertiblePricer"
    long        Notional,           // initial value, e.g. 1000
    decimal     Strike,             // % of initial value, e.g. 1.00 = inital value
    decimal     Barrier,            // % of initial value, e.g. 0.70
    decimal     CouponRate,         // annual, e.g. 0.08
    decimal[]   PricePath           // observed underlying prices over the term, as % of initial value
);