import { expect, test } from '@playwright/test';
import { ClaimsApi } from '../support/api';
import { openTab, signInViaUi, trackConsoleErrors } from '../support/ui';

// E2E-07: walk every screen (and every claim-detail tab) and assert nothing was logged via console.error
// and no uncaught exception escaped, for each role.
for (const role of ['Handler', 'Supervisor', 'Manager'] as const) {
  test(`E2E-07 no console errors on any screen (${role})`, async ({ page }) => {
    const errors = trackConsoleErrors(page);
    const api = await ClaimsApi.as('handler');
    const claim = await api.createClaim(5_000);

    await page.goto('/login');
    await expect(page.getByRole('button', { name: /Sign in as/ }).first()).toBeVisible();

    await signInViaUi(page, role);
    await expect(page.getByRole('heading', { name: 'Claims', exact: true })).toBeVisible();
    await expect(page.getByRole('table')).toBeVisible();

    await page.goto('/claims/new');
    await expect(page.getByRole('heading', { name: 'Log New Claim' })).toBeVisible();

    await page.goto(`/claims/${claim.id}`);
    await expect(page.getByRole('heading', { name: claim.claimNumber })).toBeVisible();
    for (const tab of ['Overview', 'Parties', 'Reserves', 'Documents', 'Audit Log'] as const) {
      await openTab(page, tab);
    }

    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page).toHaveURL(/\/login$/);

    expect(errors, errors.join('\n')).toEqual([]);
  });
}
