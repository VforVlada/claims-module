export interface PolicySearchResultDto {
  id: string;
  policyNumber: string;
  clientName: string;
  effectiveDate: string;
  expirationDate: string;
  isInForce: boolean;
}

export interface PolicyCoverageDto {
  id: string;
  coverageType: string;
  limit: number;
  deductible: number;
  currency: string;
}
