import { BadgeColor } from '../components/status-badge/status-badge.component';
import { ApprovalStatus, ApprovalStatusLabels, ClaimStatus, ClaimStatusLabels, PostingStatus, PostingStatusLabels } from './enums';

/**
 * Claim status colours per the brief: Draft = gray, Open = blue, UnderInvestigation = orange,
 * PendingPayment = purple, Closed = green. Reopened (teal) and Withdrawn (brown) get their own
 * distinct tones so no two statuses share a colour.
 */
export function claimStatusBadge(status: ClaimStatus): { label: string; color: BadgeColor } {
  const color: Record<ClaimStatus, BadgeColor> = {
    [ClaimStatus.Draft]: 'neutral',
    [ClaimStatus.Open]: 'info',
    [ClaimStatus.UnderInvestigation]: 'orange',
    [ClaimStatus.PendingPayment]: 'purple',
    [ClaimStatus.Closed]: 'success',
    [ClaimStatus.Reopened]: 'teal',
    [ClaimStatus.Withdrawn]: 'brown'
  };
  return { label: ClaimStatusLabels[status], color: color[status] };
}

export function approvalStatusBadge(status: ApprovalStatus): { label: string; color: BadgeColor } {
  const color: Record<ApprovalStatus, BadgeColor> = {
    [ApprovalStatus.PendingApproval]: 'warning',
    [ApprovalStatus.AutoApproved]: 'success',
    [ApprovalStatus.Approved]: 'success',
    [ApprovalStatus.Rejected]: 'danger'
  };
  return { label: ApprovalStatusLabels[status], color: color[status] };
}

export function postingStatusBadge(status: PostingStatus): { label: string; color: BadgeColor } {
  const color: Record<PostingStatus, BadgeColor> = {
    [PostingStatus.NotPosted]: 'neutral',
    [PostingStatus.Posted]: 'success',
    [PostingStatus.Failed]: 'danger'
  };
  return { label: PostingStatusLabels[status], color: color[status] };
}
