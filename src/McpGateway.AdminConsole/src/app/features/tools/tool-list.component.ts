import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { ToolsApi } from '../../core/api/tools.api';
import { ToolSummaryResponse, RegisterToolRequest } from '../../core/models/tool';
import { RISK_LEVELS, RiskLevel } from '../../core/models/enums';
import { TokenService } from '../../core/auth/token.service';
import { SnackbarService } from '../../shared/snackbar.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';
import { describeError } from '../../shared/api-error';
import { RiskBadgeComponent } from '../../shared/risk-badge/risk-badge.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { ToolFormDialogComponent, ToolFormData } from './tool-form-dialog.component';

@Component({
  selector: 'app-tool-list',
  imports: [
    FormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    RiskBadgeComponent,
    PageHeaderComponent,
  ],
  templateUrl: './tool-list.component.html',
  styleUrl: './tool-list.component.scss',
})
export class ToolListComponent implements OnInit {
  private readonly api = inject(ToolsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snackbar = inject(SnackbarService);
  private readonly confirm = inject(ConfirmService);
  private readonly tokenService = inject(TokenService);

  protected readonly tools = signal<ToolSummaryResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly riskLevels = RISK_LEVELS;
  protected readonly isAdmin = this.tokenService.isAdmin;
  protected readonly columns = ['name', 'risk', 'version', 'approval', 'enabled'];

  protected riskFilter: RiskLevel | '' = '';
  protected nameContains = '';
  protected includeDisabled = false;

  ngOnInit(): void {
    void this.refresh();
  }

  protected async refresh(): Promise<void> {
    this.loading.set(true);
    try {
      const tools = await firstValueFrom(
        this.api.list({
          riskLevel: this.riskFilter || undefined,
          includeDisabled: this.includeDisabled,
          nameContains: this.nameContains.trim() || undefined,
        }),
      );
      this.tools.set(tools);
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to load tools.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected open(tool: ToolSummaryResponse): void {
    void this.router.navigate(['/tools', tool.name]);
  }

  protected async registerTool(): Promise<void> {
    const request = await firstValueFrom(
      this.dialog
        .open<ToolFormDialogComponent, ToolFormData, RegisterToolRequest>(ToolFormDialogComponent, {
          data: { mode: 'tool' },
        })
        .afterClosed(),
    );
    if (!request) {
      return;
    }
    try {
      await firstValueFrom(this.api.register(request));
      this.snackbar.success(`Registered ${request.name}.`);
      await this.refresh();
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to register the tool.'));
    }
  }

  protected async toggleEnabled(tool: ToolSummaryResponse, enabled: boolean): Promise<void> {
    if (!enabled) {
      const ok = await this.confirm.ask({
        title: `Disable ${tool.name}?`,
        message: 'Disabling acts as a kill switch: the gateway will deny all authorization for this tool.',
        confirmText: 'Disable',
        destructive: true,
      });
      if (!ok) {
        return;
      }
    }
    try {
      await firstValueFrom(this.api.setEnabled(tool.name, enabled));
      this.snackbar.success(`${enabled ? 'Enabled' : 'Disabled'} ${tool.name}.`);
      await this.refresh();
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to change the tool state.'));
    }
  }
}
