import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { TokenService } from '../../core/auth/token.service';

/** Landing page after sign-in; feature sections are reached from the sidenav. */
@Component({
  selector: 'app-home',
  imports: [RouterLink, MatCardModule, MatIconModule],
  template: `
    <h1>Welcome</h1>
    <p>Signed in as <strong>{{ clientId() }}</strong>. Choose a section from the sidebar to get started.</p>
    <div class="cards">
      <mat-card routerLink="/tools" class="tile">
        <mat-icon>build</mat-icon><span>Tool registry</span>
      </mat-card>
      <mat-card routerLink="/approvals" class="tile">
        <mat-icon>how_to_reg</mat-icon><span>Pending approvals</span>
      </mat-card>
      <mat-card routerLink="/stats" class="tile">
        <mat-icon>insights</mat-icon><span>Usage statistics</span>
      </mat-card>
    </div>
  `,
  styles: `
    .cards { display: flex; flex-wrap: wrap; gap: 1rem; margin-top: 1rem; }
    .tile {
      display: flex; align-items: center; gap: 0.5rem;
      padding: 1rem 1.25rem; cursor: pointer; min-width: 200px;
    }
  `,
})
export class HomeComponent {
  private readonly tokenService = inject(TokenService);
  protected readonly clientId = this.tokenService.clientId;
}
