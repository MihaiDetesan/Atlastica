using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlastica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCapital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "capital",
                table: "country_facts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "capital",
                table: "country_facts");
        }
    }
}
