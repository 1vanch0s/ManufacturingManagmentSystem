using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingManagmentSystem.Migrations
{
    /// <inheritdoc />
    public partial class Orders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workorders_productionlines_productionlineid",
                table: "workorders");

            migrationBuilder.RenameIndex(
                name: "ix_workorders_productionlineid",
                table: "workorders",
                newName: "IX_workorders_productionlineid");

            migrationBuilder.AddForeignKey(
                name: "FK_workorders_productionlines_productionlineid",
                table: "workorders",
                column: "productionlineid",
                principalTable: "productionlines",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workorders_productionlines_productionlineid",
                table: "workorders");

            migrationBuilder.RenameIndex(
                name: "IX_workorders_productionlineid",
                table: "workorders",
                newName: "ix_workorders_productionlineid");

            migrationBuilder.AddForeignKey(
                name: "fk_workorders_productionlines_productionlineid",
                table: "workorders",
                column: "productionlineid",
                principalTable: "productionlines",
                principalColumn: "id");
        }
    }
}
