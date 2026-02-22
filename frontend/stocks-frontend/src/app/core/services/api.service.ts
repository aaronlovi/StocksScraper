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

export interface ScoringCheckResponse {
  checkNumber: number;
  name: string;
  computedValue: number | null;
  threshold: string;
  result: 'pass' | 'fail' | 'na';
}

export interface DerivedMetricsResponse {
  bookValue: number | null;
  marketCap: number | null;
  debtToEquityRatio: number | null;
  priceToBookRatio: number | null;
  debtToBookRatio: number | null;
  adjustedRetainedEarnings: number | null;
  oldestRetainedEarnings: number | null;
  averageNetCashFlow: number | null;
  averageOwnerEarnings: number | null;
  averageRoeCF: number | null;
  averageRoeOE: number | null;
  estimatedReturnCF: number | null;
  estimatedReturnOE: number | null;
  currentDividendsPaid: number | null;
}

export interface CompanyScoreSummary {
  companyId: number;
  cik: string;
  companyName: string | null;
  ticker: string | null;
  exchange: string | null;
  overallScore: number;
  computableChecks: number;
  yearsOfData: number;
  bookValue: number | null;
  marketCap: number | null;
  debtToEquityRatio: number | null;
  priceToBookRatio: number | null;
  debtToBookRatio: number | null;
  adjustedRetainedEarnings: number | null;
  averageNetCashFlow: number | null;
  averageOwnerEarnings: number | null;
  averageRoeCF: number | null;
  averageRoeOE: number | null;
  estimatedReturnCF: number | null;
  estimatedReturnOE: number | null;
  pricePerShare: number | null;
  priceDate: string | null;
  sharesOutstanding: number | null;
  currentDividendsPaid: number | null;
  maxBuyPrice: number | null;
  percentageUpside: number | null;
  return1y: number | null;
  computedAt: string;
}

export interface ScoresReportParams {
  page: number;
  pageSize: number;
  sortBy: string;
  sortDir: string;
  minScore: number | null;
  exchange: string | null;
}

export interface ArRevenueRow {
  year: number;
  accountsReceivable: number | null;
  revenue: number | null;
  ratio: number | null;
  arConceptUsed: string | null;
  revenueConceptUsed: string | null;
}

export interface ScoringResponse {
  rawDataByYear: Record<string, Record<string, number>>;
  metrics: DerivedMetricsResponse;
  scorecard: ScoringCheckResponse[];
  overallScore: number;
  computableChecks: number;
  yearsOfData: number;
  pricePerShare: number | null;
  priceDate: string | null;
  sharesOutstanding: number | null;
  maxBuyPrice: number | null;
  percentageUpside: number | null;
}

export interface MoatDerivedMetricsResponse {
  averageGrossMargin: number | null;
  averageOperatingMargin: number | null;
  averageRoeCF: number | null;
  averageRoeOE: number | null;
  revenueCagr: number | null;
  capexRatio: number | null;
  interestCoverage: number | null;
  debtToEquityRatio: number | null;
  estimatedReturnOE: number | null;
  currentDividendsPaid: number | null;
  marketCap: number | null;
  pricePerShare: number | null;
  positiveOeYears: number;
  totalOeYears: number;
  capitalReturnYears: number;
  totalCapitalReturnYears: number;
}

export interface MoatYearMetrics {
  year: number;
  grossMarginPct: number | null;
  operatingMarginPct: number | null;
  roeCfPct: number | null;
  roeOePct: number | null;
  revenue: number | null;
}

export interface MoatScoringResponse {
  rawDataByYear: Record<string, Record<string, number>>;
  metrics: MoatDerivedMetricsResponse;
  scorecard: ScoringCheckResponse[];
  trendData: MoatYearMetrics[];
  overallScore: number;
  computableChecks: number;
  yearsOfData: number;
  pricePerShare: number | null;
  priceDate: string | null;
  sharesOutstanding: number | null;
}

export interface InvestmentReturnResponse {
  ticker: string;
  startDate: string;
  endDate: string;
  startPrice: number;
  endPrice: number;
  totalReturnPct: number;
  annualizedReturnPct: number | null;
  currentValueOf1000: number;
}

export interface CompanyMoatScoreSummary {
  companyId: number;
  cik: string;
  companyName: string | null;
  ticker: string | null;
  exchange: string | null;
  overallScore: number;
  computableChecks: number;
  yearsOfData: number;
  averageGrossMargin: number | null;
  averageOperatingMargin: number | null;
  averageRoeCF: number | null;
  averageRoeOE: number | null;
  estimatedReturnOE: number | null;
  revenueCagr: number | null;
  capexRatio: number | null;
  interestCoverage: number | null;
  debtToEquityRatio: number | null;
  pricePerShare: number | null;
  priceDate: string | null;
  sharesOutstanding: number | null;
  return1y: number | null;
  computedAt: string;
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

  getArRevenue(cik: string): Observable<ArRevenueRow[]> {
    return this.http.get<ArRevenueRow[]>(`/api/companies/${cik}/ar-revenue`);
  }

  getScoring(cik: string): Observable<ScoringResponse> {
    return this.http.get<ScoringResponse>(`/api/companies/${cik}/scoring`);
  }

  getScoresReport(params: ScoresReportParams): Observable<PaginatedResponse<CompanyScoreSummary>> {
    const parts: string[] = [
      `page=${params.page}`,
      `pageSize=${params.pageSize}`,
      `sortBy=${params.sortBy}`,
      `sortDir=${params.sortDir}`
    ];
    if (params.minScore != null) parts.push(`minScore=${params.minScore}`);
    if (params.exchange != null) parts.push(`exchange=${encodeURIComponent(params.exchange)}`);
    return this.http.get<PaginatedResponse<CompanyScoreSummary>>(
      `/api/reports/scores?${parts.join('&')}`
    );
  }

  getInvestmentReturn(cik: string, startDate?: string): Observable<InvestmentReturnResponse> {
    const parts: string[] = [];
    if (startDate) parts.push(`startDate=${startDate}`);
    const qs = parts.length ? `?${parts.join('&')}` : '';
    return this.http.get<InvestmentReturnResponse>(`/api/companies/${cik}/investment-return${qs}`);
  }

  getMoatScoring(cik: string): Observable<MoatScoringResponse> {
    return this.http.get<MoatScoringResponse>(`/api/companies/${cik}/moat-scoring`);
  }

  getMoatScoresReport(params: ScoresReportParams): Observable<PaginatedResponse<CompanyMoatScoreSummary>> {
    const parts: string[] = [
      `page=${params.page}`,
      `pageSize=${params.pageSize}`,
      `sortBy=${params.sortBy}`,
      `sortDir=${params.sortDir}`
    ];
    if (params.minScore != null) parts.push(`minScore=${params.minScore}`);
    if (params.exchange != null) parts.push(`exchange=${encodeURIComponent(params.exchange)}`);
    return this.http.get<PaginatedResponse<CompanyMoatScoreSummary>>(
      `/api/reports/moat-scores?${parts.join('&')}`
    );
  }
}
