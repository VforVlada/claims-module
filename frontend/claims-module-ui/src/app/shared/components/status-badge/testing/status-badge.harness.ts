import { BaseHarnessFilters, ComponentHarness, HarnessPredicate } from '@angular/cdk/testing';
import { BadgeColor } from '../status-badge.component';

export interface StatusBadgeHarnessFilters extends BaseHarnessFilters {
  label?: string | RegExp;
}

/** Component harness for <app-status-badge>, so specs never reach for its internal CSS classes directly. */
export class StatusBadgeHarness extends ComponentHarness {
  static hostSelector = 'app-status-badge';

  private readonly badge = this.locatorFor('.status-badge');

  static with(options: StatusBadgeHarnessFilters = {}): HarnessPredicate<StatusBadgeHarness> {
    return new HarnessPredicate(StatusBadgeHarness, options).addOption('label', options.label, (harness, label) =>
      HarnessPredicate.stringMatches(harness.getLabel(), label)
    );
  }

  async getLabel(): Promise<string> {
    return (await this.badge()).text();
  }

  async getColor(): Promise<BadgeColor | null> {
    const classes = ((await (await this.badge()).getAttribute('class')) ?? '').split(/\s+/);
    const colorClass = classes.find((c) => c.startsWith('status-badge--'));
    return colorClass ? (colorClass.replace('status-badge--', '') as BadgeColor) : null;
  }
}
