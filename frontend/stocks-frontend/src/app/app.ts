import { Component } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { SidebarComponent } from './core/layout/sidebar/sidebar.component';
import { TitlebarComponent } from './core/layout/titlebar/titlebar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, TitlebarComponent],
  template: `
    <app-titlebar (search)="onSearch($event)" />
    <div class="app-body">
      <app-sidebar />
      <main class="content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      height: 100vh;
    }
    .app-body {
      display: flex;
      flex: 1;
      overflow: hidden;
    }
    .content {
      flex: 1;
      padding: 20px;
      overflow-y: auto;
      background: #f8fafc;
    }
  `]
})
export class App {
  constructor(private router: Router) {}

  onSearch(query: string): void {
    this.router.navigate(['/search'], { queryParams: { q: query } });
  }
}
