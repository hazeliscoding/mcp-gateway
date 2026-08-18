import { Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface SecretRevealData {
  clientId: string;
  clientSecret: string;
}

/** One-time display of a freshly issued client secret with a copy button. */
@Component({
  selector: 'app-secret-reveal-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './secret-reveal-dialog.component.html',
  styleUrl: './secret-reveal-dialog.component.scss',
})
export class SecretRevealDialogComponent {
  protected readonly data = inject<SecretRevealData>(MAT_DIALOG_DATA);
  protected readonly copied = signal(false);

  protected async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.data.clientSecret);
      this.copied.set(true);
    } catch {
      this.copied.set(false);
    }
  }
}
