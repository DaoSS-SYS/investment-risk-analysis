using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RiskAnalysis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorporateActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "corporate_actions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instrument_id = table.Column<int>(type: "integer", nullable: false),
                    action_date = table.Column<DateOnly>(type: "date", nullable: false),
                    action_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ratio = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    adjustment_factor = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    observed_ratio = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    observed_log_return = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    is_applied = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_corporate_actions", x => x.id);
                    table.ForeignKey(
                        name: "fk_corporate_actions_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Аномалии ценовых рядов и их классификация: корпоративные действия и рыночные события");

            migrationBuilder.CreateIndex(
                name: "ix_corporate_actions_instrument_id_action_date",
                table: "corporate_actions",
                columns: new[] { "instrument_id", "action_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "corporate_actions");
        }
    }
}
