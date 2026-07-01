using System;

namespace PayoffEngine;

public class BarrierReverseConvertiblePricer : IInstrumentPricer
{
    public string InstrumentType => "BarrierReverseConvertible";


    public PricingResult Price(PricingRequest req)
    {
        decimal redemption = 0m;
        string  scenario = "";
        bool    barrierReached = req.PricePath.Any(p => p <= req.Barrier);

        if (barrierReached && req.PricePath[^1] < req.Strike)
        {
            scenario = "BelowStrikeBarrierBreached";
            redemption = req.Notional * (req.PricePath[^1] / req.Strike);
        }
        else if (req.PricePath[^1] < req.Strike)
        {
            scenario = "BelowStrikeBarrierIntact";
            redemption = req.Notional;
        }
        else
        {
            scenario = "AtOrAboveStrike";
            redemption = req.Notional;
        }

        PricingResult result = new(
            redemption,
            req.Notional * req.CouponRate,
            scenario,
            barrierReached
        );
        return result;
    }
}
