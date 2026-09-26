import { Component, Input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

/**
 * "SLA breached" chip shown next to a claim's status badge when the SLA job has flagged the claim
 * (Draft/Open and untouched for 48h+). Warn/red tone; the tooltip gives when the job detected the breach
 * (up to 15 minutes after the 48h mark, or later if the API was down), not when inactivity began.
 */
@Component({
  selector: 'app-sla-badge',
  standalone: true,
  imports: [MatIconModule, MatTooltipModule],
  providers: [DatePipe],
  template: `<span
    class="sla-badge"
    role="status"
    [matTooltip]="tooltip"
    [attr.aria-label]="tooltip"
    ><mat-icon aria-hidden="true">schedule</mat-icon>SLA breached</span
  >`,
  styles: [
    `
      .sla-badge {
        display: inline-flex;
        align-items: center;
        gap: 4px;
        padding: 2px 10px;
        border-radius: 12px;
        background: #ffebee;
        color: #c62828;
        border: 1px solid #ef9a9a;
        font-size: 12px;
        font-weight: 600;
        letter-spacing: 0.02em;
        white-space: nowrap;
        cursor: default;
      }
      .sla-badge mat-icon {
        font-size: 14px;
        width: 14px;
        height: 14px;
      }
    `
  ]
})
export class SlaBadgeComponent {
  /** ISO timestamp the SLA job set; null/undefined when the API did not provide one. */
  @Input() breachedAt: string | null | undefined = null;

  constructor(private readonly datePipe: DatePipe) {}

  get tooltip(): string {
    const when = this.breachedAt ? this.datePipe.transform(this.breachedAt, 'medium') : null;
    return when ? `SLA breached — no activity for 48+ hours (detected ${when})` : 'SLA breached — no activity for 48+ hours';
  }
}
