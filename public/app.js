const $ = (id) => document.getElementById(id);

const keywordsEl = $("keywords");
const maxEl = $("max");
const refreshEl = $("refreshSec");
const scanBtn = $("scanBtn");
const resultsEl = $("results");
const statusEl = $("status");

let timer = null;

function setStatus(msg, type = "") {
  statusEl.textContent = msg;
  statusEl.className = "status-badge";
  if (type) {
    statusEl.classList.add(type);
  }
}

function formatViewers(n) {
  if (n === null || n === undefined) return null;
  return n.toLocaleString();
}

function formatStartTime(isoString) {
  if (!isoString) return "Unknown";
  const date = new Date(isoString);
  const now = new Date();
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);

  if (diffMins < 1) return "Just started";
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ${diffMins % 60}m ago`;
  return date.toLocaleDateString();
}

function escapeHtml(s) {
  return String(s)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function renderEmptyState() {
  resultsEl.innerHTML = `
    <div class="empty-state">
      <div class="empty-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
          <path d="M8 21h8"></path>
          <path d="M12 17v4"></path>
          <polygon points="10 8 16 11.5 10 15 10 8" fill="currentColor" stroke="none"></polygon>
        </svg>
      </div>
      <h3>Ready to Scan</h3>
      <p>Enter keywords and click "Scan Now" to discover live streams</p>
    </div>
  `;
}

function renderNoResults() {
  resultsEl.innerHTML = `
    <div class="empty-state">
      <div class="empty-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <circle cx="11" cy="11" r="8"></circle>
          <path d="m21 21-4.35-4.35"></path>
          <path d="M8 8l6 6"></path>
          <path d="M14 8l-6 6"></path>
        </svg>
      </div>
      <h3>No Live Streams Found</h3>
      <p>Try different keywords or check back later</p>
    </div>
  `;
}

function render(results) {
  if (!results.length) {
    renderNoResults();
    return;
  }

  resultsEl.innerHTML = "";

  for (const r of results) {
    const viewers = formatViewers(r.concurrentViewers);
    const hasViewers = viewers !== null;

    const card = document.createElement("div");
    card.className = "stream-card";
    card.innerHTML = `
      <div class="thumbnail-wrapper">
        <img class="thumbnail" src="${escapeHtml(r.thumbnail)}" alt="" loading="lazy" />
        <div class="live-badge">
          <span class="live-dot"></span>
          Live
        </div>
      </div>
      <div class="stream-info">
        <h3 class="stream-title">
          <a href="${escapeHtml(r.url)}" target="_blank" rel="noopener noreferrer">${escapeHtml(r.title)}</a>
        </h3>
        <div class="stream-meta">
          <div class="meta-item">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
              <circle cx="12" cy="7" r="4"></circle>
            </svg>
            <span>${escapeHtml(r.channelTitle)}</span>
          </div>
          <div class="meta-item">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <polyline points="12 6 12 12 16 14"></polyline>
            </svg>
            <span>Started ${formatStartTime(r.actualStartTime)}</span>
          </div>
        </div>
      </div>
      <div class="stream-stats">
        <div class="viewer-count ${hasViewers ? '' : 'no-data'}">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
            <circle cx="9" cy="7" r="4"></circle>
            <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
            <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
          </svg>
          <span class="viewer-number">${hasViewers ? viewers : '—'}</span>
          <span class="viewer-label">viewers</span>
        </div>
      </div>
    `;
    resultsEl.appendChild(card);
  }
}

async function scanOnce() {
  const q = keywordsEl.value.trim();
  const max = maxEl.value;

  if (!q) {
    setStatus("Enter at least 1 keyword", "error");
    return;
  }

  setStatus("Scanning...", "scanning");
  scanBtn.disabled = true;

  try {
    const url = `/api/live?q=${encodeURIComponent(q)}&max=${encodeURIComponent(max)}`;
    const res = await fetch(url);
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Request failed");

    render(data.results);

    const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    if (data.results.length > 0) {
      setStatus(`${data.results.length} live streams found - Updated ${time}`, "success");
    } else {
      setStatus(`No results - Updated ${time}`, "");
    }
  } catch (e) {
    setStatus(`Error: ${e.message}`, "error");
  } finally {
    scanBtn.disabled = false;
  }
}

function applyAutoRefresh() {
  const sec = parseInt(refreshEl.value, 10);
  if (timer) clearInterval(timer);
  timer = null;

  if (sec > 0) {
    timer = setInterval(() => {
      scanOnce();
    }, sec * 1000);
  }
}

// Event listeners
scanBtn.addEventListener("click", async () => {
  await scanOnce();
  applyAutoRefresh();
});

refreshEl.addEventListener("change", applyAutoRefresh);

// Allow Enter key to trigger scan
keywordsEl.addEventListener("keydown", (e) => {
  if (e.key === "Enter") {
    scanOnce();
    applyAutoRefresh();
  }
});

// Initialize with empty state
renderEmptyState();

// Set default keywords
keywordsEl.value = "gaming, music, news";
