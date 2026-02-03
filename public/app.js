const $ = (id) => document.getElementById(id);

// ── DOM Elements ──

const keywordsEl      = $("keywords");
const maxEl           = $("max");
const minViewersEl    = $("minViewers");
const maxViewersEl    = $("maxViewers");
const refreshEl       = $("refreshSec");
const sortOrderEl     = $("sortOrder");
const regionEl        = $("region");
const langEl          = $("lang");
const topicEl         = $("topic");
const safeSearchEl    = $("safeSearch");
const scanBtn         = $("scanBtn");
const stopBtn         = $("stopBtn");
const resultsEl       = $("results");
const statusEl        = $("status");
const blockedSection  = $("blockedSection");
const blockedList     = $("blockedList");
const clearBlockedBtn = $("clearBlockedBtn");
const viewToggle      = $("viewToggle");
const quotaFill       = $("quotaFill");
const quotaText       = $("quotaText");
const quotaBadge      = $("quotaBadge");
const presetSelect    = $("presetSelect");
const savePresetBtn   = $("savePresetBtn");
const deletePresetBtn = $("deletePresetBtn");
const filtersToggle   = $("filtersToggle");
const filtersPanel    = $("filtersPanel");
const favoritesToggle = $("favoritesToggle");
const favoritesPanel  = $("favoritesPanel");
const favoritesOverlay = $("favoritesOverlay");
const favoritesClose  = $("favoritesClose");
const favoritesList   = $("favoritesList");
const favCountEl      = $("favCount");

let timer = null;
let activeTab = "live";
let previousViewerCounts = {};

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
      scanOnce();
    });
    tag.appendChild(removeBtn);

    blockedList.appendChild(tag);
  }
}

// ── Favorites (persisted in localStorage) ──

function getFavorites() {
  try {
    return JSON.parse(localStorage.getItem("favorites") || "[]");
  } catch {
    return [];
  }
}

function saveFavorites(list) {
  localStorage.setItem("favorites", JSON.stringify(list));
  updateFavCount();
}

function isFavorite(channelTitle) {
  return getFavorites().some(f => f.channelTitle.toLowerCase() === channelTitle.toLowerCase());
}

function addFavorite(stream) {
  const favs = getFavorites();
  if (favs.some(f => f.channelTitle.toLowerCase() === stream.channelTitle.toLowerCase())) return;
  favs.push({
    channelTitle: stream.channelTitle,
    thumbnail: stream.thumbnail,
    url: stream.url,
    title: stream.title,
    addedAt: Date.now(),
  });
  saveFavorites(favs);
}

function removeFavorite(channelTitle) {
  const favs = getFavorites().filter(
    f => f.channelTitle.toLowerCase() !== channelTitle.toLowerCase()
  );
  saveFavorites(favs);
}

function updateFavCount() {
  const count = getFavorites().length;
  if (count > 0) {
    favCountEl.textContent = count;
    favCountEl.style.display = "";
  } else {
    favCountEl.style.display = "none";
  }
}

function renderFavoritesList() {
  const favs = getFavorites();

  if (favs.length === 0) {
    favoritesList.innerHTML = `
      <div class="favorites-empty">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="40" height="40">
          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"></path>
        </svg>
        <p>No favorites yet</p>
        <span>Click the heart on any stream to save it here</span>
      </div>
    `;
    return;
  }

  favoritesList.innerHTML = "";

  for (const fav of favs) {
    const card = document.createElement("div");
    card.className = "fav-card";
    card.innerHTML = `
      <div class="fav-card-thumb">
        <img src="${escapeHtml(fav.thumbnail)}" alt="" loading="lazy" />
      </div>
      <div class="fav-card-info">
        <p class="fav-card-channel">${escapeHtml(fav.channelTitle)}</p>
        <p class="fav-card-title">${escapeHtml(fav.title)}</p>
      </div>
    `;

    const removeBtn = document.createElement("button");
    removeBtn.className = "fav-card-remove";
    removeBtn.title = "Remove from favorites";
    removeBtn.textContent = "\u00d7";
    removeBtn.addEventListener("click", () => {
      removeFavorite(fav.channelTitle);
      renderFavoritesList();
      // Update heart buttons in visible cards
      document.querySelectorAll(".fav-btn").forEach(btn => {
        if (btn.dataset.channel && btn.dataset.channel.toLowerCase() === fav.channelTitle.toLowerCase()) {
          btn.classList.remove("is-fav");
          btn.querySelector("svg path").removeAttribute("fill");
        }
      });
    });
    card.appendChild(removeBtn);
    favoritesList.appendChild(card);
  }
}

function openFavoritesPanel() {
  renderFavoritesList();
  favoritesOverlay.style.display = "";
  favoritesPanel.classList.add("open");
}

function closeFavoritesPanel() {
  favoritesPanel.classList.remove("open");
  favoritesOverlay.style.display = "none";
}

favoritesToggle.addEventListener("click", openFavoritesPanel);
favoritesClose.addEventListener("click", closeFavoritesPanel);
favoritesOverlay.addEventListener("click", closeFavoritesPanel);

// ── Filters Toggle ──

filtersToggle.addEventListener("click", () => {
  const isOpen = filtersPanel.style.display !== "none";
  filtersPanel.style.display = isOpen ? "none" : "";
  filtersToggle.classList.toggle("active", !isOpen);
  localStorage.setItem("filtersOpen", !isOpen ? "1" : "0");
});

// ── Presets (persisted in localStorage) ──

function getPresets() {
  try {
    return JSON.parse(localStorage.getItem("searchPresets") || "{}");
  } catch {
    return {};
  }
}

function savePresets(presets) {
  localStorage.setItem("searchPresets", JSON.stringify(presets));
  renderPresetOptions();
}

function getCurrentSettings() {
  return {
    keywords: keywordsEl.value,
    max: maxEl.value,
    minViewers: minViewersEl.value,
    maxViewers: maxViewersEl.value,
    sortOrder: sortOrderEl.value,
    region: regionEl.value,
    lang: langEl.value,
    topic: topicEl.value,
    safeSearch: safeSearchEl.value,
    refreshSec: refreshEl.value,
  };
}

function applySettings(settings) {
  if (settings.keywords !== undefined) keywordsEl.value = settings.keywords;
  if (settings.max !== undefined) maxEl.value = settings.max;
  if (settings.minViewers !== undefined) minViewersEl.value = settings.minViewers;
  if (settings.maxViewers !== undefined) maxViewersEl.value = settings.maxViewers;
  if (settings.sortOrder !== undefined) sortOrderEl.value = settings.sortOrder;
  if (settings.region !== undefined) regionEl.value = settings.region;
  if (settings.lang !== undefined) langEl.value = settings.lang;
  if (settings.topic !== undefined) topicEl.value = settings.topic;
  if (settings.safeSearch !== undefined) safeSearchEl.value = settings.safeSearch;
  if (settings.refreshSec !== undefined) refreshEl.value = settings.refreshSec;
}

function renderPresetOptions() {
  const presets = getPresets();
  const names = Object.keys(presets).sort();

  // Clear all except the first placeholder option
  while (presetSelect.options.length > 1) {
    presetSelect.remove(1);
  }

  for (const name of names) {
    const opt = document.createElement("option");
    opt.value = name;
    opt.textContent = name;
    presetSelect.appendChild(opt);
  }

  presetSelect.value = "";
}

savePresetBtn.addEventListener("click", () => {
  const name = prompt("Preset name:");
  if (!name || !name.trim()) return;

  const presets = getPresets();
  presets[name.trim()] = getCurrentSettings();
  savePresets(presets);
  presetSelect.value = name.trim();
});

deletePresetBtn.addEventListener("click", () => {
  const name = presetSelect.value;
  if (!name) return;

  const presets = getPresets();
  delete presets[name];
  savePresets(presets);
});

presetSelect.addEventListener("change", () => {
  const name = presetSelect.value;
  if (!name) return;

  const presets = getPresets();
  if (presets[name]) {
    applySettings(presets[name]);
    scanOnce();
    applyAutoRefresh();
  }
});

// ── Tabs ──

function initTabs() {
  const tabBtns = document.querySelectorAll(".tab-btn");
  tabBtns.forEach(btn => {
    btn.addEventListener("click", () => {
      tabBtns.forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
      activeTab = btn.dataset.tab;
      scanOnce();
    });
  });
}

// ── Grid / List Toggle ──

function initViewToggle() {
  const savedView = localStorage.getItem("viewMode") || "list";
  if (savedView === "grid") {
    resultsEl.classList.add("grid-view");
  }
  updateViewToggleIcon();

  viewToggle.addEventListener("click", () => {
    resultsEl.classList.toggle("grid-view");
    const isGrid = resultsEl.classList.contains("grid-view");
    localStorage.setItem("viewMode", isGrid ? "grid" : "list");
    updateViewToggleIcon();
  });
}

function updateViewToggleIcon() {
  const isGrid = resultsEl.classList.contains("grid-view");
  const icon = $("viewToggleIcon");
  if (isGrid) {
    // Show list icon when in grid mode (click to switch to list)
    icon.innerHTML = `
      <line x1="3" y1="6" x2="21" y2="6"></line>
      <line x1="3" y1="12" x2="21" y2="12"></line>
      <line x1="3" y1="18" x2="21" y2="18"></line>
    `;
  } else {
    // Show grid icon when in list mode (click to switch to grid)
    icon.innerHTML = `
      <rect x="3" y="3" width="7" height="7"></rect>
      <rect x="14" y="3" width="7" height="7"></rect>
      <rect x="3" y="14" width="7" height="7"></rect>
      <rect x="14" y="14" width="7" height="7"></rect>
    `;
  }
}

// ── Quota Display ──

async function updateQuota() {
  try {
    const res = await fetch("/api/quota");
    const data = await res.json();
    const pct = Math.min((data.used / data.limit) * 100, 100);
    quotaFill.style.width = pct + "%";
    quotaText.textContent = `${data.used.toLocaleString()} / ${data.limit.toLocaleString()}`;

    quotaBadge.classList.remove("warning", "danger");
    if (pct >= 90) {
      quotaBadge.classList.add("danger");
    } else if (pct >= 70) {
      quotaBadge.classList.add("warning");
    }
  } catch {
    // Silently ignore quota fetch errors
  }
}

// ── Trending (viewer count delta tracking) ──

function getTrend(videoId, currentViewers) {
  if (currentViewers === null) return null;
  const prev = previousViewerCounts[videoId];
  if (prev === undefined || prev === null) return null;

  const delta = currentViewers - prev;
  if (delta > 0) return { direction: "up", delta };
  if (delta < 0) return { direction: "down", delta };
  return { direction: "stable", delta: 0 };
}

function updatePreviousViewerCounts(results) {
  const newCounts = {};
  for (const r of results) {
    if (r.concurrentViewers !== null) {
      newCounts[r.videoId] = r.concurrentViewers;
    }
  }
  previousViewerCounts = newCounts;
}

function trendHtml(trend) {
  if (!trend) return "";
  const cls = `trend-${trend.direction}`;
  const arrow = trend.direction === "up" ? "\u25b2" : trend.direction === "down" ? "\u25bc" : "\u25cf";
  const sign = trend.delta > 0 ? "+" : "";
  return `<span class="${cls}">${arrow} ${sign}${trend.delta.toLocaleString()}</span>`;
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

function formatScheduledTime(isoString) {
  if (!isoString) return "Not scheduled";
  const date = new Date(isoString);
  const now = new Date();
  const diffMs = date - now;

  if (diffMs < 0) return "Starting soon";

  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 60) return `In ${diffMins}m`;
  if (diffHours < 24) return `In ${diffHours}h ${diffMins % 60}m`;
  if (diffDays < 7) return `In ${diffDays}d ${diffHours % 24}h`;
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

function escapeHtml(s) {
  return String(s)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function copyToClipboard(text) {
  navigator.clipboard.writeText(text).catch(() => {
    // Fallback for older browsers
    const ta = document.createElement("textarea");
    ta.value = text;
    ta.style.position = "fixed";
    ta.style.opacity = "0";
    document.body.appendChild(ta);
    ta.select();
    document.execCommand("copy");
    document.body.removeChild(ta);
  });
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
  const label = activeTab === "upcoming" ? "Upcoming Streams" : "Live Streams";
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
      <h3>No ${label} Found</h3>
      <p>Try different keywords or check back later</p>
    </div>
  `;
}

function renderLiveCard(r) {
  const viewers = formatViewers(r.concurrentViewers);
  const hasViewers = viewers !== null;
  const trend = getTrend(r.videoId, r.concurrentViewers);

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
        ${trendHtml(trend)}
      </div>
    </div>
  `;

  addCardActions(card, r);
  return card;
}

function renderUpcomingCard(r) {
  const card = document.createElement("div");
  card.className = "stream-card";
  card.innerHTML = `
    <div class="thumbnail-wrapper">
      <img class="thumbnail" src="${escapeHtml(r.thumbnail)}" alt="" loading="lazy" />
      <div class="upcoming-badge">Upcoming</div>
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
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
            <line x1="16" y1="2" x2="16" y2="6"></line>
            <line x1="8" y1="2" x2="8" y2="6"></line>
            <line x1="3" y1="10" x2="21" y2="10"></line>
          </svg>
          <span>${formatScheduledTime(r.scheduledStartTime)}</span>
        </div>
      </div>
    </div>
    <div class="stream-stats">
      <div class="viewer-count no-data">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
          <line x1="16" y1="2" x2="16" y2="6"></line>
          <line x1="8" y1="2" x2="8" y2="6"></line>
          <line x1="3" y1="10" x2="21" y2="10"></line>
        </svg>
        <span class="viewer-number">${formatScheduledTime(r.scheduledStartTime)}</span>
        <span class="viewer-label">scheduled</span>
      </div>
    </div>
  `;

  addCardActions(card, r);
  return card;
}

function addCardActions(card, r) {
  const heartPath = "M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z";
  const isFav = isFavorite(r.channelTitle);

  // Action buttons container
  const actionsDiv = document.createElement("div");
  actionsDiv.className = "stream-actions";
  actionsDiv.style.cssText = "display:flex;flex-direction:column;align-items:center;justify-content:center;gap:6px;";

  // Copy link button
  const copyBtn = document.createElement("button");
  copyBtn.className = "copy-btn";
  copyBtn.title = "Copy link";
  copyBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14">
    <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
    <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
  </svg>`;
  copyBtn.addEventListener("click", (e) => {
    e.stopPropagation();
    copyToClipboard(r.url);
    copyBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14">
      <polyline points="20 6 9 17 4 12"></polyline>
    </svg>`;
    setTimeout(() => {
      copyBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14">
        <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
        <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
      </svg>`;
    }, 1500);
  });

  // Favorite button
  const favBtn = document.createElement("button");
  favBtn.className = "fav-btn" + (isFav ? " is-fav" : "");
  favBtn.dataset.channel = r.channelTitle;
  favBtn.title = isFav ? "Remove from favorites" : "Add to favorites";
  favBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="${isFav ? 'currentColor' : 'none'}" stroke="currentColor" stroke-width="2"><path d="${heartPath}"></path></svg>`;
  favBtn.addEventListener("click", (e) => {
    e.stopPropagation();
    const nowFav = isFavorite(r.channelTitle);
    if (nowFav) {
      removeFavorite(r.channelTitle);
      favBtn.classList.remove("is-fav");
      favBtn.querySelector("svg").setAttribute("fill", "none");
      favBtn.title = "Add to favorites";
    } else {
      addFavorite(r);
      favBtn.classList.add("is-fav");
      favBtn.querySelector("svg").setAttribute("fill", "currentColor");
      favBtn.title = "Remove from favorites";
    }
  });

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

  actionsDiv.appendChild(copyBtn);
  actionsDiv.appendChild(favBtn);
  actionsDiv.appendChild(blockBtn);

  // Insert before stats div
  const statsDiv = card.querySelector(".stream-stats");
  card.insertBefore(actionsDiv, statsDiv);
}

function render(results) {
  if (!results.length) {
    renderNoResults();
    return;
  }

  resultsEl.innerHTML = "";

  for (const r of results) {
    const card = activeTab === "upcoming"
      ? renderUpcomingCard(r)
      : renderLiveCard(r);
    resultsEl.appendChild(card);
  }

  // Update viewer count history for trending
  if (activeTab === "live") {
    updatePreviousViewerCounts(results);
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
      order: sortOrderEl.value,
      safeSearch: safeSearchEl.value,
      eventType: activeTab === "upcoming" ? "upcoming" : "live",
    });

    if (blocked.length > 0) params.set("blocked", blocked.join(","));
    if (regionEl.value) params.set("region", regionEl.value);
    if (langEl.value) params.set("lang", langEl.value);
    if (topicEl.value) params.set("topic", topicEl.value);

    const res = await fetch(`/api/live?${params}`);
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Request failed");

    render(data.results);
    updateQuota();

    const time = new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    const filterNote = data.filtered > 0 ? ` (${data.filtered} filtered)` : "";
    const label = activeTab === "upcoming" ? "upcoming streams" : "live streams";

    if (data.results.length > 0) {
      setStatus(`${data.results.length} ${label} found${filterNote} - ${time}`, "success");
    } else {
      setStatus(`No results${filterNote} - ${time}`, "");
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
    stopBtn.style.display = "";
  } else {
    stopBtn.style.display = "none";
  }
}

// ── Event Listeners ──

scanBtn.addEventListener("click", async () => {
  await scanOnce();
  applyAutoRefresh();
});

stopBtn.addEventListener("click", () => {
  if (timer) clearInterval(timer);
  timer = null;
  stopBtn.style.display = "none";
  setStatus("Stopped", "");
});

refreshEl.addEventListener("change", applyAutoRefresh);

clearBlockedBtn.addEventListener("click", () => {
  clearAllBlocked();
  scanOnce();
});

keywordsEl.addEventListener("keydown", (e) => {
  if (e.key === "Enter") {
    scanOnce();
    applyAutoRefresh();
  }
});

// ── Initialize ──

initTabs();
initViewToggle();
renderEmptyState();
renderBlockedChannels();
renderPresetOptions();
updateQuota();
updateFavCount();
keywordsEl.value = "gaming, music, news";

// Restore filters panel state
if (localStorage.getItem("filtersOpen") === "1") {
  filtersPanel.style.display = "";
  filtersToggle.classList.add("active");
}
