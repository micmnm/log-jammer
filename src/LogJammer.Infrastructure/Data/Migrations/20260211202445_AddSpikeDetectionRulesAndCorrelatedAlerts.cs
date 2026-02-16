using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogJammer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSpikeDetectionRulesAndCorrelatedAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "consecutive_below_threshold",
                table: "alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "correlated_spike_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    data_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    alert_ids = table.Column<string>(type: "text", nullable: false),
                    group_count = table.Column<int>(type: "integer", nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correlated_spike_alerts", x => x.id);
                    table.ForeignKey(
                        name: "FK_correlated_spike_alerts_data_sources_data_source_id",
                        column: x => x.data_source_id,
                        principalTable: "data_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spike_detection_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    known_error_id = table.Column<Guid>(type: "uuid", nullable: true),
                    threshold_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    threshold_value = table.Column<double>(type: "double precision", nullable: false),
                    window_minutes = table.Column<int>(type: "integer", nullable: false),
                    lookback_minutes = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spike_detection_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_spike_detection_rules_known_errors_known_error_id",
                        column: x => x.known_error_id,
                        principalTable: "known_errors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_correlated_spike_alerts_data_source_id",
                table: "correlated_spike_alerts",
                column: "data_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_spike_detection_rules_known_error_id",
                table: "spike_detection_rules",
                column: "known_error_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "correlated_spike_alerts");

            migrationBuilder.DropTable(
                name: "spike_detection_rules");

            migrationBuilder.DropColumn(
                name: "consecutive_below_threshold",
                table: "alerts");
        }
    }
}
