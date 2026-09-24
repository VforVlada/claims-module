import { Browser, BrowserContext, Locator, Page, expect } from '@playwright/test';
import { ClaimsApi, RoleKey, Session } from './api';

const STORAGE_KEY = 'claims-module.currentUser';

/** Collects console errors and uncaught page errors (E2E-07). */
export function trackConsoleErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(`[console] ${message.text()} @ ${page.url()}`);
  });
  page.on('pageerror', (error) => errors.push(`[pageerror] ${error.message} @ ${page.url()}`));
  return errors;
}

/** Signs in by seeding AuthService's localStorage entry before any app script runs (fast path for non-login tests). */
export async function seedSession(context: BrowserContext, session: Session): Promise<void> {
  await context.addInitScript(
    ([key, value]) => window.localStorage.setItem(key, value),
    [STORAGE_KEY, JSON.stringify({ token: session.token, userId: session.userId, userName: session.userName, role: session.role })] as const
  );
}

export interface RolePage {
  page: Page;
  context: BrowserContext;
  api: ClaimsApi;
  consoleErrors: string[];
}

/** A fresh browser context signed in as the given role — lets one test drive handler + supervisor side by side. */
export async function pageAs(browser: Browser, role: RoleKey): Promise<RolePage> {
  const api = await ClaimsApi.as(role);
  const context = await browser.newContext();
  await seedSession(context, api.session);
  const page = await context.newPage();
  return { page, context, api, consoleErrors: trackConsoleErrors(page) };
}

/** Signs in through the real login screen's role switcher. */
export async function signInViaUi(page: Page, roleLabel: 'Handler' | 'Supervisor' | 'Manager'): Promise<void> {
  await page.goto('/login');
  await page.getByRole('button', { name: `Sign in as ${roleLabel}`, exact: true }).click();
  await expect(page).toHaveURL(/\/claims$/);
}

export async function openClaim(page: Page, claimId: string): Promise<void> {
  await page.goto(`/claims/${claimId}`);
  await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
}

export async function openTab(page: Page, name: 'Overview' | 'Parties' | 'Reserves' | 'Documents' | 'Audit Log'): Promise<void> {
  await page.getByRole('tab', { name }).click();
  await expect(page.getByRole('tab', { name })).toHaveAttribute('aria-selected', 'true');
}

/** The reserve card for a component type, e.g. "Indemnity Reserve". */
export function reserveCard(page: Page, componentLabel: string): Locator {
  return page.locator('mat-card').filter({ has: page.locator('mat-card-title', { hasText: componentLabel }) });
}

/** History rows (body rows only) in a reserve card's table. */
export function historyRows(card: Locator): Locator {
  return card.locator('tbody').getByRole('row');
}

/**
 * The active FNOL step panel. Inactive steps stay in the DOM (hidden), and the horizontal
 * mat-stepper sets no aria-expanded on its panels, so select the one panel that is visible.
 */
export function activeStep(page: Page): Locator {
  return page.locator('[role="tabpanel"]:visible');
}

/**
 * GL posting is an async Hangfire job: reload the claim and re-open the Audit Log tab until the
 * action shows up in the UI.
 */
export async function expectAuditEntryEventually(page: Page, action: string, timeout = 60_000): Promise<void> {
  await expect(async () => {
    await page.reload();
    await openTab(page, 'Audit Log');
    await expect(page.getByRole('cell', { name: action, exact: true }).first()).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout, intervals: [1_000, 2_000, 3_000] });
}

/** Confirms the shared ConfirmDialog, optionally typing a reason. */
export async function confirmDialog(page: Page, confirmLabel: string, reason?: string): Promise<void> {
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  if (reason !== undefined) {
    await dialog.getByLabel('Reason').fill(reason);
  }
  await dialog.getByRole('button', { name: confirmLabel, exact: true }).click();
  await expect(dialog).toBeHidden();
}
