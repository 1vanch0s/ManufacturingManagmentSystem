using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ManufacturingManagmentSystem.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "materials",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    unitofmeasure = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    minimalstock = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    specifications = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    minimalstock = table.Column<int>(type: "integer", nullable: false),
                    productiontimeperunit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productmaterials",
                columns: table => new
                {
                    productid = table.Column<int>(type: "integer", nullable: false),
                    materialid = table.Column<int>(type: "integer", nullable: false),
                    quantityneeded = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productmaterials", x => new { x.productid, x.materialid });
                    table.ForeignKey(
                        name: "fk_productmaterials_materials_materialid",
                        column: x => x.materialid,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_productmaterials_products_productid",
                        column: x => x.productid,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "productionlines",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Stopped"),
                    efficiencyfactor = table.Column<float>(type: "real", nullable: false),
                    currentworkorderid = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_productionlines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workorders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<int>(type: "integer", nullable: false),
                    productionlineid = table.Column<int>(type: "integer", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    startdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estimatedenddate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workorders", x => x.id);
                    table.ForeignKey(
                        name: "FK_workorders_productionlines_productionlineid",
                        column: x => x.productionlineid,
                        principalTable: "productionlines",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_workorders_products_productid",
                        column: x => x.productid,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_productionlines_currentworkorderid",
                table: "productionlines",
                column: "currentworkorderid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_productmaterials_materialid",
                table: "productmaterials",
                column: "materialid");

            migrationBuilder.CreateIndex(
                name: "ix_workorders_productid",
                table: "workorders",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_workorders_productionlineid",
                table: "workorders",
                column: "productionlineid");

            migrationBuilder.AddForeignKey(
                name: "FK_productionlines_workorders_currentworkorderid",
                table: "productionlines",
                column: "currentworkorderid",
                principalTable: "workorders",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_productionlines_workorders_currentworkorderid",
                table: "productionlines");

            migrationBuilder.DropTable(
                name: "productmaterials");

            migrationBuilder.DropTable(
                name: "materials");

            migrationBuilder.DropTable(
                name: "workorders");

            migrationBuilder.DropTable(
                name: "productionlines");

            migrationBuilder.DropTable(
                name: "products");
        }
    }
}
