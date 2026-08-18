import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { RISK_LEVELS, RiskLevel } from '../../core/models/enums';
import { RegisterToolRequest } from '../../core/models/tool';

export interface ToolFormData {
  mode: 'tool' | 'version';
  toolName?: string;
}

/**
 * Collects a tool registration or a new version. In version mode the name is fixed
 * and hidden; both modes return a full RegisterToolRequest (the caller drops the
 * name for the version endpoint).
 */
@Component({
  selector: 'app-tool-form-dialog',
  imports: [
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
  ],
  templateUrl: './tool-form-dialog.component.html',
  styleUrl: './tool-form-dialog.component.scss',
})
export class ToolFormDialogComponent {
  protected readonly data = inject<ToolFormData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ToolFormDialogComponent, RegisterToolRequest>);

  protected readonly riskLevels = RISK_LEVELS;
  protected readonly schemaError = signal<string | null>(null);

  protected name = this.data.toolName ?? '';
  protected version = '1.0';
  protected description = '';
  protected riskLevel: RiskLevel = 'ReadOnly';
  protected approvalRequired = false;
  protected scopes = '';
  protected timeoutSeconds = 30;
  protected inputSchema = '{\n  "type": "object"\n}';
  protected outputSchema = '{\n  "type": "object"\n}';

  protected get title(): string {
    return this.data.mode === 'tool' ? 'Register tool' : `Add version to ${this.data.toolName}`;
  }

  protected submit(): void {
    const input = this.parse(this.inputSchema, 'input');
    if (input === undefined) {
      return;
    }
    const output = this.parse(this.outputSchema, 'output');
    if (output === undefined) {
      return;
    }

    const request: RegisterToolRequest = {
      name: this.name.trim(),
      version: this.version.trim(),
      description: this.description.trim(),
      riskLevel: this.riskLevel,
      approvalRequired: this.approvalRequired,
      requiredScopes: this.scopes
        .split(/[\s,]+/)
        .map((s) => s.trim())
        .filter((s) => s.length > 0),
      timeoutSeconds: this.timeoutSeconds,
      inputSchema: input,
      outputSchema: output,
    };
    this.ref.close(request);
  }

  private parse(value: string, which: string): unknown {
    try {
      return JSON.parse(value);
    } catch {
      this.schemaError.set(`The ${which} schema is not valid JSON.`);
      return undefined;
    }
  }
}
