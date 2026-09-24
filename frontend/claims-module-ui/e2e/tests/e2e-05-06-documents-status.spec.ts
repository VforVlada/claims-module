import { expect, test } from '@playwright/test';
import { ClaimStatusLabel } from '../support/api';
import { confirmDialog, openClaim, openTab, pageAs } from '../support/ui';

/** Smallest well-formed PDF, so the upload passes the API's content-type allow-list. */
const PDF_BYTES = Buffer.from(
  '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 0/Kids[]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n',
  'latin1'
);

// E2E-05: upload a document and download it back through the row's link.
test('E2E-05 upload a file and download it via its link', async ({ browser }) => {
  const handler = await pageAs(browser, 'handler');
  const claim = await handler.api.createClaim();
  const fileName = `e2e-05-${Date.now()}.pdf`;

  await openClaim(handler.page, claim.id);
  await openTab(handler.page, 'Documents');
  await expect(handler.page.getByText('No documents uploaded yet.')).toBeVisible();

  // Pick a document type (defaults to Other); it is sent as the multipart "documentType" field.
  const typeSelect = handler.page.getByRole('combobox', { name: 'Document type' });
  await expect(typeSelect).toContainText('Other');
  await typeSelect.click();
  await handler.page.getByRole('option', { name: 'Police Report', exact: true }).click();
  await expect(typeSelect).toContainText('Police Report');

  // The visible "Upload Document" button proxies a hidden <input type=file>.
  await handler.page.locator('input[type="file"]').setInputFiles({ name: fileName, mimeType: 'application/pdf', buffer: PDF_BYTES });

  const row = handler.page.getByRole('row').filter({ hasText: fileName });
  await expect(row).toBeVisible();
  await expect(row).toContainText(handler.api.session.userName);
  await expect(row).toContainText('Police Report');

  const link = row.getByRole('link', { name: `Download ${fileName}` });
  const href = await link.getAttribute('href');
  expect(href).toBeTruthy();

  // Clicking opens the signed URL in a new tab…
  const [popup] = await Promise.all([handler.page.waitForEvent('popup'), link.click()]);
  await popup.close();
  // …and the signed URL serves the uploaded bytes without an Authorization header.
  expect(Buffer.compare(await handler.api.fetchAnonymous(href!), PDF_BYTES)).toBe(0);

  const audit = (await handler.api.auditLog(claim.id)).map((e) => e.action);
  expect(audit).toContain('DOCUMENT_UPLOADED');
  expect(handler.consoleErrors).toEqual([]);
  await handler.context.close();
});

// E2E-06: the status menu offers exactly the allowed next statuses, the chip updates and an audit entry is added.
test('E2E-06 status transition offers only valid next statuses; chip updates; audit entry added', async ({ browser }) => {
  const handler = await pageAs(browser, 'handler');
  const claim = await handler.api.createClaim();
  const statuses = await handler.api.claimStatuses();
  const allowed = statuses.find((s) => s.status === claim.status)?.allowedNextStatuses ?? [];
  test.skip(allowed.length === 0, `no transitions configured from ${ClaimStatusLabel[claim.status]} for a handler`);

  await openClaim(handler.page, claim.id);
  await expect(handler.page.locator('app-status-badge').first()).toHaveText(ClaimStatusLabel[claim.status]);

  await handler.page.getByRole('button', { name: /Transition Status/ }).click();
  const offered = (await handler.page.getByRole('menuitem').allInnerTexts()).map((t) => t.trim());
  expect(offered.sort()).toEqual(allowed.map((s) => ClaimStatusLabel[s]).sort());

  const target = ClaimStatusLabel[allowed[0]];
  await handler.page.getByRole('menuitem', { name: target, exact: true }).click();
  await expect(handler.page.getByRole('dialog')).toContainText(`from ${ClaimStatusLabel[claim.status]} to ${target}`);
  await confirmDialog(handler.page, 'Transition');

  await expect(handler.page.locator('app-status-badge').first()).toHaveText(target);
  await openTab(handler.page, 'Audit Log');
  await expect(handler.page.getByRole('cell', { name: 'STATUS_CHANGED', exact: true }).first()).toBeVisible();

  expect((await handler.api.getClaim(claim.id)).status).toBe(allowed[0]);
  expect(handler.consoleErrors).toEqual([]);
  await handler.context.close();
});
