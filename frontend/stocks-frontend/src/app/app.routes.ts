import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    title: 'Stocks - Dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
  },
  {
    path: 'search',
    title: 'Stocks - Search',
    loadComponent: () =>
      import('./features/search/search.component').then(m => m.SearchComponent)
  },
  {
    path: 'scores',
    title: 'Stocks - Scores',
    loadComponent: () =>
      import('./features/scores-report/scores-report.component').then(m => m.ScoresReportComponent)
  },
  {
    path: 'company/:cik',
    loadComponent: () =>
      import('./features/company/company.component').then(m => m.CompanyComponent)
  },
  {
    path: 'company/:cik/scoring',
    loadComponent: () =>
      import('./features/scoring/scoring.component').then(m => m.ScoringComponent)
  },
  {
    path: 'company/:cik/report/:submissionId/:concept',
    loadComponent: () =>
      import('./features/report/report.component').then(m => m.ReportComponent)
  }
];
