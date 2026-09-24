import { ApprovalStatus, ApprovalTier, PostingStatus, ReserveComponentType } from './enums';

export interface ReserveHistoryDto {
  id: string;
  changeSequence: number;
  /** The component's amount before this change. */
  previousAmount: number;
  /** The component's new total after this change. */
  newAmount: number;
  /** The change itself: newAmount - previousAmount (negative for a decrease). */
  amount: number;
  currency: string;
  /** Authority tier the backend evaluated for this change (drives who may approve it). */
  requiredTier: ApprovalTier;
  approvalStatus: ApprovalStatus;
  postingStatus: PostingStatus;
  /** Why the reserve was changed; "Initial reserve" on the first row. */
  changeReason: string | null;
  /** ChangedBy in the brief's terms. */
  requestedBy: string;
  decidedBy?: string | null;
  decidedAt?: string | null;
  rejectionReason?: string | null;
  createdAt: string;
}

export interface ReserveComponentDto {
  id: string;
  claimId: string;
  componentType: ReserveComponentType;
  currentAmount: number;
  currency: string;
  history: ReserveHistoryDto[];
}

export interface OpenReserveRequest {
  componentType: ReserveComponentType;
  /** Positive for every component except RecoveryReserve, which must be negative. */
  amount: number;
  currency?: string;
  reason?: string | null;
  managerOverrideConfirmed?: boolean;
}

export interface AdjustReserveRequest {
  /** The component's NEW TOTAL (not a delta). Positive, or negative for RecoveryReserve. */
  amount: number;
  /** Required, max 1000 characters. */
  reason: string;
  currency?: string;
  managerOverrideConfirmed?: boolean;
}

export interface ReserveHistoryActionRequest {
  reserveHistoryId: string;
  /** Approve only: a Manager confirming that this approval may take the claim over the $10M aggregate cap (BR-R-07). */
  managerOverrideConfirmed?: boolean;
}

export interface RejectReserveRequest {
  reserveHistoryId: string;
  reason: string;
}
