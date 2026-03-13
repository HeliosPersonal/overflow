using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overflow.EstimationService.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantIsPresent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPresent",
                table: "Participants",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPresent",
                table: "Participants");
        }
    }
}
