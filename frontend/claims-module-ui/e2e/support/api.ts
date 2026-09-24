import { APIRequestContext, expect, request as playwrightRequest } from '@playwright/test';
import { API_URL } from './env';

/** Mock-login role keys accepted by POST /api/auth/mock-login. */
export type RoleKey = 'handler' | 'supervisor' | 'manager';

export interface Session {
  token: string;
  userId: string;
  userName: string;
  role: string;
}

export interface ClaimDetail {
  id: string;
  claimNumber: string;
  status: number;
  isSlaBreached: boolean;
  slaBreachedAt: string | null;
  reserveComponents: {
    id: string;
    componentType: number;
    currentAmount: number;
    /** amount = newAmount - previousAmount (the change); changeReason is "Initial reserve" on the first row. */
    history: {
      id: string;
      changeSequence: number;
      previousAmount: number;
      newAmount: number;
      amount: number;
      changeReason: string | null;
      approvalStatus: number;
      postingStatus: number;
    }[];
  }[];
}

export interface AuditEntry {
  action: string;
  newValues?: string | null;
  oldValues?: string | null;
  performedBy: string;
}

/** Numeric enums as serialised by the API (mirrors src/app/shared/models/enums.ts). */
export const ClaimStatusLabel: Record<number, string> = {
  0: 'Draft',
  1: 'Open',
  2: 'Under Investigation',
  3: 'Pending Payment',
  4: 'Closed',
  5: 'Reopened',
  6: 'Withdrawn'
};

export async function newApiContext(): Promise<APIRequestContext> {
  return playwrightRequest.newContext({ baseURL: API_URL });
}

export async function mockLogin(api: APIRequestContext, role: RoleKey): Promise<Session> {
  const response = await api.post('/api/auth/mock-login', { data: { role } });
  expect(response.ok(), `mock-login as ${role}: ${response.status()}`).toBeTruthy();
  return (await response.json()) as Session;
}

/** Thin typed client over the Claims API for test setup and server-side assertions. */
export class ClaimsApi {
  constructor(
    private readonly api: APIRequestContext,
    readonly session: Session
  ) {}

  static async as(role: RoleKey): Promise<ClaimsApi> {
    const api = await newApiContext();
    return new ClaimsApi(api, await mockLogin(api, role));
  }

  private get headers(): Record<string, string> {
    return { Authorization: `Bearer ${this.session.token}` };
  }

  private async json<T>(response: Awaited<ReturnType<APIRequestContext['get']>>, what: string): Promise<T> {
    expect(response.ok(), `${what}: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
    return (await response.json()) as T;
  }

  async firstCauseOfLossCodeId(): Promise<string> {
    const codes = await this.json<{ id: string }[]>(
      await this.api.get('/api/reference/cause-of-loss-codes', { headers: this.headers }),
      'list cause-of-loss codes'
    );
    expect(codes.length, 'seeded cause-of-loss codes').toBeGreaterThan(0);
    return codes[0].id;
  }

  /** Creates a Draft claim (no policy) with one Claimant, optionally with an initial Indemnity reserve. */
  async createClaim(initialReserveAmount?: number): Promise<ClaimDetail> {
    const yesterday = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString();
    const result = await this.json<{ value: ClaimDetail }>(
      await this.api.post('/api/claims', {
        headers: this.headers,
        data: {
          policyId: null,
          claimType: 0,
          lossDate: yesterday,
          lossDescription: `E2E claim created ${new Date().toISOString()}`,
          lossLocation: 'E2E Test Street 1',
          causeOfLossCodeId: await this.firstCauseOfLossCodeId(),
          assignedHandler: this.session.userName,
          parties: [{ partyType: 0, partyRole: 0, name: 'E2E Claimant', contactEmail: 'claimant@example.com' }],
          riskObjects: [],
          initialReserve: initialReserveAmount ? { componentType: 0, amount: initialReserveAmount, currency: 'USD' } : null
        }
      }),
      'create claim'
    );
    return result.value;
  }

  /** Opens a reserve component. Amounts are positive, except RecoveryReserve (componentType 2), which must be negative. */
  async openReserve(claimId: string, amount: number, componentType = 0, reason?: string): Promise<void> {
    await this.json(
      await this.api.post(`/api/claims/${claimId}/reserves`, {
        headers: this.headers,
        data: { componentType, amount, currency: 'USD', ...(reason ? { reason } : {}) }
      }),
      'open reserve'
    );
  }

  /**
   * Adjusts a reserve component to a NEW TOTAL (not a delta) — PUT /api/claims/{id}/reserves/{reserveId}.
   * The reason is mandatory; submitting the component's current amount is rejected with 422.
   */
  async adjustReserve(claimId: string, reserveId: string, newAmount: number, reason: string, managerOverrideConfirmed = false): Promise<void> {
    await this.json(
      await this.api.put(`/api/claims/${claimId}/reserves/${reserveId}`, {
        headers: this.headers,
        data: { amount: newAmount, reason, currency: 'USD', ...(managerOverrideConfirmed ? { managerOverrideConfirmed } : {}) }
      }),
      'adjust reserve'
    );
  }

  async getClaim(claimId: string): Promise<ClaimDetail> {
    return this.json<ClaimDetail>(await this.api.get(`/api/claims/${claimId}`, { headers: this.headers }), 'get claim');
  }

  async auditLog(claimId: string): Promise<AuditEntry[]> {
    const page = await this.json<{ items: AuditEntry[] }>(
      await this.api.get(`/api/claims/${claimId}/audit`, { headers: this.headers, params: { pageNumber: 1, pageSize: 50 } }),
      'get audit log'
    );
    return page.items;
  }

  async claimStatuses(): Promise<{ status: number; allowedNextStatuses: number[] }[]> {
    return this.json(await this.api.get('/api/reference/claim-statuses', { headers: this.headers }), 'list claim statuses');
  }

  /** Waits for the async Hangfire GL job to write its audit entry (server-side check). */
  async waitForAuditAction(claimId: string, action: string, minCount = 1, timeout = 60_000): Promise<void> {
    await expect
      .poll(async () => (await this.auditLog(claimId)).filter((e) => e.action === action).length, {
        message: `waiting for ${minCount}x ${action} on claim ${claimId}`,
        timeout,
        intervals: [1_000, 2_000, 3_000]
      })
      .toBeGreaterThanOrEqual(minCount);
  }

  async fetchAnonymous(url: string): Promise<Buffer> {
    const response = await this.api.get(url);
    expect(response.ok(), `GET ${url}: HTTP ${response.status()}`).toBeTruthy();
    return response.body();
  }
}
