import { ComponentHarness } from '@angular/cdk/testing';
import { MatTooltipHarness } from '@angular/material/tooltip/testing';

/** Component harness for <app-sla-badge>. */
export class SlaBadgeHarness extends ComponentHarness {
  static hostSelector = 'app-sla-badge';

  private readonly badge = this.locatorFor('.sla-badge');
  private readonly tooltipHarness = this.locatorFor(MatTooltipHarness);

  async getText(): Promise<string> {
    return (await this.badge()).text();
  }

  /** Hovers the chip and returns its tooltip text. */
  async getTooltipText(): Promise<string> {
    const tooltip = await this.tooltipHarness();
    await tooltip.show();
    const text = await tooltip.getTooltipText();
    await tooltip.hide();
    return text;
  }

  async getAriaLabel(): Promise<string | null> {
    return (await this.badge()).getAttribute('aria-label');
  }
}
