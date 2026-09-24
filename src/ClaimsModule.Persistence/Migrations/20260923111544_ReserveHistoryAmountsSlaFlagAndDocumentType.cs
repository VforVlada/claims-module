using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimsModule.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReserveHistoryAmountsSlaFlagAndDocumentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangeReason",
                table: "ReserveHistories",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NewAmount",
                table: "ReserveHistories",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "NewCurrency",
                table: "ReserveHistories",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousAmount",
                table: "ReserveHistories",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PreviousCurrency",
                table: "ReserveHistories",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSlaBreached",
                table: "Claims",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaBreachedAt",
                table: "Claims",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "ClaimDocuments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Other");

            // Backfill: existing history rows only stored the change (Amount). A row's previous
            // amount is the balance its component had from earlier balance-contributing changes.
            migrationBuilder.Sql("""
                UPDATE h SET
                    h.PreviousAmount = ISNULL(prev.Total, 0),
                    h.NewAmount = ISNULL(prev.Total, 0) + h.Amount,
                    h.PreviousCurrency = h.Currency,
                    h.NewCurrency = h.Currency
                FROM ReserveHistories h
                OUTER APPLY (
                    SELECT SUM(e.Amount) AS Total
                    FROM ReserveHistories e
                    WHERE e.ReserveComponentId = h.ReserveComponentId
                      AND e.ChangeSequence < h.ChangeSequence
                      AND e.ApprovalStatus IN ('AutoApproved', 'Approved')
                ) prev;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangeReason",
                table: "ReserveHistories");

            migrationBuilder.DropColumn(
                name: "NewAmount",
                table: "ReserveHistories");

            migrationBuilder.DropColumn(
                name: "NewCurrency",
                table: "ReserveHistories");

            migrationBuilder.DropColumn(
                name: "PreviousAmount",
                table: "ReserveHistories");

            migrationBuilder.DropColumn(
                name: "PreviousCurrency",
                table: "ReserveHistories");

            migrationBuilder.DropColumn(
                name: "IsSlaBreached",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "SlaBreachedAt",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "ClaimDocuments");
        }
    }
}
