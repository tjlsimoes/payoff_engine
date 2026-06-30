namespace PayoffEngine;

public record class PricingResult(
    decimal     Redemption,         // cash or share-equivalent value returned at maturity
    decimal     CouponPaid,         // coupon component (paid in all scenarios for a BRC)
    string      Scenario,           // which branch fired, for explainability
    bool        BarrierBreached
);