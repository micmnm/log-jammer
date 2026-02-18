export function summarizeQuery(queryDsl: Record<string, unknown>): string {
  const query = queryDsl.query as Record<string, unknown> | undefined;
  if (!query) return '(all documents)';

  const parts: string[] = [];

  if ('query_string' in query) {
    const qs = query.query_string as Record<string, unknown>;
    return (qs.query as string) || '(all documents)';
  }

  if ('match' in query) {
    parts.push(summarizeMatch(query.match as Record<string, unknown>));
  }

  if ('bool' in query) {
    const bool = query.bool as Record<string, unknown>;
    for (const clause of ['must', 'filter', 'should'] as const) {
      const items = bool[clause];
      if (Array.isArray(items)) {
        for (const item of items) {
          parts.push(summarizeClause(item as Record<string, unknown>));
        }
      }
    }
  }

  return parts.filter(Boolean).join(' AND ') || '(all documents)';
}

function summarizeMatch(match: Record<string, unknown>): string {
  return Object.entries(match)
    .map(([field, value]) => `${field}:${value}`)
    .join(' AND ');
}

function summarizeClause(clause: Record<string, unknown>): string {
  if ('match' in clause) {
    return summarizeMatch(clause.match as Record<string, unknown>);
  }
  if ('match_phrase' in clause) {
    return summarizeMatch(clause.match_phrase as Record<string, unknown>);
  }
  if ('term' in clause) {
    return summarizeMatch(clause.term as Record<string, unknown>);
  }
  if ('range' in clause) {
    const range = clause.range as Record<string, unknown>;
    const field = Object.keys(range)[0];
    const bounds = range[field] as Record<string, unknown>;
    const parts = Object.entries(bounds).map(([op, val]) => `${op}:${val}`);
    return `${field}[${parts.join(',')}]`;
  }
  if ('query_string' in clause) {
    const qs = clause.query_string as Record<string, unknown>;
    return (qs.query as string) || '';
  }
  return '';
}

export function extractIndexPattern(
  url: string,
  body: Record<string, unknown>
): string {
  if (body.params && typeof body.params === 'object') {
    const params = body.params as Record<string, unknown>;
    if (typeof params.index === 'string') return params.index;
  }
  const urlMatch = url.match(/\/([^/]+)\/_(?:search|msearch)/);
  if (urlMatch) return urlMatch[1];
  return 'unknown';
}
