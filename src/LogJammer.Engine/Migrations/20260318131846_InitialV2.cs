using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogJammer.Engine.Migrations
{
    /// <inheritdoc />
    public partial class InitialV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ConnectionConfig = table.Column<string>(type: "jsonb", nullable: false),
                    MessageTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastPolledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrainStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerializedState = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrainStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrainStates_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Template = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ClusterId = table.Column<int>(type: "integer", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SampleMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsNew = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogPatterns_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatternBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatternId = table.Column<Guid>(type: "uuid", nullable: false),
                    HourOfWeek = table.Column<int>(type: "integer", nullable: false),
                    AvgCount = table.Column<double>(type: "double precision", nullable: false),
                    StdDevCount = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatternBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatternBaselines_LogPatterns_PatternId",
                        column: x => x.PatternId,
                        principalTable: "LogPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatternOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatternId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WindowEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatternOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatternOccurrences_LogPatterns_PatternId",
                        column: x => x.PatternId,
                        principalTable: "LogPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrainStates_DataSourceId",
                table: "DrainStates",
                column: "DataSourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogPatterns_DataSourceId",
                table: "LogPatterns",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_PatternBaselines_PatternId_HourOfWeek",
                table: "PatternBaselines",
                columns: new[] { "PatternId", "HourOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatternOccurrences_PatternId_WindowStart",
                table: "PatternOccurrences",
                columns: new[] { "PatternId", "WindowStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrainStates");

            migrationBuilder.DropTable(
                name: "PatternBaselines");

            migrationBuilder.DropTable(
                name: "PatternOccurrences");

            migrationBuilder.DropTable(
                name: "LogPatterns");

            migrationBuilder.DropTable(
                name: "DataSources");
        }
    }
}
