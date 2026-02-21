import { Component, output, signal, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, switchMap } from 'rxjs/operators';
import { ApiService, TypeaheadResult } from '../../services/api.service';

@Component({
  selector: 'app-titlebar',
  standalone: true,
  imports: [FormsModule],
  template: `
    <header class="titlebar">
      <div class="brand">
        <img src="favicon.svg" alt="" class="brand-icon" />
        <h1>Stocks Explorer</h1>
      </div>
      <div class="search-wrapper">
        <input
          type="text"
          placeholder="Search companies..."
          [(ngModel)]="searchText"
          (input)="onInput()"
          (keydown.enter)="onSearch()"
          (blur)="hideDropdown()"
        />
        @if (suggestions().length > 0) {
          <ul class="dropdown">
            @for (item of suggestions(); track item.text + item.cik) {
              <li (mousedown)="selectSuggestion(item)">
                <span class="suggestion-text">{{ item.text }}</span>
                <span class="suggestion-type">{{ item.type }}</span>
              </li>
            }
          </ul>
        }
      </div>
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
    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .brand-icon {
      width: 28px;
      height: 28px;
    }
    h1 {
      font-size: 18px;
      margin: 0;
    }
    .search-wrapper {
      position: relative;
    }
    input {
      padding: 6px 12px;
      border: 1px solid #475569;
      border-radius: 4px;
      background: #1e293b;
      color: #e2e8f0;
      width: 280px;
    }
    .dropdown {
      position: absolute;
      top: 100%;
      right: 0;
      width: 280px;
      background: #fff;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      margin: 4px 0 0;
      padding: 0;
      list-style: none;
      z-index: 100;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }
    .dropdown li {
      display: flex;
      justify-content: space-between;
      padding: 8px 12px;
      cursor: pointer;
      color: #0f172a;
    }
    .dropdown li:hover {
      background: #f1f5f9;
    }
    .suggestion-type {
      font-size: 11px;
      color: #94a3b8;
      text-transform: uppercase;
    }
  `]
})
export class TitlebarComponent implements OnDestroy {
  searchText = '';
  search = output<string>();
  suggestions = signal<TypeaheadResult[]>([]);

  private typeahead$ = new Subject<string>();
  private subscription: Subscription;

  constructor(private api: ApiService, private router: Router) {
    this.subscription = this.typeahead$.pipe(
      debounceTime(300),
      switchMap(q => this.api.getTypeahead(q))
    ).subscribe(results => this.suggestions.set(results));
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  onInput(): void {
    if (this.searchText.length >= 2) {
      this.typeahead$.next(this.searchText);
    } else {
      this.suggestions.set([]);
    }
  }

  onSearch(): void {
    this.suggestions.set([]);
    this.search.emit(this.searchText);
  }

  selectSuggestion(item: TypeaheadResult): void {
    this.suggestions.set([]);
    this.searchText = item.text;
    this.router.navigate(['/company', item.cik]);
  }

  hideDropdown(): void {
    setTimeout(() => this.suggestions.set([]), 200);
  }
}
