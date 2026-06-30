using System;

namespace PayoffEngine;

public class AutocallablePricer : IInstrumentPricer
{
    public string InstrumentType => "Autocallable";


    public PricingResult Price(PricingRequest req)
    {
        decimal redemption = 0m;
        decimal accrued = 0m;
        string  scenario = "";
        bool    barrierReached = req.PricePath.Any(p => p <= req.Barrier);
        bool    earlyExit = false;
        
        int i = 0;
        while (i < req.PricePath.Length)
        {
            if (req.PricePath[i] >= req.Strike)
            {
                earlyExit = true;
                accrued =  req.Notional * req.CouponRate * ((i + 1m) / req.PricePath.Length);
                scenario = $"AutocalledAtPeriod{i + 1}";
                break ;
            }
            i++;
        }

        if (earlyExit)
        {
            return new PricingResult(
            req.Notional,
            accrued,
            scenario,
            barrierReached
        );
        }
        else if (barrierReached && req.PricePath[^1] < req.Strike)
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
