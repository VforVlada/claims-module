using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClaimsModule.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestingSeedDataAndWorkflowPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000104"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000105"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000106"));

            migrationBuilder.DeleteData(
                table: "ClaimStatusTransitions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000134"));

            migrationBuilder.InsertData(
                table: "CauseOfLossCodes",
                columns: new[] { "Id", "Code", "CreatedAt", "DeletedAt", "Description", "IsActive", "IsDeleted", "OrganizationEntityId", "PerilCategory", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-0000000000ca"), "LEGACY", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Legacy / retired peril", false, false, new Guid("11111111-1111-1111-1111-111111111111"), "Other", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-0000000000cb"), "B-COLL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Collision (Org B)", true, false, new Guid("22222222-2222-2222-2222-222222222222"), "Auto", null, "seed", null }
                });

            migrationBuilder.InsertData(
                table: "Policies",
                columns: new[] { "Id", "ClientName", "CreatedAt", "DeletedAt", "EffectiveDate", "ExpirationDate", "IsDeleted", "OrganizationEntityId", "PolicyNumber", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[] { new Guid("00000000-0000-0000-0000-0000000000a4"), "Short-Term Events Co", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 9, 7, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, new Guid("11111111-1111-1111-1111-111111111111"), "POL-2026-000404", null, "seed", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CauseOfLossCodes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-0000000000ca"));

            migrationBuilder.DeleteData(
                table: "CauseOfLossCodes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-0000000000cb"));

            migrationBuilder.DeleteData(
                table: "Policies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-0000000000a4"));

            migrationBuilder.InsertData(
                table: "ClaimStatusTransitions",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "FromStatus", "IsDeleted", "OrganizationEntityId", "RequiredPermission", "ToStatus", "UpdatedAt", "UserCreated", "UserModified" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000104"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Draft", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "Closed", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000105"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Draft", false, new Guid("11111111-1111-1111-1111-111111111111"), "Supervisor", "Closed", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000106"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Draft", false, new Guid("11111111-1111-1111-1111-111111111111"), "Manager", "Closed", null, "seed", null },
                    { new Guid("00000000-0000-0000-0000-000000000134"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Closed", false, new Guid("11111111-1111-1111-1111-111111111111"), "Handler", "Reopened", null, "seed", null }
                });
        }
    }
}
