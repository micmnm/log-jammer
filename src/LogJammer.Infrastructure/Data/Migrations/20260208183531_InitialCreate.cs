using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace LogJammer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "data_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    adapter_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    connection_config = table.Column<string>(type: "jsonb", nullable: false),
                    poll_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    schema_mapping = table.Column<string>(type: "jsonb", nullable: true),
                    sampling_budget = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tag_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fingerprint_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    data_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    normalize_before_hash = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fingerprint_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_fingerprint_configs_data_sources_data_source_id",
                        column: x => x.data_source_id,
                        principalTable: "data_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "known_errors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fingerprint_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    representative_message = table.Column<string>(type: "text", nullable: false),
                    representative_stack_trace = table.Column<string>(type: "text", nullable: true),
                    embedding_vector = table.Column<Vector>(type: "vector(384)", nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_seen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_occurrences = table.Column<long>(type: "bigint", nullable: false),
                    occurrence_windows = table.Column<string>(type: "jsonb", nullable: true),
                    data_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_known_errors", x => x.id);
                    table.ForeignKey(
                        name: "FK_known_errors_data_sources_data_source_id",
                        column: x => x.data_source_id,
                        principalTable: "data_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    known_error_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    threshold_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    threshold_value = table.Column<double>(type: "double precision", nullable: false),
                    actual_value = table.Column<double>(type: "double precision", nullable: false),
                    notification_count = table.Column<int>(type: "integer", nullable: false),
                    last_notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                    table.ForeignKey(
                        name: "FK_alerts_known_errors_known_error_id",
                        column: x => x.known_error_id,
                        principalTable: "known_errors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "classification_queue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    known_error_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_tags = table.Column<string>(type: "jsonb", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    reviewed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classification_queue", x => x.id);
                    table.ForeignKey(
                        name: "FK_classification_queue_known_errors_known_error_id",
                        column: x => x.known_error_id,
                        principalTable: "known_errors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "error_occurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    known_error_id = table.Column<Guid>(type: "uuid", nullable: false),
                    window_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    window_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    count = table.Column<long>(type: "bigint", nullable: false),
                    sample_ratio = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_error_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "FK_error_occurrences_known_errors_known_error_id",
                        column: x => x.known_error_id,
                        principalTable: "known_errors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "error_tags",
                columns: table => new
                {
                    known_error_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_auto_assigned = table.Column<bool>(type: "boolean", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_error_tags", x => new { x.known_error_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_error_tags_known_errors_known_error_id",
                        column: x => x.known_error_id,
                        principalTable: "known_errors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_error_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    known_error_id = table.Column<Guid>(type: "uuid", nullable: false),
                    override_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    override_data = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_overrides_known_errors_known_error_id",
                        column: x => x.known_error_id,
                        principalTable: "known_errors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_known_error_id",
                table: "alerts",
                column: "known_error_id");

            migrationBuilder.CreateIndex(
                name: "IX_classification_queue_known_error_id",
                table: "classification_queue",
                column: "known_error_id");

            migrationBuilder.CreateIndex(
                name: "IX_classification_queue_reviewed",
                table: "classification_queue",
                column: "reviewed",
                filter: "reviewed = false");

            migrationBuilder.CreateIndex(
                name: "IX_error_occurrences_known_error_id_window_start",
                table: "error_occurrences",
                columns: new[] { "known_error_id", "window_start" });

            migrationBuilder.CreateIndex(
                name: "IX_error_tags_tag_id",
                table: "error_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_fingerprint_configs_data_source_id",
                table: "fingerprint_configs",
                column: "data_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_known_errors_data_source_id",
                table: "known_errors",
                column: "data_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_known_errors_fingerprint_hash",
                table: "known_errors",
                column: "fingerprint_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tags_name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_overrides_known_error_id",
                table: "user_overrides",
                column: "known_error_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "classification_queue");

            migrationBuilder.DropTable(
                name: "error_occurrences");

            migrationBuilder.DropTable(
                name: "error_tags");

            migrationBuilder.DropTable(
                name: "fingerprint_configs");

            migrationBuilder.DropTable(
                name: "user_overrides");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "known_errors");

            migrationBuilder.DropTable(
                name: "data_sources");
        }
    }
}
