# Moat Score Design

## Table of Contents

- [1. Overview](#1-overview)
- [2. Philosophy](#2-philosophy)
  - [2.1. Philosophy - Key Differences from Value Score](#21-philosophy---key-differences-from-value-score)
  - [2.2. Philosophy - Target Companies](#22-philosophy---target-companies)
- [3. Scoring Checks](#3-scoring-checks)
- [4. Supplementary Trend Charts](#4-supplementary-trend-charts)
  - [4.1. Supplementary Trend Charts - Design Note on Stability](#41-supplementary-trend-charts---design-note-on-stability)
  - [4.2. Supplementary Trend Charts - Proposed Charts](#42-supplementary-trend-charts---proposed-charts)
- [5. Data Requirements](#5-data-requirements)
  - [5.1. Data Requirements - Already Available](#51-data-requirements---already-available)
  - [5.2. Data Requirements - New Concepts Needed](#52-data-requirements---new-concepts-needed)

## 1. Overview

A second scoring system inspired by Buffett's later investment philosophy (credited to Charlie Munger's influence): *"It's far better to buy a wonderful company at a fair price than a fair company at a wonderful price."*

Where the existing **Value Score** rewards Ben Graham-style deep value (high tangible book value, low debt, cheap price-to-book), the **Moat Score** seeks companies whose competitive advantage comes from brand, pricing power, and capital-light operations rather than balance sheet assets.

## 2. Philosophy

### 2.1. Philosophy - Key Differences from Value Score

| Dimension | Value Score | Moat Score |
|-----------|------------|------------|
| Book value | Requires $150M+ tangible equity | No minimum — brand value matters more |
| Price-to-book | Must be <= 3.0x | No check — irrelevant when value is intangible |
| Debt-to-equity | Conservative (< 0.5) | More permissive (< 1.0) |
| ROE threshold | >= 10% | >= 15% — moat companies earn above-average returns |
| Margins | Not checked | Gross >= 40%, operating >= 15% — pricing power is the signal |
| Consistency | Not directly checked | Never-unprofitable, consistent capital return |
| Capital intensity | Not checked | CapEx / Owner Earnings < 50% — capital-light is better |
| History required | 4 years | 7 years — moats are proven over time |
| Return threshold | > 5% | > 3% — paying up for quality is acceptable |

### 2.2. Philosophy - Target Companies

Companies like Coca-Cola, Apple, Visa, Mastercard, Procter & Gamble, Moody's — high margins, high ROE, capital-light, consistent returns, strong brands. These mostly fail the Value Score (too expensive on P/B, low tangible book) but would score well on the Moat Score.

## 3. Scoring Checks

All margin and return checks that reference an "average" compute the metric per fiscal year (from annual 10-K data only), then average those yearly values across all available years. This is the same averaging pattern used by the Value Score for owner earnings, net cash flow, and ROE.

### 3.1. Scoring Checks - Computation Notes

- **Gross margin (per year)**: `Gross Profit / Revenue * 100`. Gross Profit is resolved directly from the `GrossProfit` concept if reported; otherwise derived as `Revenue - COGS`. Revenue and COGS each use fallback chains (see section 5.2). If neither Gross Profit nor COGS is available for a year, that year is excluded from the average. The check returns `NotAvailable` if no years have computable gross margin.
- **Operating margin (per year)**: `Operating Income / Revenue * 100`. Same averaging and fallback pattern. This deliberately uses `OperatingIncomeLoss` rather than `NetIncomeLoss` because net income is subject to manipulation in individual years through timing of asset sales/write-downs, tax provision adjustments, one-time restructuring charges, and interest expense from financing decisions. Operating income captures core business profitability before these distortions.
- **Revenue CAGR**: `(Revenue_latest / Revenue_oldest) ^ (1 / years) - 1`, using the oldest and most recent fiscal years with revenue data.
- **CapEx ratio**: `Average annual CapEx / Average annual Owner Earnings * 100`, using the same CapEx and Owner Earnings values already computed by the Value Score.
- **Interest coverage**: `Operating Income / Interest Expense`, using the most recent fiscal year.

### 3.2. Scoring Checks - Check Table

| # | Check | Threshold | Rationale |
|---|-------|-----------|-----------|
| 1 | High ROE (CF) avg | >= 15% | Moat companies earn outsized returns on equity consistently |
| 2 | High ROE (OE) avg | >= 15% | Same, owner-earnings basis |
| 3 | Gross margin avg | >= 40% | Pricing power — the hallmark of brand/moat value |
| 4 | Operating margin avg | >= 15% | Efficient operations — not just high prices but cost discipline |
| 5 | Revenue growth | CAGR > 3% over available years | A moat should defend and expand market share |
| 6 | Positive owner earnings every year | 0 failing years | A true franchise doesn't lose money |
| 7 | Low capex ratio | CapEx / Owner Earnings < 50% | Capital-light businesses retain more earnings |
| 8 | Consistent dividend or buyback | Returned capital in >= 75% of years | Management returning excess cash to owners |
| 9 | Debt-to-equity | < 1.0 | Still prudent on debt, but more permissive than the value score's 0.5 |
| 10 | Interest coverage | Operating Income / Interest Expense > 5x | Can easily service debt from operations |
| 11 | History | >= 7 years | Longer track record — moats are proven over time |
| 12 | Estimated return (OE) | > 3% | Lower floor — paying up for quality is acceptable |
| 13 | Estimated return (OE) | < 40% | Same sanity cap — avoid distressed/anomalous situations |

## 4. Supplementary Trend Charts

### 4.1. Supplementary Trend Charts - Design Note on Stability

Standard deviation over 5-7 years of data is not a meaningful stability measure. Instead of computing STDDEV-based scoring checks for margin and ROE consistency, these metrics will be **graphed as trend charts** on the Moat Score detail page, using the same format as the existing AR / Revenue sparkline. This lets the investor visually assess consistency and trajectory without reducing it to a single pass/fail number.

### 4.2. Supplementary Trend Charts - Proposed Charts

Each chart displays yearly values as a table plus an SVG sparkline, identical in format to the existing AR / Revenue trend chart.

| Chart | Y-axis | Purpose |
|-------|--------|---------|
| AR / Revenue % | Accounts receivable / revenue ratio by year | Detect earnings quality deterioration (already exists in Value Score page) |
| Gross Margin % | Gross margin by year | Visualize pricing power stability and trajectory |
| Operating Margin % | Operating margin by year | Visualize operational efficiency over time |
| ROE (CF) % | Cash-flow-based ROE by year | Visualize return consistency |
| ROE (OE) % | Owner-earnings-based ROE by year | Visualize return consistency |
| Revenue | Revenue by year | Visualize growth trajectory |

## 5. Data Requirements

### 5.1. Data Requirements - Already Available

The existing scoring infrastructure already computes:

- ROE (CF and OE) per year
- Owner earnings per year
- CapEx
- Dividends paid
- Debt-to-equity ratio
- Estimated returns
- Revenue (used in AR/Revenue ratio)

### 5.2. Data Requirements - New Concepts Needed

| Concept | XBRL Tag(s) | Used For |
|---------|-------------|----------|
| Revenue | `Revenues`, `RevenueFromContractWithCustomerExcludingAssessedTax`, `SalesRevenueNet` | Gross margin, revenue growth |
| Cost of Goods Sold | `CostOfGoodsAndServicesSold`, `CostOfRevenue`, `CostOfGoodsSold` | Gross margin |
| Operating Income | `OperatingIncomeLoss` | Operating margin, interest coverage |
| Interest Expense | `InterestExpense`, `InterestExpenseDebt` | Interest coverage |
| Gross Profit | `GrossProfit` (alternative to Revenue - COGS) | Gross margin |
