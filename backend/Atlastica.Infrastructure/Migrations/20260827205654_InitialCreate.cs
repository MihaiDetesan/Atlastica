using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlastica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    iso_code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    boundary_geojson = table.Column<string>(type: "jsonb", nullable: false),
                    boundary_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.iso_code);
                });

            migrationBuilder.CreateTable(
                name: "points_of_interest",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    short_description = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    wikipedia_url = table.Column<string>(type: "text", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_points_of_interest", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "country_facts",
                columns: table => new
                {
                    iso_code = table.Column<string>(type: "text", nullable: false),
                    population = table.Column<long>(type: "bigint", nullable: true),
                    government_form = table.Column<string>(type: "text", nullable: true),
                    official_languages = table.Column<List<string>>(type: "text[]", nullable: false),
                    area_km2 = table.Column<double>(type: "double precision", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: true),
                    main_mountains = table.Column<string>(type: "text", nullable: true),
                    main_rivers = table.Column<string>(type: "text", nullable: true),
                    biodiversity_facts = table.Column<string>(type: "text", nullable: true),
                    source_extract_hash = table.Column<string>(type: "text", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_country_facts", x => x.iso_code);
                    table.ForeignKey(
                        name: "FK_country_facts_countries_iso_code",
                        column: x => x.iso_code,
                        principalTable: "countries",
                        principalColumn: "iso_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_points_of_interest_slug",
                table: "points_of_interest",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "country_facts");

            migrationBuilder.DropTable(
                name: "points_of_interest");

            migrationBuilder.DropTable(
                name: "countries");
        }
    }
}
