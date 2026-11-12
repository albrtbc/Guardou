using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guardou.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_CreatedBy",
                table: "Items");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CreatedBy_Kind",
                table: "Items",
                columns: new[] { "CreatedBy", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_ParentId",
                table: "Items",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Trashed",
                table: "Items",
                column: "Trashed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_CreatedBy_Kind",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_ParentId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_Trashed",
                table: "Items");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CreatedBy",
                table: "Items",
                column: "CreatedBy");
        }
    }
}
