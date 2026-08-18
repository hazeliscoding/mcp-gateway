import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { IDENTITY_TYPES, IdentityType } from '../../core/models/enums';
import { RegisterIdentityRequest } from '../../core/models/identity';

/** Collects a new identity registration. */
@Component({
  selector: 'app-register-identity-dialog',
  imports: [
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './register-identity-dialog.component.html',
  styleUrl: './register-identity-dialog.component.scss',
})
export class RegisterIdentityDialogComponent {
  private readonly ref = inject(MatDialogRef<RegisterIdentityDialogComponent, RegisterIdentityRequest>);

  protected readonly identityTypes = IDENTITY_TYPES;

  protected clientId = '';
  protected type: IdentityType = 'Agent';
  protected displayName = '';
  protected scopes = '';

  protected submit(): void {
    this.ref.close({
      clientId: this.clientId.trim(),
      type: this.type,
      displayName: this.displayName.trim(),
      grantedScopes: this.scopes
        .split(/[\s,]+/)
        .map((s) => s.trim())
        .filter((s) => s.length > 0),
    });
  }
}
