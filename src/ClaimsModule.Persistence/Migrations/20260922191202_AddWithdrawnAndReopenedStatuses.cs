using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClaimsModule.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawnAndReopenedStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "IsDeleted", "OrganizationEntityId", "RequiredPermission", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000125"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Draft", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000126"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Draft", false, new Guid("11111111-1111-1111-1111-111111111111"), "Supervisor", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000127"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Draft", false, new Guid("11111111-1111-1111-1111-111111111111"), "Manager", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000128"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Open", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000129"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Open", false, new Guid("11111111-1111-1111-1111-111111111111"), "Supervisor", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000130"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Open", false, new Guid("11111111-1111-1111-1111-111111111111"), "Manager", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000131"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnderInvestigation", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000132"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnderInvestigation", false, new Guid("11111111-1111-1111-1111-111111111111"), "Supervisor", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000133"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "UnderInvestigation", false, new Guid("11111111-1111-1111-1111-111111111111"), "Manager", "Withdrawn", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000134"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Closed", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "Reopened", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000135"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Closed", false, new Guid("11111111-1111-1111-1111-111111111111"), "Supervisor", "Reopened", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000136"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Closed", false, new Guid("11111111-1111-1111-1111-111111111111"), "Manager", "Reopened", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000137"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "UnderInvestigation", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000138"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Supervisor", "UnderInvestigation", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000139"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Manager", "UnderInvestigation", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000140"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "PendingPayment", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000141"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Supervisor", "PendingPayment", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000142"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Manager", "PendingPayment", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000143"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "Closed", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000144"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Supervisor", "Closed", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000145"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reopened", false, new Guid("11111111-1111-1111-1111-111111111111"), "Manager", "Closed", null, "seed", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000125"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000126"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000127"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000128"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000129"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000130"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000131"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000132"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000133"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000134"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000135"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000136"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000137"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000138"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000139"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000140"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000141"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000142"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000143"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000144"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000145"));
        }
    }
}
