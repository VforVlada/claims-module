import { signal } from '@angular/core';
import { ClaimDetailDto } from '../shared/models/claim.models';
import { ApprovalStatus, ApprovalTier, ClaimStatus, ClaimType, PostingStatus, ReserveComponentStatus, ReserveComponentType } from '../shared/models/enums';
import { ReserveComponentDto, ReserveHistoryDto } from '../shared/models/reserve.models';
import { PagedList } from '../shared/models/common.models';
import { CurrentUser } from '../core/services/auth.service';

export const API = 'http://localhost:5299/api';

/** Minimal AuthService double exposing the signals the app reads. */
export function authStub(role: 'Handler' | 'Supervisor' | 'Manager' | null, token = 'test-token') {
  const user = signal<CurrentUser | null>(role ? { token, userId: `user-${role.toLowerCase()}`, userName: `${role} User`, role } : null);
  return {
    currentUser: user.asReadonly(),
    role: () => user()?.role ?? null,
    isAuthenticated: () => user() !== null,
    canApproveReserves: () => role === 'Supervisor' || role === 'Manager',
    logout: jasmine.createSpy('logout').and.callFake(() => user.set(null)),
    login: jasmine.createSpy('login'),
    listRoles: jasmine.createSpy('listRoles')
  };
}

export function emptyPage<T>(items: T[] = []): PagedList<T> {
  return { items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

/**
 * A reserve history row. `amount` is the change (newAmount - previousAmount); pass `amount` alone
 * for a first row from zero, or previousAmount/newAmount for an adjustment.
 */
export function historyEntry(overrides: Partial<ReserveHistoryDto> = {}): ReserveHistoryDto {
  const previousAmount = overrides.previousAmount ?? 0;
  const newAmount = overrides.newAmount ?? previousAmount + (overrides.amount ?? 50_000);
  return {
    id: 'hist-1',
    changeSequence: 1,
    previousAmount,
    newAmount,
    amount: newAmount - previousAmount,
    changeReason: previousAmount === 0 ? 'Initial reserve' : 'Revised estimate',
    currency: 'USD',
    requiredTier: ApprovalTier.Supervisor,
    approvalStatus: ApprovalStatus.PendingApproval,
    postingStatus: PostingStatus.NotPosted,
    requestedBy: 'Hannah Handler',
    createdAt: '2026-09-01T10:00:00Z',
    ...overrides
  };
}

export function reserveComponent(id: string, history: ReserveHistoryDto[]): ReserveComponentDto {
  return {
    id,
    claimId: 'claim-1',
    componentType: ReserveComponentType.IndemnityReserve,
    currentAmount: history[history.length - 1]?.newAmount ?? 0,
    currency: 'USD',
    status: ReserveComponentStatus.Open,
    // Mirrors the API: the latest change's approval status (history is oldest-first here).
    approvalStatus: history[history.length - 1]?.approvalStatus ?? ApprovalStatus.AutoApproved,
    history
  };
}

export function claimDetail(overrides: Partial<ClaimDetailDto> = {}): ClaimDetailDto {
  return {
    id: 'claim-1',
    claimNumber: 'CLM-2026-000123',
    policyId: 'policy-1',
    policyNumber: 'POL-2026-000001',
    clientName: 'Acme Corp',
    claimType: ClaimType.Auto,
    status: ClaimStatus.Open,
    assignedHandler: 'Hannah Handler',
    requiresManagerOverride: false,
    lossEvent: {
      lossDate: '2026-09-01T00:00:00Z',
      description: 'Rear-ended at a junction',
      location: 'Main St',
      causeOfLossCodeId: 'col-1',
      causeOfLossCode: 'COLL',
      causeOfLossDescription: 'Collision'
    },
    parties: [],
    riskObjects: [],
    reserveComponents: [],
    createdAt: '2026-09-01T10:00:00Z',
    isSlaBreached: false,
    slaBreachedAt: null,
    ...overrides
  };
}
