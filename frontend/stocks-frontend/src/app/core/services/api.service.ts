import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CompanyDetail {
  companyId: number;
  cik: number;
  dataSource: string;
  companyName: string | null;
  latestPrice: number | null;
  latestPriceDate: string | null;
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
  latestPrice: number | null;
  latestPriceDate: string | null;
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

export interface StatementListItem {
  roleName: string;
  rootConceptName: string;
  rootLabel: string;
}

export interface StatementTreeNode {
  conceptName: string;
  label: string;
  documentation?: string | null;
  value: string | null;
  children?: StatementTreeNode[];
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

  listStatements(cik: string, submissionId: number): Observable<StatementListItem[]> {
    return this.http.get<StatementListItem[]>(
      `/api/companies/${cik}/submissions/${submissionId}/statements`
    );
  }

  getStatement(cik: string, submissionId: number, concept: string, taxonomyYear?: number, roleName?: string): Observable<StatementTreeNode> {
    const params: string[] = [];
    if (taxonomyYear) params.push(`taxonomyYear=${taxonomyYear}`);
    if (roleName) params.push(`roleName=${encodeURIComponent(roleName)}`);
    let url = `/api/companies/${cik}/submissions/${submissionId}/statements/${concept}`;
    if (params.length > 0) url += '?' + params.join('&');
    return this.http.get<StatementTreeNode>(url);
  }

  getTypeahead(query: string): Observable<TypeaheadResult[]> {
    return this.http.get<TypeaheadResult[]>(`/api/typeahead?q=${encodeURIComponent(query)}`);
  }
}
