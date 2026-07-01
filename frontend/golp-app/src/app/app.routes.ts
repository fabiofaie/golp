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
  {
    path: 'circles/:circleId/match/new',
    loadComponent: () => import('./circles/record-match/record-match.component').then(m => m.RecordMatchComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles/:circleId/leaderboard',
    loadComponent: () => import('./circles/circle-leaderboard/circle-leaderboard.component').then(m => m.CircleLeaderboardComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles/:circleId/matches',
    loadComponent: () => import('./circles/circle-match-history/circle-match-history.component').then(m => m.CircleMatchHistoryComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles/:circleId/matches/:matchId',
    loadComponent: () => import('./circles/match-confirm/match-confirm.component').then(m => m.MatchConfirmComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles/:circleId/matches/:matchId/detail',
    loadComponent: () => import('./circles/match-detail/match-detail.component').then(m => m.MatchDetailComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles/:circleId/awards',
    loadComponent: () => import('./circles/circle-awards/circle-awards.component').then(m => m.CircleAwardsComponent),
    canActivate: [authGuard]
  },
  {
    path: 'circles/:circleId/stats',
    loadComponent: () => import('./circles/circle-stats/circle-stats.component').then(m => m.CircleStatsComponent),
    canActivate: [authGuard]
  },
  {
    path: 'join',
    loadComponent: () => import('./circles/join-circle/join-circle.component').then(m => m.JoinCircleComponent)
  },
  {
    path: 'profilo',
    loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent),
    canActivate: [authGuard]
  },
  {
    path: 'elo-info',
    loadComponent: () => import('./elo-info/elo-info.component').then(m => m.EloInfoComponent)
  },
  {
    path: 'simulate-match',
    loadComponent: () => import('./elo-info/elo-info.component').then(m => m.EloInfoComponent)
  },
  {
    path: 'm/:token',
    loadComponent: () => import('./public/match-public-confirm/match-public-confirm.component').then(m => m.MatchPublicConfirmComponent)
  },
  {
    path: 'match/quick',
    loadComponent: () => import('./circles/quick-match/quick-match.component').then(m => m.QuickMatchComponent),
    canActivate: [authGuard]
  },
  { path: '**', redirectTo: '/dashboard' }
];
