import { AssetType, ClaimStatus, ClaimType, DocumentType, PartyRole, PartyType } from './enums';
import { ReserveComponentDto } from './reserve.models';

export interface LossEventDto {
  lossDate: string;
  description: string;
  location: string;
  causeOfLossCodeId: string;
  causeOfLossCode?: string | null;
  causeOfLossDescription?: string | null;
}

export interface ClaimPartyDto {
  id: string;
  partyType: PartyType;
  partyRole: PartyRole;
  name: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
}

export interface ClaimRiskObjectDto {
  id: string;
  assetType: AssetType;
  description: string;
  identifier?: string | null;
}

export interface ClaimDocumentDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  documentType: DocumentType;
  uploadedBy: string;
  createdAt: string;
  downloadUrl: string;
}

export interface ClaimAuditLogDto {
  id: string;
  action: string;
  oldValues?: string | null;
  newValues?: string | null;
  performedBy: string;
  createdAt: string;
}

export interface ClaimListItemDto {
  id: string;
  claimNumber: string;
  policyNumber?: string | null;
  clientName?: string | null;
  lossDate: string;
  causeOfLossCode: string;
  status: ClaimStatus;
  assignedHandler: string;
  totalReserve: number;
  currency: string;
  /** Set by the SLA job for Draft/Open claims untouched for 48h+; cleared by any activity. */
  isSlaBreached: boolean;
  slaBreachedAt: string | null;
}

export interface ClaimDetailDto {
  id: string;
  claimNumber: string;
  policyId?: string | null;
  policyNumber?: string | null;
  clientName?: string | null;
  claimType: ClaimType;
  status: ClaimStatus;
  assignedHandler: string;
  requiresManagerOverride: boolean;
  lossEvent: LossEventDto;
  parties: ClaimPartyDto[];
  riskObjects: ClaimRiskObjectDto[];
  reserveComponents: ReserveComponentDto[];
  createdAt: string;
  isSlaBreached: boolean;
  slaBreachedAt: string | null;
}

export interface ClaimPartyInput {
  partyType: PartyType;
  partyRole: PartyRole;
  name: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
}

export interface ClaimRiskObjectInput {
  assetType: AssetType;
  description: string;
  identifier?: string | null;
}

export interface InitialReserveInput {
  componentType: ReserveComponentDto['componentType'];
  /** Positive, or negative for RecoveryReserve. */
  amount: number;
  currency?: string;
}

export interface CreateClaimRequest {
  policyId?: string | null;
  claimType: ClaimType;
  lossDate: string;
  lossDescription: string;
  lossLocation: string;
  causeOfLossCodeId: string;
  assignedHandler: string;
  parties: ClaimPartyInput[];
  riskObjects: ClaimRiskObjectInput[];
  initialReserve?: InitialReserveInput | null;
}

export interface AddPartyRequest {
  partyType: PartyType;
  partyRole: PartyRole;
  name: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
}

export interface ClaimListFilter {
  statuses?: ClaimStatus[];
  fromDate?: string;
  toDate?: string;
  assignedHandler?: string;
  causeOfLossCodeId?: string;
  pageNumber: number;
  pageSize: number;
}
