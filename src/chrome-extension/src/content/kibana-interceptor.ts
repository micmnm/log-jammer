// Runs in the ISOLATED world — has access to chrome.runtime APIs.
// Listens for messages from the MAIN world page-interceptor script
// and forwards them to the service worker.
// Also relays the verbose setting to the page interceptor.

// On load, read verbose setting and send it to the MAIN world interceptor
chrome.storage.local.get(['lj_settings'], (result) => {
  const verbose = result.lj_settings?.verbose ?? false;
  window.postMessage({
    source: 'logjammer-content-script',
    type: 'SET_VERBOSE',
    verbose,
  }, '*');
});

// Listen for settings changes and relay verbose flag
chrome.storage.onChanged.addListener((changes) => {
  if (changes.lj_settings) {
    const verbose = changes.lj_settings.newValue?.verbose ?? false;
    window.postMessage({
      source: 'logjammer-content-script',
      type: 'SET_VERBOSE',
      verbose,
    }, '*');
  }
});

// Listen for captured queries from the page-level interceptor
window.addEventListener('message', (event) => {
  if (event.source !== window) return;
  if (event.data?.source !== 'logjammer-page-interceptor') return;

  if (event.data.type === 'KIBANA_QUERY_CAPTURED') {
    try {
      chrome.runtime.sendMessage({
        type: 'KIBANA_QUERY_CAPTURED',
        payload: event.data.payload,
      });
    } catch {
      // Extension context may not be available
    }
  }
});

// Notify service worker that a Kibana page is active
try {
  chrome.runtime.sendMessage({ type: 'KIBANA_SESSION_ACTIVE' });
} catch {
  // Extension context may not be available
}
