using System.ComponentModel.DataAnnotations;

namespace PayoffEngine;

public record class PricingRequest(
    [Required(ErrorMessage = "InstrumentType is required")]
    [StringLength(50, ErrorMessage = "InstrumentType cannot be longer than 50 characters")]
    string      InstrumentType,     // .e.g "BarrierReverseConvertiblePricer"

    [Required(ErrorMessage = "Notional is required")]
    [RegularExpression(@"^\d+\.?\d{0,2}$", ErrorMessage = "Invalid Notional")]
    [Range(1, 100_000, ErrorMessage = "Total must be between 1 and 100_000")]
    long        Notional,           // initial value, e.g. 1000

    [Required(ErrorMessage = "Strike is required")]
    [RegularExpression(@"^\d+\.?\d{0,2}$", ErrorMessage = "Invalid Strike")]
    [Range(1, 5, ErrorMessage = "Total must be between 1 and 5")]
    decimal     Strike,             // % of initial value, e.g. 1.00 = inital value

    [Required(ErrorMessage = "Barrier is required")]
    [RegularExpression(@"^\d+\.?\d{0,2}$", ErrorMessage = "Invalid Barrier")]
    [Range(0, 1, ErrorMessage = "Total must be between 0 and 1")]
    decimal     Barrier,            // % of initial value, e.g. 0.70

    [Required(ErrorMessage = "CouponRate is required")]
    [RegularExpression(@"^\d+\.?\d{0,2}$", ErrorMessage = "Invalid CouponRate")]
    [Range(0, 1, ErrorMessage = "Total must be between 0 and 1")]
    decimal     CouponRate,         // annual, e.g. 0.08

    [MinLength(1)]
    decimal[]   PricePath           // observed underlying prices over the term, as % of initial value
);