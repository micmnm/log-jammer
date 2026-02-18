import { summarizeQuery, extractIndexPattern } from '../kibana-query-parser';

describe('kibana-query-parser', () => {
  describe('summarizeQuery', () => {
    it('summarizes a simple match query', () => {
      const query = { query: { match: { 'log.level': 'ERROR' } } };
      expect(summarizeQuery(query)).toContain('log.level:ERROR');
    });

    it('summarizes a bool query with must clauses', () => {
      const query = {
        query: {
          bool: {
            must: [
              { match: { 'log.level': 'ERROR' } },
              { match: { 'service.name': 'api-gateway' } }
            ]
          }
        }
      };
      const summary = summarizeQuery(query);
      expect(summary).toContain('log.level:ERROR');
      expect(summary).toContain('service.name:api-gateway');
    });

    it('summarizes a range filter', () => {
      const query = {
        query: {
          bool: {
            filter: [
              { range: { '@timestamp': { gte: 'now-15m', lte: 'now' } } }
            ]
          }
        }
      };
      expect(summarizeQuery(query)).toContain('@timestamp');
    });

    it('returns fallback for empty query', () => {
      expect(summarizeQuery({})).toBe('(all documents)');
    });

    it('summarizes query_string queries', () => {
      const query = { query: { query_string: { query: 'status:500 AND path:/api/*' } } };
      expect(summarizeQuery(query)).toBe('status:500 AND path:/api/*');
    });
  });

  describe('extractIndexPattern', () => {
    it('extracts index from Kibana bsearch URL', () => {
      expect(extractIndexPattern('/internal/bsearch', { params: { index: 'logs-*' } })).toBe('logs-*');
    });

    it('returns unknown for unrecognized format', () => {
      expect(extractIndexPattern('/some/url', {})).toBe('unknown');
    });
  });
});
