// Mirrors ClaimsModule.Domain.Enums — numeric values must match the backend exactly,
// since JSON payloads carry the enum as an integer.

export enum ClaimStatus {
  Draft = 0,
  Open = 1,
  UnderInvestigation = 2,
  PendingPayment = 3,
  Closed = 4,
  Reopened = 5,
  Withdrawn = 6
}

export const ClaimStatusLabels: Record<ClaimStatus, string> = {
  [ClaimStatus.Draft]: 'Draft',
  [ClaimStatus.Open]: 'Open',
  [ClaimStatus.UnderInvestigation]: 'Under Investigation',
  [ClaimStatus.PendingPayment]: 'Pending Payment',
  [ClaimStatus.Closed]: 'Closed',
  [ClaimStatus.Reopened]: 'Reopened',
  [ClaimStatus.Withdrawn]: 'Withdrawn'
};

export enum ClaimType {
  Auto = 0,
  Property = 1,
  Liability = 2,
  Other = 3
}

export const ClaimTypeLabels: Record<ClaimType, string> = {
  [ClaimType.Auto]: 'Auto',
  [ClaimType.Property]: 'Property',
  [ClaimType.Liability]: 'Liability',
  [ClaimType.Other]: 'Other'
};

export enum PartyType {
  Individual = 0,
  Organization = 1
}

export const PartyTypeLabels: Record<PartyType, string> = {
  [PartyType.Individual]: 'Individual',
  [PartyType.Organization]: 'Organization'
};

export enum PartyRole {
  Claimant = 0,
  Insured = 1,
  ThirdParty = 2,
  Witness = 3,
  Attorney = 4,
  Adjuster = 5,
  Other = 6
}

export const PartyRoleLabels: Record<PartyRole, string> = {
  [PartyRole.Claimant]: 'Claimant',
  [PartyRole.Insured]: 'Insured',
  [PartyRole.ThirdParty]: 'Third Party',
  [PartyRole.Witness]: 'Witness',
  [PartyRole.Attorney]: 'Attorney',
  [PartyRole.Adjuster]: 'Adjuster',
  [PartyRole.Other]: 'Other'
};

export enum AssetType {
  Vehicle = 0,
  RealProperty = 1,
  PersonalProperty = 2,
  Other = 3
}

export const AssetTypeLabels: Record<AssetType, string> = {
  [AssetType.Vehicle]: 'Vehicle',
  [AssetType.RealProperty]: 'Real Property',
  [AssetType.PersonalProperty]: 'Personal Property',
  [AssetType.Other]: 'Other'
};

export enum ReserveComponentType {
  IndemnityReserve = 0,
  ExpenseReserve = 1,
  RecoveryReserve = 2,
  LitigationReserve = 3
}

export const ReserveComponentTypeLabels: Record<ReserveComponentType, string> = {
  [ReserveComponentType.IndemnityReserve]: 'Indemnity Reserve',
  [ReserveComponentType.ExpenseReserve]: 'Expense Reserve',
  [ReserveComponentType.RecoveryReserve]: 'Recovery Reserve',
  [ReserveComponentType.LitigationReserve]: 'Litigation Reserve'
};

/** Mirrors ClaimsModule.Domain.Enums.DocumentType (serialized as a number). */
export enum DocumentType {
  Other = 0,
  Photo = 1,
  PoliceReport = 2,
  RepairEstimate = 3,
  Invoice = 4,
  MedicalReport = 5,
  Correspondence = 6
}

export const DocumentTypeLabels: Record<DocumentType, string> = {
  [DocumentType.Other]: 'Other',
  [DocumentType.Photo]: 'Photo',
  [DocumentType.PoliceReport]: 'Police Report',
  [DocumentType.RepairEstimate]: 'Repair Estimate',
  [DocumentType.Invoice]: 'Invoice',
  [DocumentType.MedicalReport]: 'Medical Report',
  [DocumentType.Correspondence]: 'Correspondence'
};

/** Mirrors ClaimsModule.Domain.Enums.ReserveComponentStatus: a reserve line closes with its claim. */
export enum ReserveComponentStatus {
  Open = 0,
  Closed = 1
}

export enum ApprovalStatus {
  PendingApproval = 0,
  AutoApproved = 1,
  Approved = 2,
  Rejected = 3
}

export const ApprovalStatusLabels: Record<ApprovalStatus, string> = {
  [ApprovalStatus.PendingApproval]: 'Pending Approval',
  [ApprovalStatus.AutoApproved]: 'Auto-Approved',
  [ApprovalStatus.Approved]: 'Approved',
  [ApprovalStatus.Rejected]: 'Rejected'
};

export enum PostingStatus {
  NotPosted = 0,
  Posted = 1,
  Failed = 2
}

export const PostingStatusLabels: Record<PostingStatus, string> = {
  [PostingStatus.NotPosted]: 'Not Posted',
  [PostingStatus.Posted]: 'Posted',
  [PostingStatus.Failed]: 'Failed'
};

/** Mirrors ReserveAuthorityEvaluator's thresholds (Application layer) — client-side preview only. */
export type ApprovalTierName = 'Auto' | 'Supervisor' | 'Manager';

/** What the live authority indicator says for each tier (BR-R-02..04), shown in FNOL step 3 and the reserve form. */
export const ApprovalTierMessages: Record<ApprovalTierName, string> = {
  Auto: 'Auto-approved (up to $10,000)',
  Supervisor: 'Requires Supervisor approval ($10,000.01 – $100,000)',
  Manager: 'Requires Manager approval (over $100,000)'
};

export function estimateApprovalTier(absAmount: number): ApprovalTierName {
  if (absAmount <= 10_000) return 'Auto';
  if (absAmount <= 100_000) return 'Supervisor';
  return 'Manager';
}

/** Mirrors ClaimsModule.Domain.Enums.ApprovalTier — the tier the backend stamped on a reserve change. */
export enum ApprovalTier {
  Auto = 0,
  Supervisor = 1,
  Manager = 2
}

/** Mirrors ReserveAuthorityEvaluator.CanApprove: Supervisor-tier changes need Supervisor or Manager, Manager-tier need Manager. */
export function canApproveTier(role: string | null | undefined, tier: ApprovalTier): boolean {
  const normalized = role?.toLowerCase();
  switch (tier) {
    case ApprovalTier.Supervisor:
      return normalized === 'supervisor' || normalized === 'manager';
    case ApprovalTier.Manager:
      return normalized === 'manager';
    default:
      return false;
  }
}

/** Sorts enum values alphabetically by their display label — the order every dropdown shows them in. */
export function sortByLabel<T extends number>(values: readonly T[], labels: Record<T, string>): T[] {
  return [...values].sort((a, b) => labels[a].localeCompare(labels[b]));
}

/** All values of a numeric enum, as dropdown options sorted alphabetically by label. */
export function optionsByLabel<T extends number>(labels: Record<T, string>): T[] {
  return sortByLabel(Object.keys(labels).map(Number) as T[], labels);
}

/** Mirrors ClaimsModule.Application.Policies.Dtos.PolicyStatus: derived from the policy's effective period. */
export enum PolicyStatus {
  Active = 0,
  Expired = 1,
  NotYetEffective = 2
}

export const PolicyStatusLabels: Record<PolicyStatus, string> = {
  [PolicyStatus.Active]: 'In force',
  [PolicyStatus.Expired]: 'Expired',
  [PolicyStatus.NotYetEffective]: 'Not yet effective'
};
