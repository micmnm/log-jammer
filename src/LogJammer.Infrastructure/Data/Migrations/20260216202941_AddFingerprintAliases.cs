using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogJammer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFingerprintAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fingerprint_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fingerprint_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    known_error_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fingerprint_aliases", x => x.id);
                    table.ForeignKey(
                        name: "FK_fingerprint_aliases_known_errors_known_error_id",
                        column: x => x.known_error_id,
                        principalTable: "known_errors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fingerprint_aliases_fingerprint_hash",
                table: "fingerprint_aliases",
                column: "fingerprint_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fingerprint_aliases_known_error_id",
                table: "fingerprint_aliases",
                column: "known_error_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fingerprint_aliases");
        }
    }
}
