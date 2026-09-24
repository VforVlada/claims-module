import { expect, test } from '@playwright/test';
import { confirmDialog, expectAuditEntryEventually, historyRows, openClaim, openTab, pageAs, reserveCard } from '../support/ui';

test.describe('reserve approval authority', () => {
  // E2E-02: 50,000 is Supervisor tier — handler submits, supervisor approves, GL posts.
  test('E2E-02 handler adds a 50,000 reserve; supervisor approves; GL entry appears', async ({ browser }) => {
    const handler = await pageAs(browser, 'handler');
    const claim = await handler.api.createClaim();

    await openClaim(handler.page, claim.id);
    await openTab(handler.page, 'Reserves');
    await handler.page.getByRole('button', { name: /Open New Reserve/ }).click();
    await handler.page.getByLabel('Amount').fill('50000');
    await expect(handler.page.getByText('Requires Supervisor approval')).toBeVisible();
    await handler.page.getByRole('button', { name: 'Submit', exact: true }).click();

    const handlerRows = historyRows(reserveCard(handler.page, 'Indemnity Reserve'));
    await expect(handlerRows).toHaveCount(1);
    await expect(handlerRows.first()).toContainText('Pending Approval');
    await expect(handler.page.getByRole('button', { name: 'Approve' }), 'handlers never see Approve').toHaveCount(0);

    const supervisor = await pageAs(browser, 'supervisor');
    await openClaim(supervisor.page, claim.id);
    await openTab(supervisor.page, 'Reserves');
    const card = reserveCard(supervisor.page, 'Indemnity Reserve');
    await card.getByRole('button', { name: 'Approve' }).click();
    await confirmDialog(supervisor.page, 'Approve');

    await expect(historyRows(card).first()).toContainText('Approved');
    await expect(historyRows(card).first()).toContainText(supervisor.api.session.userName);

    await expectAuditEntryEventually(supervisor.page, 'GL_POSTING_SIMULATED');
    expect(handler.consoleErrors.concat(supervisor.consoleErrors)).toEqual([]);
    await handler.context.close();
    await supervisor.context.close();
  });

  // E2E-03: 150,000 is Manager tier — the supervisor must not be offered Approve; the manager approves.
  test('E2E-03 supervisor sees no Approve on a 150,000 reserve; manager approves it', async ({ browser }) => {
    const handler = await pageAs(browser, 'handler');
    const claim = await handler.api.createClaim();
    await handler.api.openReserve(claim.id, 150_000);
    await handler.context.close();

    const supervisor = await pageAs(browser, 'supervisor');
    await openClaim(supervisor.page, claim.id);
    await openTab(supervisor.page, 'Reserves');
    const supervisorCard = reserveCard(supervisor.page, 'Indemnity Reserve');
    await expect(historyRows(supervisorCard).first()).toContainText('Pending Approval');
    await expect(supervisorCard.getByRole('button', { name: 'Approve' })).toHaveCount(0);
    await expect(supervisorCard.getByRole('button', { name: 'Reject' })).toHaveCount(0);
    await supervisor.context.close();

    const manager = await pageAs(browser, 'manager');
    await openClaim(manager.page, claim.id);
    await openTab(manager.page, 'Reserves');
    const managerCard = reserveCard(manager.page, 'Indemnity Reserve');
    await managerCard.getByRole('button', { name: 'Approve' }).click();
    await confirmDialog(manager.page, 'Approve');
    await expect(historyRows(managerCard).first()).toContainText('Approved');
    await expect(historyRows(managerCard).first()).toContainText(manager.api.session.userName);

    await manager.api.waitForAuditAction(claim.id, 'GL_POSTING_SIMULATED');
    expect(supervisor.consoleErrors.concat(manager.consoleErrors)).toEqual([]);
    await manager.context.close();
  });

  // E2E-04: reject with a reason, handler resubmits — both history records remain.
  test('E2E-04 supervisor rejects with a reason; handler resubmits; both records are in history', async ({ browser }) => {
    const handler = await pageAs(browser, 'handler');
    const claim = await handler.api.createClaim();
    await handler.api.openReserve(claim.id, 50_000);

    const supervisor = await pageAs(browser, 'supervisor');
    await openClaim(supervisor.page, claim.id);
    await openTab(supervisor.page, 'Reserves');
    const supervisorCard = reserveCard(supervisor.page, 'Indemnity Reserve');
    await supervisorCard.getByRole('button', { name: 'Reject' }).click();

    const dialog = supervisor.page.getByRole('dialog');
    await expect(dialog.getByRole('button', { name: 'Reject', exact: true }), 'a reason is mandatory').toBeDisabled();
    await confirmDialog(supervisor.page, 'Reject', 'E2E-04: estimate does not support this amount');
    await expect(historyRows(supervisorCard).first()).toContainText('Rejected');

    // Handler resubmits a lower amount against the same component. Adjust takes the NEW TOTAL:
    // the rejected change never applied, so the component is still at $0 and 40,000 is the new total.
    await openClaim(handler.page, claim.id);
    await openTab(handler.page, 'Reserves');
    const handlerCard = reserveCard(handler.page, 'Indemnity Reserve');
    await handlerCard.getByRole('button', { name: /Adjust/ }).click();
    const newAmount = handler.page.getByLabel('New reserve amount');
    await expect(newAmount, 'prefilled with the current amount').toHaveValue('0');
    const submit = handler.page.getByRole('button', { name: 'Submit', exact: true });
    await newAmount.fill('40000');
    await expect(handler.page.getByText('Requires Supervisor approval')).toBeVisible();
    await expect(submit, 'a reason for the change is mandatory').toBeDisabled();
    await handler.page.getByLabel('Reason for change').fill('E2E-04: resubmitted at the supported estimate');
    await submit.click();

    const rows = historyRows(reserveCard(handler.page, 'Indemnity Reserve'));
    await expect(rows).toHaveCount(2);
    const rejected = rows.filter({ hasText: 'Rejected' });
    await expect(rejected).toContainText('$0.00 → $50,000.00');
    await expect(rejected).toContainText('+$50,000.00');
    await expect(rejected).toContainText('Initial reserve');
    const resubmitted = rows.filter({ hasText: 'Pending Approval' });
    await expect(resubmitted).toContainText('$0.00 → $40,000.00');
    await expect(resubmitted).toContainText('+$40,000.00');
    await expect(resubmitted).toContainText('E2E-04: resubmitted at the supported estimate');

    const history = (await handler.api.getClaim(claim.id)).reserveComponents[0].history;
    const latest = history.find((h) => h.changeSequence === 2)!;
    expect(latest).toMatchObject({ previousAmount: 0, newAmount: 40_000, amount: 40_000, changeReason: 'E2E-04: resubmitted at the supported estimate' });

    const audit = (await handler.api.auditLog(claim.id)).map((e) => e.action);
    expect(audit).toContain('RESERVE_REJECTED');
    // Both the original submission and the resubmission go through ReserveSubmittedEvent (pending -> RESERVE_CREATED).
    expect(audit.filter((a) => a === 'RESERVE_CREATED').length).toBe(2);

    expect(handler.consoleErrors.concat(supervisor.consoleErrors)).toEqual([]);
    await handler.context.close();
    await supervisor.context.close();
  });
});
