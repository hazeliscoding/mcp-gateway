import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { firstValueFrom } from 'rxjs';
import { AuditApi } from '../../core/api/audit.api';
import { AuditStatsResponse } from '../../core/models/audit';
import { SnackbarService } from '../../shared/snackbar.service';
import { describeError } from '../../shared/api-error';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';

const OUTCOME_COLORS: Record<string, string> = {
  Permitted: '#2e7d32',
  RequiresApproval: '#ef6c00',
  Denied: '#c62828',
  Prohibited: '#6a1b9a',
};

@Component({
  selector: 'app-stats',
  imports: [
    DecimalPipe,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressBarModule,
    BaseChartDirective,
    PageHeaderComponent,
  ],
  templateUrl: './stats.component.html',
  styleUrl: './stats.component.scss',
})
export class StatsComponent implements OnInit {
  private readonly api = inject(AuditApi);
  private readonly snackbar = inject(SnackbarService);

  protected readonly stats = signal<AuditStatsResponse | null>(null);
  protected readonly loading = signal(false);

  protected from: Date | null = null;
  protected to: Date | null = null;

  protected readonly perDayData = computed<ChartData<'line'>>(() => {
    const stats = this.stats();
    return {
      labels: stats?.eventsPerDay.map((d) => d.date) ?? [],
      datasets: [
        {
          label: 'Events',
          data: stats?.eventsPerDay.map((d) => d.count) ?? [],
          borderColor: '#1565c0',
          backgroundColor: 'rgba(21, 101, 192, 0.15)',
          fill: true,
          tension: 0.3,
        },
      ],
    };
  });

  protected readonly outcomeData = computed<ChartData<'doughnut'>>(() => {
    const outcomes = this.stats()?.authorizationOutcomes ?? [];
    return {
      labels: outcomes.map((o) => o.name),
      datasets: [
        {
          data: outcomes.map((o) => o.count),
          backgroundColor: outcomes.map((o) => OUTCOME_COLORS[o.name] ?? '#607d8b'),
        },
      ],
    };
  });

  protected readonly toolData = computed<ChartData<'bar'>>(() => {
    const tools = this.stats()?.eventsByTool ?? [];
    return {
      labels: tools.map((t) => t.name),
      datasets: [{ label: 'Events', data: tools.map((t) => t.count), backgroundColor: '#1565c0' }],
    };
  });

  protected readonly lineOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
  };
  protected readonly barOptions: ChartConfiguration<'bar'>['options'] = {
    indexAxis: 'y',
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { x: { beginAtZero: true, ticks: { precision: 0 } } },
  };
  protected readonly doughnutOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
  };

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.stats.set(await firstValueFrom(this.api.stats(this.from?.toISOString(), this.to?.toISOString())));
    } catch (err) {
      this.snackbar.error(describeError(err, 'Failed to load statistics.'));
    } finally {
      this.loading.set(false);
    }
  }
}
