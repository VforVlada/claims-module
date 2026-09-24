import { ApprovalTier, canApproveTier, estimateApprovalTier } from './enums';

describe('estimateApprovalTier (F-07)', () => {
  it('uses the ReserveAuthorityEvaluator boundaries (inclusive upper bounds)', () => {
    expect(estimateApprovalTier(0.01)).toBe('Auto');
    expect(estimateApprovalTier(10_000)).toBe('Auto');
    expect(estimateApprovalTier(10_000.01)).toBe('Supervisor');
    expect(estimateApprovalTier(100_000)).toBe('Supervisor');
    expect(estimateApprovalTier(100_000.01)).toBe('Manager');
  });
});

describe('canApproveTier (F-11)', () => {
  it('Handlers approve nothing', () => {
    expect(canApproveTier('Handler', ApprovalTier.Supervisor)).toBeFalse();
    expect(canApproveTier('Handler', ApprovalTier.Manager)).toBeFalse();
  });

  it('Supervisors approve Supervisor-tier changes only', () => {
    expect(canApproveTier('Supervisor', ApprovalTier.Supervisor)).toBeTrue();
    expect(canApproveTier('Supervisor', ApprovalTier.Manager)).toBeFalse();
  });

  it('Managers approve Supervisor- and Manager-tier changes', () => {
    expect(canApproveTier('Manager', ApprovalTier.Supervisor)).toBeTrue();
    expect(canApproveTier('manager', ApprovalTier.Manager)).toBeTrue();
  });

  it('nobody approves Auto-tier changes or when signed out', () => {
    expect(canApproveTier('Manager', ApprovalTier.Auto)).toBeFalse();
    expect(canApproveTier(null, ApprovalTier.Supervisor)).toBeFalse();
  });
});
