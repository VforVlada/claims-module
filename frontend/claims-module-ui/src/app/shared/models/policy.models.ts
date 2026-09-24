import { PolicyStatus } from './enums';

export interface PolicySearchResultDto {
  policyId: string;
  policyNumber: string;
  clientName: string;
  effectiveDate: string;
  expirationDate: string;
  status: PolicyStatus;
}

export interface PolicyCoverageDto {
  id: string;
  coverageType: string;
  limit: number;
  deductible: number;
  currency: string;
}

