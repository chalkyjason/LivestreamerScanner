const $ = (id) => document.getElementById(id);

const keywordsEl = $("keywords");
const maxEl = $("max");
const minViewersEl = $("minViewers");
const maxViewersEl = $("maxViewers");
const refreshEl = $("refreshSec");
const scanBtn = $("scanBtn");
const resultsEl = $("results");
const statusEl = $("status");
const blockedSection = $("blockedSection");
const blockedList = $("blockedList");
const clearBlockedBtn = $("clearBlockedBtn");

let timer = null;

// ── Blocked Channels (persisted in localStorage) ──

function getBlockedChannels() {
  try {
    return JSON.parse(localStorage.getItem("blockedChannels") || "[]");
  } catch {
    return [];
  }
}

function saveBlockedChannels(list) {
  localStorage.setItem("blockedChannels", JSON.stringify(list));
  renderBlockedChannels();
}

function blockChannel(channelTitle) {
  const blocked = getBlockedChannels();
  const normalized = channelTitle.trim();
  if (!normalized) return;
  if (blocked.some(b => b.toLowerCase() === normalized.toLowerCase())) return;
  blocked.push(normalized);
  saveBlockedChannels(blocked);
}

function unblockChannel(channelTitle) {
  const blocked = getBlockedChannels().filter(
    b => b.toLowerCase() !== channelTitle.toLowerCase()
  );
  saveBlockedChannels(blocked);
}

function clearAllBlocked() {
  saveBlockedChannels([]);
}

function renderBlockedChannels() {
  const blocked = getBlockedChannels();
  if (blocked.length === 0) {
    blockedSection.style.display = "none";
    return;
  }

  blockedSection.style.display = "";
  blockedList.textContent = "";

  for (const name of blocked) {
    const tag = document.createElement("span");
    tag.className = "blocked-tag";

    const text = document.createElement("span");
    text.textContent = name;
    tag.appendChild(text);

    const removeBtn = document.createElement("button");
    removeBtn.className = "blocked-tag-remove";
    removeBtn.textContent = "\u00d7";
    removeBtn.title = `Unblock ${name}`;
    removeBtn.addEventListener("click", () => {
      unblockChannel(name);
      scanOnce(); // Re-scan to show the unblocked channel
    });
    tag.appendChild(removeBtn);

    blockedList.appendChild(tag);
  }
}

// ── Utility ──

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

// ── Rendering ──

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
          <span class="viewer-number">${hasViewers ? viewers : '\u2014'}</span>
          <span class="viewer-label">viewers</span>
        </div>
      </div>
    `;

    // Block channel button
    const blockBtn = document.createElement("button");
    blockBtn.className = "block-btn";
    blockBtn.title = `Block ${r.channelTitle}`;
    blockBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <circle cx="12" cy="12" r="10"></circle>
      <line x1="4.93" y1="4.93" x2="19.07" y2="19.07"></line>
    </svg>`;
    blockBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      blockChannel(r.channelTitle);
      card.style.opacity = "0";
      card.style.transform = "translateX(20px)";
      card.style.transition = "all 0.3s ease";
      setTimeout(() => card.remove(), 300);
      setStatus(`Blocked ${r.channelTitle}`, "");
    });

    // Insert block button before the stats div
    const statsDiv = card.querySelector(".stream-stats");
    card.insertBefore(blockBtn, statsDiv);

    resultsEl.appendChild(card);
  }
}

// ── Scanning ──

async function scanOnce() {
  const q = keywordsEl.value.trim();
  const max = maxEl.value;
  const minV = parseInt(minViewersEl.value, 10) || 0;
  const maxV = parseInt(maxViewersEl.value, 10) || 0;

  if (!q) {
    setStatus("Enter at least 1 keyword", "error");
    return;
  }

  setStatus("Scanning...", "scanning");
  scanBtn.disabled = true;

  try {
    const blocked = getBlockedChannels();
    const params = new URLSearchParams({
      q,
      max,
      minViewers: minV.toString(),
      maxViewers: maxV.toString(),
    });
    if (blocked.length > 0) {
      params.set("blocked", blocked.join(","));
    }

    const res = await fetch(`/api/live?${params}`);
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Request failed");

    render(data.results);

    const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    const filterNote = data.filtered > 0 ? ` (${data.filtered} filtered)` : "";
    if (data.results.length > 0) {
      setStatus(`${data.results.length} live streams found${filterNote} - Updated ${time}`, "success");
    } else {
      setStatus(`No results${filterNote} - Updated ${time}`, "");
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

// ── Event Listeners ──

scanBtn.addEventListener("click", async () => {
  await scanOnce();
  applyAutoRefresh();
});

refreshEl.addEventListener("change", applyAutoRefresh);

clearBlockedBtn.addEventListener("click", () => {
  clearAllBlocked();
  scanOnce();
});

// Allow Enter key to trigger scan
keywordsEl.addEventListener("keydown", (e) => {
  if (e.key === "Enter") {
    scanOnce();
    applyAutoRefresh();
  }
});

// ── Initialize ──

renderEmptyState();
renderBlockedChannels();
keywordsEl.value = "gaming, music, news";
