import { Pipe, PipeTransform } from '@angular/core';

/** Splits a PascalCase enum value into spaced words: "RequiresApproval" → "Requires approval". */
@Pipe({ name: 'enumLabel' })
export class EnumLabelPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    const spaced = value.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
    return spaced.charAt(0).toUpperCase() + spaced.slice(1);
  }
}
