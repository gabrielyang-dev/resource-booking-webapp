using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attendees = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.CheckConstraint("CK_Bookings_End_After_Start", "\"End\" > \"Start\"");
                    table.ForeignKey(
                        name: "FK_Bookings_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OwnerId_IdempotencyKey",
                table: "Bookings",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OwnerId_Start",
                table: "Bookings",
                columns: new[] { "OwnerId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ResourceId_Start",
                table: "Bookings",
                columns: new[] { "ResourceId", "Start" });

            // Postgres is the final guard against double bookings: the same resource cannot hold
            // overlapping [Start, End) ranges while the status holds the slot.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                "ALTER TABLE \"Bookings\" ADD CONSTRAINT \"EX_Bookings_No_Overlap\" " +
                "EXCLUDE USING gist (\"ResourceId\" WITH =, tstzrange(\"Start\", \"End\", '[)') WITH &&) " +
                "WHERE (\"Status\" IN ('Pending','Confirmed','CheckedIn'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");
        }
    }
}
