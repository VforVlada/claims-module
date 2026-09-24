import { ClaimStatus } from './enums';

export interface CauseOfLossCodeDto {
  id: string;
  code: string;
  description: string;
  perilCategory: string;
}

export interface ClaimStatusDto {
  status: ClaimStatus;
  allowedNextStatuses: ClaimStatus[];
}
