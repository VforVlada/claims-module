import { Component, Input } from '@angular/core';
import { NgClass } from '@angular/common';

export type BadgeColor = 'neutral' | 'info' | 'warning' | 'orange' | 'purple' | 'teal' | 'brown' | 'success' | 'danger';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [NgClass],
  template: `<span class="status-badge" [ngClass]="'status-badge--' + color">{{ label }}</span>`,
  styleUrl: './status-badge.component.scss'
})
export class StatusBadgeComponent {
  @Input({ required: true }) label = '';
  @Input() color: BadgeColor = 'neutral';
}
