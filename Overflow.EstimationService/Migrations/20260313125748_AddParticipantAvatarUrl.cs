using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overflow.EstimationService.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantAvatarUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Participants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Participants");
        }
    }
}
