using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace LogJammer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassificationConfigAndTagCentroids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "classification_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classification_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tag_centroids",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    centroid_vector = table.Column<Vector>(type: "vector(384)", nullable: true),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_centroids", x => x.id);
                    table.ForeignKey(
                        name: "FK_tag_centroids_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_classification_config_key",
                table: "classification_config",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tag_centroids_tag_id",
                table: "tag_centroids",
                column: "tag_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "classification_config");

            migrationBuilder.DropTable(
                name: "tag_centroids");
        }
    }
}
