using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogJammer.Engine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourceVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "DataSources",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "DataSources");
        }
    }
}
