import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatDialog } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { IdentitiesApi } from '../../core/api/identities.api';
import { IdentityResponse, IssuedSecretResponse, RegisterIdentityRequest } from '../../core/models/identity';
import { TokenService } from '../../core/auth/token.service';
import { SnackbarService } from '../../shared/snackbar.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';
import { describeError } from '../../shared/api-error';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import {
  SecretRevealDialogComponent,
  SecretRevealData,
} from '../../shared/secret-reveal-dialog/secret-reveal-dialog.component';
import { RegisterIdentityDialogComponent } from './register-identity-dialog.component';

@Component({
  selector: 'app-identity-list',
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    MatProgressBarModule,
    MatTooltipModule,
    DatePipe,
    PageHeaderComponent,
  ],
  templateUrl: './identity-list.component.html',
  styleUrl: './identity-list.component.scss',
})
export class IdentityListComponent implements OnInit {
  private readonly api = inject(IdentitiesApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackbar = inject(SnackbarService);
  private readonly confirm = inject(ConfirmService);
  private readonly tokenService = inject(TokenService);

  protected readonly identities = signal<IdentityResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly isAdmin = this.tokenService.isAdmin;
  protected readonly columns = ['clientId', 'type', 'scopes', 'created', 'enabled', 'actions'];

  ngOnInit(): void {
    void this.refresh();
  }

  protected async refresh(): Promise<void> {
    this.loading.set(true);
    try {
      this.identities.set(await firstValueFrom(this.api.list()));
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to load identities.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected async register(): Promise<void> {
    const request = await firstValueFrom(
      this.dialog
        .open<RegisterIdentityDialogComponent, unknown, RegisterIdentityRequest>(RegisterIdentityDialogComponent)
        .afterClosed(),
    );
    if (!request) {
      return;
    }
    try {
      const issued = await firstValueFrom(this.api.register(request));
      this.reveal(issued);
      this.snackbar.success(`Registered ${request.clientId}.`);
      await this.refresh();
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to register the identity.'));
    }
  }

  protected async toggleEnabled(identity: IdentityResponse, enabled: boolean): Promise<void> {
    if (!enabled) {
      const ok = await this.confirm.ask({
        title: `Disable ${identity.clientId}?`,
        message: 'A disabled identity can no longer obtain tokens. This is a kill switch for the client.',
        confirmText: 'Disable',
        destructive: true,
      });
      if (!ok) {
        return;
      }
    }
    try {
      await firstValueFrom(this.api.setEnabled(identity.clientId, enabled));
      this.snackbar.success(`${enabled ? 'Enabled' : 'Disabled'} ${identity.clientId}.`);
      await this.refresh();
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to change the identity state.'));
    }
  }

  protected async rotate(identity: IdentityResponse): Promise<void> {
    const ok = await this.confirm.ask({
      title: `Rotate secret for ${identity.clientId}?`,
      message: 'The current secret stops working immediately. The new secret is shown only once.',
      confirmText: 'Rotate',
      destructive: true,
    });
    if (!ok) {
      return;
    }
    try {
      const issued = await firstValueFrom(this.api.rotateSecret(identity.clientId));
      this.reveal(issued);
      this.snackbar.success(`Rotated secret for ${identity.clientId}.`);
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to rotate the secret.'));
    }
  }

  private reveal(issued: IssuedSecretResponse): void {
    const data: SecretRevealData = {
      clientId: issued.identity.clientId,
      clientSecret: issued.clientSecret,
    };
    this.dialog.open(SecretRevealDialogComponent, { data, width: '480px', disableClose: true });
  }
}
