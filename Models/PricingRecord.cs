
namespace PayoffEngine;


public class PricingRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string InstrumentType { get; set; }
    public required decimal Notional { get; init; }
    public required decimal Strike { get; init; }
    public required decimal Barrier { get; init; }
    public required decimal CouponRate { get; init; }
    public required string PricePath { get; init; }


    public required decimal Redemption { get; init; }
    public required decimal CouponPaid { get; init; }
    public required string Scenario { get; init; }
    public required bool BarrierBreached { get; init; }
    public DateTime PricedAtUtc { get; init; } = DateTime.UtcNow;
}