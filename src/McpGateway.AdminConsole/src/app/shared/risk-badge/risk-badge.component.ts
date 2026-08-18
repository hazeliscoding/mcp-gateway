import { Component, input } from '@angular/core';
import { RiskLevel } from '../../core/models/enums';

/** Colored chip conveying a tool's risk tier at a glance. */
@Component({
  selector: 'app-risk-badge',
  template: `<span class="risk-badge" [attr.data-risk]="risk()">{{ label() }}</span>`,
  styleUrl: './risk-badge.component.scss',
})
export class RiskBadgeComponent {
  readonly risk = input.required<RiskLevel>();

  protected label(): string {
    return this.risk() === 'ReadOnly' ? 'Read only' : this.risk();
  }
}
