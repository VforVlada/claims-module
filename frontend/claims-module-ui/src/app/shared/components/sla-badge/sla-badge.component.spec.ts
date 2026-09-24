import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { SlaBadgeComponent } from './sla-badge.component';
import { SlaBadgeHarness } from './testing/sla-badge.harness';

describe('SlaBadgeComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideNoopAnimations()] }));

  async function render(breachedAt: string | null): Promise<SlaBadgeHarness> {
    const fixture = TestBed.createComponent(SlaBadgeComponent);
    fixture.componentRef.setInput('breachedAt', breachedAt);
    fixture.detectChanges();
    return TestbedHarnessEnvironment.harnessForFixture(fixture, SlaBadgeHarness);
  }

  it('reads "SLA breached" and shows when the breach was recorded in its tooltip', async () => {
    const badge = await render('2026-09-20T08:30:00Z');
    expect(await badge.getText()).toContain('SLA breached');
    const tooltip = await badge.getTooltipText();
    expect(tooltip).toContain('SLA breached since');
    expect(tooltip).toContain('Sep 20, 2026');
    expect(await badge.getAriaLabel()).toBe(tooltip);
  });

  it('falls back to a generic tooltip without a timestamp', async () => {
    const badge = await render(null);
    expect(await badge.getTooltipText()).toBe('SLA breached — no activity for 48+ hours');
  });
});
