using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingManagmentSystem.Migrations
{
    /// <inheritdoc />
    public partial class LaptopCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_productionlines_currentworkorderid",
                table: "productionlines");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "workorders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_productionlines_currentworkorderid",
                table: "productionlines",
                column: "currentworkorderid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_productionlines_currentworkorderid",
                table: "productionlines");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "workorders",
                type: "text",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_productionlines_currentworkorderid",
                table: "productionlines",
                column: "currentworkorderid",
                unique: true);
        }
    }
}
