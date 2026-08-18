import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatDialog } from '@angular/material/dialog';
import { MatTabsModule } from '@angular/material/tabs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { firstValueFrom } from 'rxjs';
import { ApprovalsApi } from '../../core/api/approvals.api';
import { ApprovalResponse } from '../../core/models/approval';
import { ApprovalStatus, APPROVAL_STATUSES } from '../../core/models/enums';
import { TokenService } from '../../core/auth/token.service';
import { SnackbarService } from '../../shared/snackbar.service';
import { describeError } from '../../shared/api-error';
import { RiskBadgeComponent } from '../../shared/risk-badge/risk-badge.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { EnumLabelPipe } from '../../shared/enum-label.pipe';
import { DecisionDialogComponent, DecisionDialogData } from './decision-dialog.component';

@Component({
  selector: 'app-approval-list',
  imports: [
    DatePipe,
    MatTabsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    RiskBadgeComponent,
    PageHeaderComponent,
    EnumLabelPipe,
  ],
  templateUrl: './approval-list.component.html',
  styleUrl: './approval-list.component.scss',
})
export class ApprovalListComponent implements OnInit {
  private readonly api = inject(ApprovalsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackbar = inject(SnackbarService);
  private readonly tokenService = inject(TokenService);

  protected readonly statuses = APPROVAL_STATUSES;
  protected readonly approvals = signal<ApprovalResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly isAdmin = this.tokenService.isAdmin;

  private activeStatus: ApprovalStatus = 'Pending';

  ngOnInit(): void {
    void this.load();
  }

  protected onTabChange(index: number): void {
    this.activeStatus = this.statuses[index];
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.approvals.set(await firstValueFrom(this.api.list(this.activeStatus)));
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to load approvals.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected async decide(approval: ApprovalResponse, decision: 'approve' | 'reject'): Promise<void> {
    const data: DecisionDialogData = { decision, toolName: approval.toolName, version: approval.version };
    const result = await firstValueFrom(
      this.dialog
        .open<DecisionDialogComponent, DecisionDialogData, { note?: string }>(DecisionDialogComponent, { data })
        .afterClosed(),
    );
    if (!result) {
      return;
    }
    try {
      const action = decision === 'approve' ? this.api.approve(approval.id, result.note) : this.api.reject(approval.id, result.note);
      await firstValueFrom(action);
      this.snackbar.success(`Request ${decision === 'approve' ? 'approved' : 'rejected'}.`);
      await this.load();
    } catch (err) {
      // Surfaces the four-eyes rejection (self-approval) verbatim from the gateway.
      this.snackbar.error(describeError(err, `Failed to ${decision} the request.`));
    }
  }
}
