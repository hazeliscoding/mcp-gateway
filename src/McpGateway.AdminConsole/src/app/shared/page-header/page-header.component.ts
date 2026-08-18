import { Component, input } from '@angular/core';

/** Standard page heading with an optional description and a slot for actions. */
@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header">
      <div>
        <h1>{{ title() }}</h1>
        @if (description()) {
          <p>{{ description() }}</p>
        }
      </div>
      <div class="actions">
        <ng-content />
      </div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: 1.25rem;
    }
    h1 { margin: 0 0 0.25rem; font-size: 1.5rem; }
    p { margin: 0; opacity: 0.75; }
    .actions { display: flex; gap: 0.5rem; flex-wrap: wrap; }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly description = input<string>();
}
