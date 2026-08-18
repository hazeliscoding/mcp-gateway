import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { IdentitiesApi } from '../../core/api/identities.api';
import { IdentityResponse } from '../../core/models/identity';
import { GATEWAY_ADMIN_SCOPE } from '../../core/models/enums';
import { SnackbarService } from '../../shared/snackbar.service';
import { describeError } from '../../shared/api-error';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';

interface ScopeGrant {
  scope: string;
  admin: boolean;
  holders: IdentityResponse[];
}

/** Read-only view pivoting identity scope grants into a scope → identities matrix. */
@Component({
  selector: 'app-permissions',
  imports: [
    MatExpansionModule,
    MatChipsModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    PageHeaderComponent,
  ],
  templateUrl: './permissions.component.html',
  styleUrl: './permissions.component.scss',
})
export class PermissionsComponent implements OnInit {
  private readonly api = inject(IdentitiesApi);
  private readonly snackbar = inject(SnackbarService);

  protected readonly loading = signal(false);
  private readonly identities = signal<IdentityResponse[]>([]);

  protected readonly grants = computed<ScopeGrant[]>(() => {
    const byScope = new Map<string, IdentityResponse[]>();
    for (const identity of this.identities()) {
      for (const scope of identity.grantedScopes) {
        const holders = byScope.get(scope) ?? [];
        holders.push(identity);
        byScope.set(scope, holders);
      }
    }
    return [...byScope.entries()]
      .map(([scope, holders]) => ({ scope, admin: scope === GATEWAY_ADMIN_SCOPE, holders }))
      .sort((a, b) => a.scope.localeCompare(b.scope));
  });

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.identities.set(await firstValueFrom(this.api.list()));
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to load permissions.'));
    } finally {
      this.loading.set(false);
    }
  }
}
