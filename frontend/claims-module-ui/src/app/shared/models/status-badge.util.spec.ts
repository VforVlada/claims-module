import { approvalStatusBadge, claimStatusBadge, postingStatusBadge } from './status-badge.util';
import { ApprovalStatus, ClaimStatus, PostingStatus } from './enums';

describe('status-badge.util (F-10)', () => {
  it('maps every claim status to a label and colour', () => {
    expect(claimStatusBadge(ClaimStatus.Draft)).toEqual({ label: 'Draft', color: 'neutral' });
    expect(claimStatusBadge(ClaimStatus.Open)).toEqual({ label: 'Open', color: 'info' });
    expect(claimStatusBadge(ClaimStatus.UnderInvestigation)).toEqual({ label: 'Under Investigation', color: 'orange' });
    expect(claimStatusBadge(ClaimStatus.PendingPayment)).toEqual({ label: 'Pending Payment', color: 'purple' });
    expect(claimStatusBadge(ClaimStatus.Closed)).toEqual({ label: 'Closed', color: 'success' });
    expect(claimStatusBadge(ClaimStatus.Reopened)).toEqual({ label: 'Reopened', color: 'teal' });
    expect(claimStatusBadge(ClaimStatus.Withdrawn)).toEqual({ label: 'Withdrawn', color: 'brown' });
  });

  it('gives every claim status a distinct colour', () => {
    const statuses = Object.values(ClaimStatus).filter((v) => typeof v === 'number') as ClaimStatus[];
    const colours = statuses.map((s) => claimStatusBadge(s).color);
    expect(new Set(colours).size).toBe(statuses.length);
  });

  it('maps approval statuses', () => {
    expect(approvalStatusBadge(ApprovalStatus.PendingApproval).color).toBe('warning');
    expect(approvalStatusBadge(ApprovalStatus.AutoApproved)).toEqual({ label: 'Auto-Approved', color: 'success' });
    expect(approvalStatusBadge(ApprovalStatus.Approved).color).toBe('success');
    expect(approvalStatusBadge(ApprovalStatus.Rejected).color).toBe('danger');
  });

  it('maps posting statuses', () => {
    expect(postingStatusBadge(PostingStatus.NotPosted).color).toBe('neutral');
    expect(postingStatusBadge(PostingStatus.Posted).color).toBe('success');
    expect(postingStatusBadge(PostingStatus.Failed).color).toBe('danger');
  });
});
