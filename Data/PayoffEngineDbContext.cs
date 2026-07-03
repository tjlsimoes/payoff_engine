using Microsoft.EntityFrameworkCore;

namespace PayoffEngine;

public class PayoffEngineDbContext(DbContextOptions<PayoffEngineDbContext> options) : DbContext(options)
{
    public DbSet<PricingRecord> PricingRecords => Set<PricingRecord>();
}