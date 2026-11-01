using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StyleMart.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestTypeToOrderReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestType",
                table: "OrderReturns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "OrderReturns");
        }
    }
}
