import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="sidebar">
      <ul>
        <li>
          <a routerLink="/dashboard" routerLinkActive="active">Dashboard</a>
        </li>
        <li>
          <a routerLink="/search" routerLinkActive="active">Search</a>
        </li>
      </ul>
    </nav>
  `,
  styles: [`
    .sidebar {
      width: 200px;
      background: #1e293b;
      color: #e2e8f0;
      height: 100%;
      padding: 16px 0;
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    li a {
      display: block;
      padding: 10px 20px;
      color: #e2e8f0;
      text-decoration: none;
    }
    li a:hover {
      background: #334155;
    }
    li a.active {
      background: #3b82f6;
      color: #fff;
    }
  `]
})
export class SidebarComponent {}
