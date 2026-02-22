import { Component, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ApiService, DashboardStats } from '../../core/services/api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  stats = signal<DashboardStats | null>(null);
  error = signal<string | null>(null);

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.api.getDashboardStats().subscribe({
      next: data => this.stats.set(data),
      error: () => this.error.set('Failed to load dashboard stats.')
    });
  }
}
