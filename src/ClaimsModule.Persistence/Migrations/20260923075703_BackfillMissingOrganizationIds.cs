using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimsModule.Persistence.Migrations
{
    /// <summary>
    /// Data repair, no schema change. ReserveHistory rows used to be created without an
    /// OrganizationEntityId, and audit rows written by background jobs could be stamped with
    /// Guid.Empty — once the tenant query filter existed, both became invisible to their own
    /// organization. Each row inherits its parent's tenant.
    /// </summary>
    public partial class BackfillMissingOrganizationIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE rh SET rh.OrganizationEntityId = rc.OrganizationEntityId
                FROM ReserveHistories rh
                JOIN ClaimReserveComponents rc ON rc.Id = rh.ReserveComponentId
                WHERE rh.OrganizationEntityId = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.Sql("""
                UPDATE a SET a.OrganizationEntityId = c.OrganizationEntityId
                FROM ClaimAuditLogs a
                JOIN Claims c ON c.Id = a.ClaimId
                WHERE a.OrganizationEntityId = '00000000-0000-0000-0000-000000000000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible data repair: restoring Guid.Empty would only re-hide the rows.
        }
    }
}
