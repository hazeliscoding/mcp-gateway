import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';

export interface DecisionDialogData {
  decision: 'approve' | 'reject';
  toolName: string;
  version: string;
}

/** Captures an optional note for approving or rejecting a request. Returns the note (possibly empty). */
@Component({
  selector: 'app-decision-dialog',
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>
      {{ data.decision === 'approve' ? 'Approve' : 'Reject' }} {{ data.toolName }} {{ data.version }}
    </h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full">
        <mat-label>Note (optional)</mat-label>
        <textarea matInput name="note" [(ngModel)]="note" rows="3"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="undefined">Cancel</button>
      <button
        mat-flat-button
        [color]="data.decision === 'approve' ? 'primary' : 'warn'"
        (click)="confirm()"
      >
        {{ data.decision === 'approve' ? 'Approve' : 'Reject' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `.full { width: 100%; min-width: 360px; }`,
})
export class DecisionDialogComponent {
  protected readonly data = inject<DecisionDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<DecisionDialogComponent, { note?: string }>);

  protected note = '';

  protected confirm(): void {
    this.ref.close({ note: this.note.trim() || undefined });
  }
}
