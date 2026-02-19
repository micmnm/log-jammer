using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogJammer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastIngestAtToDataSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_ingest_at",
                table: "data_sources",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_ingest_at",
                table: "data_sources");
        }
    }
}
