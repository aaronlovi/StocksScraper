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
  templateUrl: './titlebar.component.html',
  styleUrls: ['./titlebar.component.css']
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
