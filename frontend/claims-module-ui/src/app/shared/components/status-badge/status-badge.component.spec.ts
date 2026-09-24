import { Component, Input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { StatusBadgeComponent } from './status-badge.component';
import { StatusBadgeHarness } from './testing/status-badge.harness';
import { claimStatusBadge } from '../../models/status-badge.util';
import { ClaimStatus } from '../../models/enums';

@Component({
  standalone: true,
  imports: [StatusBadgeComponent],
  template: `<app-status-badge [label]="badge.label" [color]="badge.color"></app-status-badge>`
})
class HostComponent {
  @Input() status = ClaimStatus.Draft;
  get badge() {
    return claimStatusBadge(this.status);
  }
}

// F-10: every claim status (all 7, incl. Reopened and Withdrawn) renders with the right colour class.
// Brief: Draft = gray (neutral), Open = blue (info), UnderInvestigation = orange, PendingPayment = purple, Closed = green (success).
describe('StatusBadgeComponent (F-10)', () => {
  const expected: [ClaimStatus, string, string][] = [
    [ClaimStatus.Draft, 'Draft', 'neutral'],
    [ClaimStatus.Open, 'Open', 'info'],
    [ClaimStatus.UnderInvestigation, 'Under Investigation', 'orange'],
    [ClaimStatus.PendingPayment, 'Pending Payment', 'purple'],
    [ClaimStatus.Closed, 'Closed', 'success'],
    [ClaimStatus.Reopened, 'Reopened', 'teal'],
    [ClaimStatus.Withdrawn, 'Withdrawn', 'brown']
  ];

  it('covers all 7 statuses', () => {
    const statuses = Object.values(ClaimStatus).filter((v) => typeof v === 'number');
    expect(statuses.length).toBe(7);
    expect(expected.map(([s]) => s).sort()).toEqual((statuses as ClaimStatus[]).sort());
  });

  for (const [status, label, color] of expected) {
    it(`renders ${label} with status-badge--${color}`, async () => {
      const fixture = TestBed.createComponent(HostComponent);
      fixture.componentRef.setInput('status', status);
      fixture.detectChanges();

      const badge = await TestbedHarnessEnvironment.loader(fixture).getHarness(StatusBadgeHarness);
      expect(await badge.getLabel()).toBe(label);
      expect(await badge.getColor()).toBe(color as ReturnType<typeof claimStatusBadge>['color']);
    });
  }

  it('defaults to neutral when no colour is given', async () => {
    const fixture = TestBed.createComponent(StatusBadgeComponent);
    fixture.componentRef.setInput('label', 'Something');
    fixture.detectChanges();

    const badge = await TestbedHarnessEnvironment.harnessForFixture(fixture, StatusBadgeHarness);
    expect(await badge.getColor()).toBe('neutral');
  });
});
