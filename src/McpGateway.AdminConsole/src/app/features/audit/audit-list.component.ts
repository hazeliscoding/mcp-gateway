import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { AuditApi } from '../../core/api/audit.api';
import { AuditEntryResponse } from '../../core/models/audit';
import { AuditEventType, AUDIT_EVENT_TYPES } from '../../core/models/enums';
import { SnackbarService } from '../../shared/snackbar.service';
import { describeError } from '../../shared/api-error';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { EnumLabelPipe } from '../../shared/enum-label.pipe';

@Component({
  selector: 'app-audit-list',
  imports: [
    DatePipe,
    FormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    PageHeaderComponent,
    EnumLabelPipe,
  ],
  templateUrl: './audit-list.component.html',
  styleUrl: './audit-list.component.scss',
})
export class AuditListComponent implements OnInit {
  private readonly api = inject(AuditApi);
  private readonly snackbar = inject(SnackbarService);

  protected readonly entries = signal<AuditEntryResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly eventTypes = AUDIT_EVENT_TYPES;
  protected readonly columns = ['occurredAt', 'eventType', 'actor', 'result', 'tool', 'trace'];

  protected toolName = '';
  protected actor = '';
  protected eventType: AuditEventType | '' = '';
  protected from: Date | null = null;
  protected to: Date | null = null;
  protected limit = 100;

  ngOnInit(): void {
    void this.search();
  }

  protected async search(): Promise<void> {
    this.loading.set(true);
    try {
      const entries = await firstValueFrom(
        this.api.query({
          toolName: this.toolName.trim() || undefined,
          actor: this.actor.trim() || undefined,
          eventType: this.eventType || undefined,
          from: this.from?.toISOString(),
          to: this.to?.toISOString(),
          limit: this.limit,
        }),
      );
      this.entries.set(entries);
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to load the audit trail.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected reset(): void {
    this.toolName = '';
    this.actor = '';
    this.eventType = '';
    this.from = null;
    this.to = null;
    this.limit = 100;
    void this.search();
  }

  protected async copyTrace(traceId: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(traceId);
      this.snackbar.success('Trace id copied.');
    } catch {
      // Clipboard access can be denied; ignore silently.
    }
  }
}
