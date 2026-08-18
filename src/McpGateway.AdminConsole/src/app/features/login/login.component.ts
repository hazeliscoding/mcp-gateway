import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TokenService } from '../../core/auth/token.service';
import { describeError } from '../../shared/api-error';

@Component({
  selector: 'app-login',
  imports: [
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly tokenService = inject(TokenService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected clientId = '';
  protected clientSecret = '';
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async submit(): Promise<void> {
    if (this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.tokenService.login(this.clientId.trim(), this.clientSecret);
      // The secret is intentionally dropped as soon as the exchange succeeds.
      this.clientSecret = '';
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/tools';
      await this.router.navigateByUrl(returnUrl);
    } catch (err) {
      this.error.set(describeError(err, 'Sign-in failed. Check the client id and secret.'));
    } finally {
      this.submitting.set(false);
    }
  }
}
