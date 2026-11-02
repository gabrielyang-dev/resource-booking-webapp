using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateRuleSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSettings", x => x.Id);
                    table.CheckConstraint("CK_RuleSettings_One_Owner", "(\"ResourceTypeId\" IS NULL) <> (\"ResourceId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_RuleSettings_ResourceTypes_ResourceTypeId",
                        column: x => x.ResourceTypeId,
                        principalTable: "ResourceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RuleSettings_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleSettings_ResourceId_Kind",
                table: "RuleSettings",
                columns: new[] { "ResourceId", "Kind" },
                unique: true,
                filter: "\"ResourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RuleSettings_ResourceTypeId_Kind",
                table: "RuleSettings",
                columns: new[] { "ResourceTypeId", "Kind" },
                unique: true,
                filter: "\"ResourceTypeId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleSettings");
        }
    }
}
