using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayoffEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstrumentType = table.Column<string>(type: "TEXT", nullable: false),
                    Notional = table.Column<decimal>(type: "TEXT", nullable: false),
                    Strike = table.Column<decimal>(type: "TEXT", nullable: false),
                    Barrier = table.Column<decimal>(type: "TEXT", nullable: false),
                    CouponRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    PricePath = table.Column<string>(type: "TEXT", nullable: false),
                    Redemption = table.Column<decimal>(type: "TEXT", nullable: false),
                    CouponPaid = table.Column<decimal>(type: "TEXT", nullable: false),
                    Scenario = table.Column<string>(type: "TEXT", nullable: false),
                    BarrierBreached = table.Column<bool>(type: "INTEGER", nullable: false),
                    PricedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingRecords");
        }
    }
}
