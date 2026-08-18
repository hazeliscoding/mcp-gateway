import { Component, computed, effect, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TokenService } from '../../core/auth/token.service';
import { SnackbarService } from '../../shared/snackbar.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

/** Authenticated layout: sidenav navigation, a toolbar with session status, and sign-out. */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly tokenService = inject(TokenService);
  private readonly router = inject(Router);
  private readonly snackbar = inject(SnackbarService);

  private warned = false;

  protected readonly navItems: NavItem[] = [
    { path: '/tools', label: 'Tools', icon: 'build' },
    { path: '/identities', label: 'Identities', icon: 'badge' },
    { path: '/permissions', label: 'Permissions', icon: 'key' },
    { path: '/approvals', label: 'Approvals', icon: 'how_to_reg' },
    { path: '/audit', label: 'Audit', icon: 'history' },
    { path: '/stats', label: 'Statistics', icon: 'insights' },
  ];

  protected readonly clientId = this.tokenService.clientId;
  protected readonly isAdmin = this.tokenService.isAdmin;
  protected readonly secondsRemaining = this.tokenService.secondsRemaining;
  protected readonly sessionLabel = computed(() => {
    const seconds = this.secondsRemaining();
    const minutes = Math.floor(seconds / 60);
    const rest = seconds % 60;
    return `${minutes}:${rest.toString().padStart(2, '0')}`;
  });
  protected readonly sessionExpiring = computed(() => this.secondsRemaining() > 0 && this.secondsRemaining() < 60);

  constructor() {
    // Warn once when the session is nearly up; re-arm after a fresh login.
    effect(() => {
      const seconds = this.secondsRemaining();
      if (seconds > 0 && seconds < 60 && !this.warned) {
        this.warned = true;
        this.snackbar.error('Session expires soon — sign in again to stay active.');
      } else if (seconds > 120) {
        this.warned = false;
      }
    });
  }

  protected signOut(): void {
    this.tokenService.clear();
    void this.router.navigate(['/login']);
  }
}
