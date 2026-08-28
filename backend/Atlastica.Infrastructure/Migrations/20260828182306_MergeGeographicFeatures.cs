using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlastica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MergeGeographicFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "main_mountains",
                table: "country_facts");

            migrationBuilder.RenameColumn(
                name: "main_rivers",
                table: "country_facts",
                newName: "notable_geographic_features");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "notable_geographic_features",
                table: "country_facts",
                newName: "main_rivers");

            migrationBuilder.AddColumn<string>(
                name: "main_mountains",
                table: "country_facts",
                type: "text",
                nullable: true);
        }
    }
}
