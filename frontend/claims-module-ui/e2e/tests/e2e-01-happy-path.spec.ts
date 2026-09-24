import { expect, test } from '@playwright/test';
import { ClaimsApi } from '../support/api';
import { activeStep, expectAuditEntryEventually, historyRows, openTab, reserveCard, signInViaUi, trackConsoleErrors } from '../support/ui';

// E2E-01 (@smoke): a handler logs a claim through all three FNOL steps with a 5,000 reserve. The reserve is
// within Auto authority, so it is approved immediately and the async GL job posts it to the audit log.
test('E2E-01 handler creates a claim with a 5,000 reserve via FNOL; auto-approved and GL-posted @smoke', async ({ page }) => {
  const consoleErrors = trackConsoleErrors(page);
  await signInViaUi(page, 'Handler');

  await page.getByRole('button', { name: /Log New Claim/ }).click();
  await expect(page).toHaveURL(/\/claims\/new$/);

  // Step 1 — Policy & Loss (loss date defaults to today)
  let step = activeStep(page);
  await step.getByRole('switch', { name: /Unknown policy/ }).click();
  await step.getByLabel('Assigned Handler').fill('Hannah Handler');
  await step.getByRole('combobox', { name: /Cause of Loss/ }).click();
  await page.getByRole('option').first().click();
  await step.getByLabel('Loss Location').fill('E2E Main St & 5th Ave');
  await step.getByLabel('Loss Description').fill('E2E-01: rear-ended at a junction');
  await step.getByRole('button', { name: 'Next' }).click();

  // Step 2 — Parties (a Claimant row is pre-added)
  step = activeStep(page);
  await expect(step.getByRole('heading', { name: 'Parties' })).toBeVisible();
  await step.getByLabel('Name', { exact: true }).fill('Jane Claimant');
  await step.getByRole('button', { name: 'Next' }).click();

  // Step 3 — Reserve & Review
  step = activeStep(page);
  await step.getByRole('switch', { name: /Open an initial reserve/ }).click();
  await step.getByLabel('Amount').fill('5000');
  await expect(step.getByText('Requires Auto approval')).toBeVisible();
  await step.getByRole('button', { name: 'Submit Claim' }).click();

  // Success banner with the claim number and a link
  const banner = page.getByRole('status').filter({ hasText: 'created' });
  await expect(banner).toBeVisible();
  const claimNumber = (await banner.locator('strong').innerText()).trim();
  expect(claimNumber).toMatch(/\S+/);
  await banner.getByRole('link', { name: 'View claim' }).click();

  await expect(page).toHaveURL(/\/claims\/[0-9a-f-]{36}$/);
  await expect(page.getByRole('heading', { name: claimNumber })).toBeVisible();
  const claimId = page.url().split('/').pop()!;

  // Reserve is approved (Auto tier)
  await openTab(page, 'Reserves');
  const rows = historyRows(reserveCard(page, 'Indemnity Reserve'));
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('$5,000.00');
  await expect(rows.first()).toContainText(/Approved/);

  // GL entry arrives asynchronously (Hangfire) — poll via reload, and double-check server-side.
  await expectAuditEntryEventually(page, 'GL_POSTING_SIMULATED');
  const api = await ClaimsApi.as('handler');
  expect((await api.auditLog(claimId)).map((e) => e.action)).toEqual(expect.arrayContaining(['CLAIM_CREATED', 'GL_POSTING_SIMULATED']));

  expect(consoleErrors).toEqual([]);
});
