// src/chrome-extension/src/content/kibana-interceptor.ts

const KIBANA_SEARCH_PATTERNS = [
  '/internal/search/es',
  '/internal/bsearch',
  '/api/console/proxy',
  '/elasticsearch/',
  '/_search',
  '/_msearch',
];

function isKibanaSearchRequest(url: string): boolean {
  return KIBANA_SEARCH_PATTERNS.some(pattern => url.includes(pattern));
}

function patchFetch(): void {
  const originalFetch = window.fetch;

  window.fetch = async function (input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const method = init?.method ?? 'GET';

    if (method === 'POST' && isKibanaSearchRequest(url)) {
      try {
        const bodyText = typeof init?.body === 'string'
          ? init.body
          : init?.body instanceof ArrayBuffer
            ? new TextDecoder().decode(init.body)
            : null;

        if (bodyText) {
          // Kibana bsearch sends newline-delimited JSON
          const lines = bodyText.split('\n').filter(Boolean);
          for (const line of lines) {
            try {
              const parsed = JSON.parse(line);
              if (parsed.params?.body?.query || parsed.query) {
                chrome.runtime.sendMessage({
                  type: 'KIBANA_QUERY_CAPTURED',
                  payload: {
                    url,
                    method,
                    queryDsl: parsed.params?.body ?? parsed,
                    indexPattern: parsed.params?.index ?? extractIndexFromUrl(url),
                    kibanaUrl: window.location.origin,
                    capturedAt: new Date().toISOString(),
                  },
                });
                break; // Only capture the first meaningful query per request
              }
            } catch {
              // Skip non-JSON lines (e.g., NDJSON batch headers)
            }
          }
        }
      } catch {
        // Never break page functionality
      }
    }

    return originalFetch.call(this, input, init);
  };
}

function extractIndexFromUrl(url: string): string {
  const match = url.match(/\/([^/]+)\/_(?:search|msearch)/);
  return match ? match[1] : 'unknown';
}

// Notify service worker that a Kibana page is active (resumes paused subscriptions)
function notifyKibanaActive(): void {
  try {
    chrome.runtime.sendMessage({ type: 'KIBANA_SESSION_ACTIVE' });
  } catch {
    // Extension context may not be available
  }
}

// Run immediately at document_start
patchFetch();
notifyKibanaActive();
