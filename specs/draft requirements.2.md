# Log Jammer - Refined Requirements Document

## Overview

Log Jammer is a proactive monitoring application that helps users detect significant events in their applications or platforms by analyzing logs and metrics. It identifies error spikes, performance degradation, and new/unknown errors to enable faster incident response.

## Core Capabilities

### 1. Data Source Integration

- **Multiple pluggable data sources** with adapter pattern
- Initial supported sources:
  - Elasticsearch/OpenSearch (ELK stack)
  - Loki (Grafana ecosystem)
  - PostgreSQL (direct database queries)
- Extensible architecture for future adapters (Splunk, CloudWatch, etc.)

### 2. Error Detection & Classification

#### Error Grouping
- **Custom fields per DataSource** for grouping errors
  - Example: group by `error_code`, `service_name`, `endpoint`, etc.
  - Configurable per data source adapter

#### Classification System
- **Hybrid classifier** combining:
  - Rule-based pattern matching (primary)
  - Optional local ML model for advanced classification
- No remote LLM or GPU required
- Maintains a "library" of known errors for comparison

#### Detection Types
| Detection Type | Description |
|---------------|-------------|
| Error Spikes | Significant increase in error frequency |
| New Errors | Errors not seen in the known library |
| Performance Degradation | Metrics like call duration exceeding thresholds |

### 3. Spike Detection

- **Configurable rules per metric**
- Threshold types:
  - Absolute thresholds
  - Percentage increase over baseline
  - Standard deviation from rolling average

### 4. Time Windows & Analysis

All detection mechanisms support:

| Window Type | Options |
|-------------|---------|
| Fixed Windows | 5 min, 15 min, 60 min |
| Historical Comparison | Compare to same period yesterday/last week |
| Rolling Averages | Moving average for baseline calculation |

## Architecture

### Deployment Model

- **MVP**: Single container deployment
- **Design for scale**: Architecture supports future horizontal scaling
- Container-ready (.NET application)

### Technology Stack

| Component | Technology |
|-----------|------------|
| Backend API | .NET (C#) |
| Frontend | React or Vue SPA |
| Database | PostgreSQL |
| Containerization | Docker |

### Database Schema (Conceptual)

- Error library (known errors with fingerprints)
- Detection rules configuration
- Data source configurations
- Alert history
- Metrics snapshots

## User Interface

### Dashboard (MVP Focus)

- Real-time view of significant events
- Error classification interface
- Spike visualization
- Known vs. unknown error indicators

### Configuration Interface

- Data source management
- Detection rule configuration
- Error grouping field selection
- Time window preferences

## MVP Scope

### Included
- Single container deployment
- Dashboard-based notifications (in-app only)
- PostgreSQL database
- Basic authentication: **None** (internal tool assumption)
- 30-day data retention

### Excluded from MVP (Future)
- External notifications (Slack, email, PagerDuty)
- User authentication/authorization
- Multi-tenant support
- Horizontal scaling infrastructure

## Performance Requirements

| Metric | Target |
|--------|--------|
| Log ingestion rate | 10,000+ logs/minute |
| Processing | Real-time with < 30 second delay |
| Resource efficiency | No GPU required |
| AI/ML | Local only, no remote LLM calls |

## Data Retention

- **Default retention period**: 30 days
- Applies to:
  - Raw log references
  - Computed metrics
  - Alert history
- Error library (known errors): Retained indefinitely

## Non-Functional Requirements

### Resource Efficiency
- Lightweight processing
- Efficient memory usage for high-volume log processing
- Optimized database queries

### Extensibility
- Pluggable adapter architecture for data sources
- Configurable detection rules
- Modular classifier system

### Observability
- Application health metrics
- Processing lag indicators
- Adapter connection status

## Open Questions for Future Iterations

1. Specific ML model selection for optional classification
2. Dashboard visualization library (Chart.js, D3, etc.)
3. API rate limiting strategy
4. Backup and disaster recovery procedures
