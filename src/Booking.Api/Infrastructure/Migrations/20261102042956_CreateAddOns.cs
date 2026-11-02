using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateAddOns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddOns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LeadTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    AvailableFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    AvailableUntil = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    FulfillerId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddOns", x => x.Id);
                    table.CheckConstraint("CK_AddOns_Hours", "\"AvailableUntil\" > \"AvailableFrom\"");
                    table.CheckConstraint("CK_AddOns_LeadTime", "\"LeadTimeMinutes\" >= 0");
                    table.ForeignKey(
                        name: "FK_AddOns_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AddOns_Users_FulfillerId",
                        column: x => x.FulfillerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AddOnOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddOnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddOnOrders", x => x.Id);
                    table.CheckConstraint("CK_AddOnOrders_Quantity", "\"Quantity\" >= 1");
                    table.ForeignKey(
                        name: "FK_AddOnOrders_AddOns_AddOnId",
                        column: x => x.AddOnId,
                        principalTable: "AddOns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AddOnOrders_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddOnOrders_AddOnId",
                table: "AddOnOrders",
                column: "AddOnId");

            migrationBuilder.CreateIndex(
                name: "IX_AddOnOrders_BookingId",
                table: "AddOnOrders",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_AddOns_FulfillerId",
                table: "AddOns",
                column: "FulfillerId");

            migrationBuilder.CreateIndex(
                name: "IX_AddOns_LocationId_Name",
                table: "AddOns",
                columns: new[] { "LocationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddOnOrders");

            migrationBuilder.DropTable(
                name: "AddOns");
        }
    }
}
