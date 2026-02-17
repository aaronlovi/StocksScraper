import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CompanyDetail {
  companyId: number;
  cik: number;
  dataSource: string;
  tickers: { ticker: string; exchange: string }[];
}

export interface SubmissionItem {
  submissionId: number;
  filingType: string;
  filingCategory: string;
  reportDate: string;
  filingReference: string;
}

export interface CompanySearchResult {
  companyId: number;
  cik: string;
  companyName: string;
  ticker: string | null;
  exchange: string | null;
}

export interface PaginationResponse {
  pageNumber: number;
  totalItems: number;
  totalPages: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  pagination: PaginationResponse;
}

export interface DashboardStats {
  totalCompanies: number;
  totalSubmissions: number;
  totalDataPoints: number;
  earliestFilingDate: string | null;
  latestFilingDate: string | null;
  companiesWithPriceData: number;
  submissionsByFilingType: Record<string, number>;
}

export interface TypeaheadResult {
  text: string;
  type: string;
  cik: string;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  getCompany(cik: string): Observable<CompanyDetail> {
    return this.http.get<CompanyDetail>(`/api/companies/${cik}`);
  }

  getSubmissions(cik: string): Observable<{ items: SubmissionItem[] }> {
    return this.http.get<{ items: SubmissionItem[] }>(`/api/companies/${cik}/submissions`);
  }

  searchCompanies(query: string, page: number, pageSize: number): Observable<PaginatedResponse<CompanySearchResult>> {
    return this.http.get<PaginatedResponse<CompanySearchResult>>(
      `/api/search?q=${encodeURIComponent(query)}&page=${page}&pageSize=${pageSize}`
    );
  }

  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>('/api/dashboard/stats');
  }

  getTypeahead(query: string): Observable<TypeaheadResult[]> {
    return this.http.get<TypeaheadResult[]>(`/api/typeahead?q=${encodeURIComponent(query)}`);
  }
}
