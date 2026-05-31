using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SenthilApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexLinkUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Links_Url",
                table: "Links",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Links_Url",
                table: "Links");
        }
    }
}
