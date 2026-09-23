using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RiskAnalysis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instruments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    short_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    full_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    security_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    engine = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    market = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    board = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    isin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    sector = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lot_size = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    history_from = table.Column<DateOnly>(type: "date", nullable: true),
                    history_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instruments", x => x.id);
                },
                comment: "Справочник финансовых инструментов");

            migrationBuilder.CreateTable(
                name: "macro_indicators",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_macro_indicators", x => x.id);
                },
                comment: "Макроэкономические показатели Банка России");

            migrationBuilder.CreateTable(
                name: "import_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    instrument_id = table.Column<int>(type: "integer", nullable: true),
                    date_from = table.Column<DateOnly>(type: "date", nullable: true),
                    date_to = table.Column<DateOnly>(type: "date", nullable: true),
                    rows_received = table.Column<int>(type: "integer", nullable: false),
                    rows_inserted = table.Column<int>(type: "integer", nullable: false),
                    rows_updated = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    message = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_logs_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Журнал загрузки данных из внешних источников");

            migrationBuilder.CreateTable(
                name: "portfolios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    base_currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    benchmark_instrument_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolios", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolios_instruments_benchmark_instrument_id",
                        column: x => x.benchmark_instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Инвестиционные портфели, выступающие объектом анализа риска");

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instrument_id = table.Column<int>(type: "integer", nullable: false),
                    trade_date = table.Column<DateOnly>(type: "date", nullable: false),
                    open = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    high = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    low = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    close = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    volume = table.Column<long>(type: "bigint", nullable: true),
                    turnover = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotes", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotes_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "История дневных котировок финансовых инструментов");

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    portfolio_id = table.Column<int>(type: "integer", nullable: false),
                    instrument_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    purchase_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.id);
                    table.ForeignKey(
                        name: "fk_positions_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_positions_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Позиции портфеля по финансовым инструментам");

            migrationBuilder.CreateTable(
                name: "risk_calculations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<int>(type: "integer", nullable: false),
                    calculation_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: true),
                    error_message = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_calculations", x => x.id);
                    table.ForeignKey(
                        name: "fk_risk_calculations_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Журнал расчётов: параметры запуска и результаты");

            migrationBuilder.CreateIndex(
                name: "ix_import_logs_instrument_id",
                table: "import_logs",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_logs_started_at",
                table: "import_logs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_is_active",
                table: "instruments",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_ticker_board",
                table: "instruments",
                columns: new[] { "ticker", "board" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_macro_indicators_code_date",
                table: "macro_indicators",
                columns: new[] { "code", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_benchmark_instrument_id",
                table: "portfolios",
                column: "benchmark_instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_owner_user_id",
                table: "portfolios",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_instrument_id",
                table: "positions",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_portfolio_id",
                table: "positions",
                column: "portfolio_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_instrument_id_trade_date",
                table: "quotes",
                columns: new[] { "instrument_id", "trade_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotes_trade_date",
                table: "quotes",
                column: "trade_date");

            migrationBuilder.CreateIndex(
                name: "ix_risk_calculations_portfolio_id_created_at",
                table: "risk_calculations",
                columns: new[] { "portfolio_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_calculations_status",
                table: "risk_calculations",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_logs");

            migrationBuilder.DropTable(
                name: "macro_indicators");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "quotes");

            migrationBuilder.DropTable(
                name: "risk_calculations");

            migrationBuilder.DropTable(
                name: "portfolios");

            migrationBuilder.DropTable(
                name: "instruments");
        }
    }
}
