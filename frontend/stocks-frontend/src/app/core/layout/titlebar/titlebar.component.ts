import { Component, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-titlebar',
  standalone: true,
  imports: [FormsModule],
  template: `
    <header class="titlebar">
      <h1>Stocks Explorer</h1>
      <input
        type="text"
        placeholder="Search companies..."
        [(ngModel)]="searchText"
        (keydown.enter)="onSearch()"
      />
    </header>
  `,
  styles: [`
    .titlebar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 20px;
      height: 56px;
      background: #0f172a;
      color: #f8fafc;
    }
    h1 {
      font-size: 18px;
      margin: 0;
    }
    input {
      padding: 6px 12px;
      border: 1px solid #475569;
      border-radius: 4px;
      background: #1e293b;
      color: #e2e8f0;
      width: 280px;
    }
  `]
})
export class TitlebarComponent {
  searchText = '';
  search = output<string>();

  onSearch(): void {
    this.search.emit(this.searchText);
  }
}
