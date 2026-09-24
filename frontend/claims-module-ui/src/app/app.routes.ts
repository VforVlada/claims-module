import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'claims' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'claims',
    canActivate: [authGuard],
    loadComponent: () => import('./features/claims-list/claims-list.component').then((m) => m.ClaimsListComponent)
  },
  {
    path: 'claims/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/fnol-intake/fnol-intake.component').then((m) => m.FnolIntakeComponent)
  },
  {
    path: 'claims/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/claim-detail/claim-detail.component').then((m) => m.ClaimDetailComponent)
  },
  { path: '**', redirectTo: 'claims' }
];
