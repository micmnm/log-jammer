// Injected into the MAIN world — shares window with the page.
// Patches fetch AND XMLHttpRequest to detect Kibana search requests
// and posts them to the content script (isolated world) via window.postMessage.

let _verbose = false;

// Listen for verbose flag from content script (isolated world)
window.addEventListener('message', (event) => {
  if (event.source !== window) return;
  if (event.data?.source !== 'logjammer-content-script') return;
  if (event.data.type === 'SET_VERBOSE') {
    _verbose = !!event.data.verbose;
    console.log('[LogJammer] verbose mode:', _verbose ? 'ON' : 'OFF');
  }
});

function log(...args: unknown[]): void {
  console.log('[LogJammer]', ...args);
}

function vlog(...args: unknown[]): void {
  if (_verbose) console.log('[LogJammer][verbose]', ...args);
}

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

function extractIndexFromUrl(url: string): string {
  const match = url.match(/\/([^/]+)\/_(?:search|msearch)/);
  return match ? match[1] : 'unknown';
}

function isDocumentFetchRequest(batchItem: Record<string, unknown>): boolean {
  const options = batchItem.options as Record<string, unknown> | undefined;
  const execCtx = options?.executionContext as Record<string, unknown> | undefined;
  if (execCtx?.description === 'fetch documents') return true;

  // Fallback: if no executionContext, check if the request has size > 0
  const request = batchItem.request as Record<string, unknown> | undefined;
  const params = request?.params as Record<string, unknown> | undefined;
  const body = params?.body as Record<string, unknown> | undefined;
  if (body && typeof body.size === 'number' && body.size > 0) return true;

  return false;
}

interface SearchEntry {
  params: Record<string, unknown>;
  fullRequestBody: Record<string, unknown>;
}

function extractSearchEntries(parsed: Record<string, unknown>): SearchEntry[] {
  // Kibana bsearch format: { batch: [{ request: { params: { index, body } }, options: { executionContext: { description } } }] }
  if (Array.isArray(parsed.batch)) {
    const matchingItems = parsed.batch.filter((item: Record<string, unknown>) => {
      const match = isDocumentFetchRequest(item);
      vlog('batch item executionContext:', (item.options as Record<string, unknown>)?.executionContext, 'isDocFetch:', match);
      return match;
    });

    const results: SearchEntry[] = [];
    for (const item of matchingItems) {
      const request = (item as Record<string, unknown>).request as Record<string, unknown> | undefined;
      if (!request?.params) continue;
      results.push({
        params: request.params as Record<string, unknown>,
        fullRequestBody: { batch: [item] } as Record<string, unknown>,
      });
    }
    return results;
  }
  // Direct format: { params: { index, body } } or bare query { query: {...} }
  if (parsed.params && typeof parsed.params === 'object') {
    return [{ params: parsed.params as Record<string, unknown>, fullRequestBody: parsed }];
  }
  if (parsed.query) {
    return [{ params: { body: parsed }, fullRequestBody: parsed }];
  }
  return [];
}

// --- Sample field extraction from ES response ---

interface FieldSample {
  name: string;
  sampleValue: string;
}

function extractSampleFields(responseData: Record<string, unknown>): FieldSample[] {
  // Navigate the same nested structure as extractHits in service-worker
  let hits: Array<Record<string, unknown>> = [];

  const tryExtract = (data: Record<string, unknown>): Array<Record<string, unknown>> => {
    if (data.hits && typeof data.hits === 'object') {
      const h = data.hits as Record<string, unknown>;
      if (Array.isArray(h.hits)) return h.hits as Array<Record<string, unknown>>;
    }
    if (data.result && typeof data.result === 'object') return tryExtract(data.result as Record<string, unknown>);
    if (data.rawResponse && typeof data.rawResponse === 'object') return tryExtract(data.rawResponse as Record<string, unknown>);
    return [];
  };

  hits = tryExtract(responseData);
  if (hits.length === 0 && Array.isArray(responseData)) {
    for (const item of responseData as Array<Record<string, unknown>>) {
      const found = tryExtract(item);
      if (found.length > 0) { hits = found; break; }
    }
  }

  // Take first 10 hits to collect field names + sample values
  const sampleHits = hits.slice(0, 10);
  const fieldMap = new Map<string, string>();

  for (const hit of sampleHits) {
    const source = (hit._source as Record<string, unknown> | undefined) ?? {};
    const fields = hit.fields as Record<string, unknown> | undefined;

    const flatFields: Record<string, unknown> = { ...source };
    if (fields) {
      for (const [key, val] of Object.entries(fields)) {
        flatFields[key] = Array.isArray(val) && val.length === 1 ? val[0] : val;
      }
    }

    for (const [key, val] of Object.entries(flatFields)) {
      if (!fieldMap.has(key) && val !== null && val !== undefined) {
        fieldMap.set(key, String(val).slice(0, 80));
      }
    }
  }

  return Array.from(fieldMap.entries()).map(([name, sampleValue]) => ({ name, sampleValue }));
}

function processBody(
  url: string,
  bodyText: string,
  sampleFields?: FieldSample[]
): void {
  const lines = bodyText.split('\n').filter(Boolean);
  vlog('NDJSON lines:', lines.length);

  for (const line of lines) {
    try {
      const parsed = JSON.parse(line);
      vlog('parsed line keys:', Object.keys(parsed));

      const entries = extractSearchEntries(parsed);
      if (entries.length > 0) {
        for (const entry of entries) {
          const body = entry.params.body as Record<string, unknown> | undefined;
          if (!body?.query) continue;

          log('Query captured (fetch documents) from', url);
          vlog('queryDsl:', JSON.stringify(body, null, 2));
          window.postMessage({
            source: 'logjammer-page-interceptor',
            type: 'KIBANA_QUERY_CAPTURED',
            payload: {
              url,
              method: 'POST',
              queryDsl: body,
              fullRequestBody: entry.fullRequestBody,
              indexPattern: (entry.params.index as string) ?? extractIndexFromUrl(url),
              kibanaUrl: window.location.origin,
              capturedAt: new Date().toISOString(),
              sampleFields: sampleFields ?? [],
            },
          }, '*');
        }
        break;
      } else {
        vlog('no document-fetch entries found in this line');
      }
    } catch {
      // Skip non-JSON lines
    }
  }
}

// --- Body reading with decompression support ---

async function decompressBody(data: Uint8Array): Promise<string> {
  // Try deflate-raw first (what pako.deflate produces by default)
  const formats = ['deflate-raw', 'deflate', 'gzip'] as const;
  for (const format of formats) {
    try {
      const ds = new DecompressionStream(format);
      const writer = ds.writable.getWriter();
      const reader = ds.readable.getReader();

      writer.write(data as unknown as BufferSource);
      writer.close();

      const chunks: Uint8Array[] = [];
      let done = false;
      while (!done) {
        const result = await reader.read();
        done = result.done;
        if (result.value) chunks.push(result.value);
      }
      const totalLength = chunks.reduce((sum, c) => sum + c.length, 0);
      const merged = new Uint8Array(totalLength);
      let offset = 0;
      for (const chunk of chunks) {
        merged.set(chunk, offset);
        offset += chunk.length;
      }
      const text = new TextDecoder().decode(merged);
      vlog(`Decompressed body using ${format} (${data.length} → ${text.length} bytes)`);
      return text;
    } catch {
      // Try next format
    }
  }
  throw new Error('Could not decompress body with any known format');
}

async function readBodyRaw(body: BodyInit | null | undefined): Promise<Uint8Array | null> {
  if (!body) return null;
  if (typeof body === 'string') return new TextEncoder().encode(body);
  if (body instanceof ArrayBuffer) return new Uint8Array(body);
  if (body instanceof Uint8Array) return body;
  if (body instanceof Blob) return new Uint8Array(await body.arrayBuffer());
  if (body instanceof ReadableStream) {
    const reader = body.getReader();
    const chunks: Uint8Array[] = [];
    let done = false;
    while (!done) {
      const result = await reader.read();
      done = result.done;
      if (result.value) chunks.push(result.value);
    }
    const totalLength = chunks.reduce((sum, c) => sum + c.length, 0);
    const merged = new Uint8Array(totalLength);
    let offset = 0;
    for (const chunk of chunks) {
      merged.set(chunk, offset);
      offset += chunk.length;
    }
    return merged;
  }
  return null;
}

function isCompressedUrl(url: string): boolean {
  return url.includes('compress=true');
}

function looksLikeJson(data: Uint8Array): boolean {
  // JSON starts with { or [
  const first = data[0];
  return first === 0x7b || first === 0x5b; // '{' or '['
}

async function readBody(url: string, body: BodyInit | null | undefined): Promise<string | null> {
  const raw = await readBodyRaw(body);
  if (!raw || raw.length === 0) return null;

  // If URL says compressed and data doesn't look like plain JSON, decompress
  if (isCompressedUrl(url) && !looksLikeJson(raw)) {
    vlog('Body appears compressed, attempting decompression...');
    try {
      return await decompressBody(raw);
    } catch (err) {
      vlog('Decompression failed:', err);
      // Fall through to try as text
    }
  }

  return new TextDecoder().decode(raw);
}

// --- Patch fetch ---
(function patchFetch(): void {
  const originalFetch = window.fetch;
  log('fetch interceptor installed');

  window.fetch = async function (input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const method = init?.method ?? 'GET';

    if (method === 'POST') {
      vlog('[fetch] POST request:', url, 'search match:', isKibanaSearchRequest(url));
    }

    if (method === 'POST' && isKibanaSearchRequest(url)) {
      log('Intercepted search request:', url);
      vlog('[fetch] body type:', init?.body == null ? 'null' : (init.body as object).constructor.name);
      try {
        const bodyText = await readBody(url, init?.body);
        vlog('[fetch] body text (first 500 chars):', bodyText?.substring(0, 500));

        if (bodyText) {
          // Make the actual request and capture the response for field extraction
          const response = await originalFetch.call(this, input, init);

          // Clone the response so we can read it while also returning it to the page
          const responseClone = response.clone();

          // Async: extract sample fields from response body and then fire the query message
          responseClone.text().then(responseText => {
            let sampleFields: FieldSample[] = [];
            // Try single JSON first, then NDJSON (newline-delimited)
            try {
              const responseData = JSON.parse(responseText) as Record<string, unknown>;
              sampleFields = extractSampleFields(responseData);
            } catch {
              // NDJSON: try each line
              const lines = responseText.split('\n').filter(Boolean);
              for (const line of lines) {
                try {
                  const lineData = JSON.parse(line) as Record<string, unknown>;
                  sampleFields = extractSampleFields(lineData);
                  if (sampleFields.length > 0) break;
                } catch { /* skip */ }
              }
            }
            vlog('[fetch] extracted', sampleFields.length, 'sample fields from response');
            processBody(url, bodyText, sampleFields);
          }).catch(() => {
            // If response read fails, still capture query without fields
            processBody(url, bodyText, []);
          });

          return response;
        }
      } catch (err) {
        vlog('[fetch] error reading body:', err);
      }
    }

    return originalFetch.call(this, input, init);
  };
})();

// --- Patch XMLHttpRequest ---
(function patchXHR(): void {
  const originalOpen = XMLHttpRequest.prototype.open;
  const originalSend = XMLHttpRequest.prototype.send;
  log('XHR interceptor installed');

  XMLHttpRequest.prototype.open = function (method: string, url: string | URL, ...rest: unknown[]) {
    (this as XMLHttpRequest & { _ljMethod?: string; _ljUrl?: string })._ljMethod = method;
    (this as XMLHttpRequest & { _ljUrl?: string })._ljUrl = typeof url === 'string' ? url : url.href;
    return (originalOpen as Function).apply(this, [method, url, ...rest]);
  };

  XMLHttpRequest.prototype.send = function (body?: Document | XMLHttpRequestBodyInit | null) {
    const xhr = this as XMLHttpRequest & { _ljMethod?: string; _ljUrl?: string };
    const method = xhr._ljMethod ?? '';
    const url = xhr._ljUrl ?? '';

    if (method.toUpperCase() === 'POST') {
      vlog('[XHR] POST request:', url, 'search match:', isKibanaSearchRequest(url));
    }

    if (method.toUpperCase() === 'POST' && isKibanaSearchRequest(url)) {
      log('Intercepted XHR search request:', url);
      vlog('[XHR] body type:', body == null ? 'null' : (body as object).constructor.name);

      // Capture body text for request processing
      let bodyText: string | null = null;
      if (typeof body === 'string') bodyText = body;
      else if (body instanceof ArrayBuffer) bodyText = new TextDecoder().decode(body);
      else if (body instanceof Uint8Array) bodyText = new TextDecoder().decode(body);
      else if (body instanceof Blob) {
        body.text().then(text => {
          vlog('[XHR] async blob body (first 500 chars):', text.substring(0, 500));
          processBody(url, text, []);
        });
      }

      if (bodyText) {
        const capturedBodyText = bodyText;
        vlog('[XHR] body text (first 500 chars):', capturedBodyText.substring(0, 500));

        // Listen for response to extract sample fields
        xhr.addEventListener('load', function () {
          let sampleFields: FieldSample[] = [];
          try {
            const responseText = xhr.responseText;
            const responseData = JSON.parse(responseText) as Record<string, unknown>;
            sampleFields = extractSampleFields(responseData);
            vlog('[XHR] extracted', sampleFields.length, 'sample fields from response');
          } catch {
            vlog('[XHR] could not parse XHR response for field extraction');
          }
          processBody(url, capturedBodyText, sampleFields);
        });
      }
    }

    return originalSend.call(this, body);
  };
})();
