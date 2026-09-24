using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimsModule.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReserveComponentStatusAndApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "ClaimReserveComponents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "AutoApproved");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ClaimReserveComponents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Open");

            // Data backfill only (the schema above is EF-generated). Existing lines take the
            // approval status of their latest change, and close if their claim is closed/withdrawn,
            // matching what the domain now maintains for new rows.
            migrationBuilder.Sql("""
                UPDATE rc SET rc.ApprovalStatus = latest.ApprovalStatus
                FROM ClaimReserveComponents rc
                CROSS APPLY (
                    SELECT TOP (1) h.ApprovalStatus
                    FROM ReserveHistories h
                    WHERE h.ReserveComponentId = rc.Id
                    ORDER BY h.ChangeSequence DESC
                ) latest;
                """);

            migrationBuilder.Sql("""
                UPDATE rc SET rc.Status = 'Closed'
                FROM ClaimReserveComponents rc
                JOIN Claims c ON c.Id = rc.ClaimId
                WHERE c.Status IN ('Closed', 'Withdrawn');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "ClaimReserveComponents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ClaimReserveComponents");
        }
    }
}
