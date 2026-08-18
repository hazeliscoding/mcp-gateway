import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { ToolsApi } from '../../core/api/tools.api';
import { ToolDetailResponse, ToolVersionResponse, RegisterToolRequest } from '../../core/models/tool';
import { TokenService } from '../../core/auth/token.service';
import { SnackbarService } from '../../shared/snackbar.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';
import { describeError } from '../../shared/api-error';
import { RiskBadgeComponent } from '../../shared/risk-badge/risk-badge.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { EnumLabelPipe } from '../../shared/enum-label.pipe';
import { ToolFormDialogComponent, ToolFormData } from './tool-form-dialog.component';

@Component({
  selector: 'app-tool-detail',
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressBarModule,
    MatTooltipModule,
    RiskBadgeComponent,
    PageHeaderComponent,
    EnumLabelPipe,
  ],
  templateUrl: './tool-detail.component.html',
  styleUrl: './tool-detail.component.scss',
})
export class ToolDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ToolsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackbar = inject(SnackbarService);
  private readonly confirm = inject(ConfirmService);
  private readonly tokenService = inject(TokenService);

  protected readonly tool = signal<ToolDetailResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly isAdmin = this.tokenService.isAdmin;

  private name = '';

  ngOnInit(): void {
    this.name = this.route.snapshot.paramMap.get('name') ?? '';
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.tool.set(await firstValueFrom(this.api.get(this.name)));
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to load the tool.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected schemaText(schema: unknown): string {
    return JSON.stringify(schema, null, 2);
  }

  protected async addVersion(): Promise<void> {
    const request = await firstValueFrom(
      this.dialog
        .open<ToolFormDialogComponent, ToolFormData, RegisterToolRequest>(ToolFormDialogComponent, {
          data: { mode: 'version', toolName: this.name },
        })
        .afterClosed(),
    );
    if (!request) {
      return;
    }
    const { name: _drop, ...version } = request;
    try {
      await firstValueFrom(this.api.addVersion(this.name, version));
      this.snackbar.success(`Added version ${version.version}.`);
      await this.load();
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to add the version.'));
    }
  }

  protected async deprecate(version: ToolVersionResponse): Promise<void> {
    const ok = await this.confirm.ask({
      title: `Deprecate version ${version.version}?`,
      message: 'Deprecated versions can no longer be authorized. This cannot be undone.',
      confirmText: 'Deprecate',
      destructive: true,
    });
    if (!ok) {
      return;
    }
    try {
      await firstValueFrom(this.api.deprecateVersion(this.name, version.version));
      this.snackbar.success(`Deprecated version ${version.version}.`);
      await this.load();
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to deprecate the version.'));
    }
  }
}
