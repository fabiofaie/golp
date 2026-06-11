import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./auth/register/register.component').then(m => m.RegisterComponent)
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./auth/reset-password/reset-password.component').then(m => m.ResetPasswordComponent)
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles',
    loadComponent: () => import('./circles/my-circles/my-circles.component').then(m => m.MyCirclesComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles/new',
    loadComponent: () => import('./circles/create-circle/create-circle.component').then(m => m.CreateCircleComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles/browse',
    loadComponent: () => import('./circles/browse-circles/browse-circles.component').then(m => m.BrowseCirclesComponent),
    canActivate: [authGuard]
  },
  { path: '**', redirectTo: '/dashboard' }
];
