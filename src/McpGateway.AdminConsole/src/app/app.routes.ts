import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    loadComponent: () => import('./features/shell/shell.component').then((m) => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'home' },
      {
        path: 'home',
        loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent),
      },
      {
        path: 'tools',
        loadComponent: () => import('./features/tools/tool-list.component').then((m) => m.ToolListComponent),
      },
      {
        path: 'tools/:name',
        loadComponent: () => import('./features/tools/tool-detail.component').then((m) => m.ToolDetailComponent),
      },
      {
        path: 'identities',
        loadComponent: () =>
          import('./features/identities/identity-list.component').then((m) => m.IdentityListComponent),
      },
      {
        path: 'permissions',
        loadComponent: () =>
          import('./features/permissions/permissions.component').then((m) => m.PermissionsComponent),
      },
      {
        path: 'approvals',
        loadComponent: () =>
          import('./features/approvals/approval-list.component').then((m) => m.ApprovalListComponent),
      },
      {
        path: 'audit',
        loadComponent: () => import('./features/audit/audit-list.component').then((m) => m.AuditListComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
