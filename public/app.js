const $ = (id) => document.getElementById(id);

const keywordsEl = $("keywords");
const maxEl = $("max");
const refreshEl = $("refreshSec");
const scanBtn = $("scanBtn");
const resultsEl = $("results");
const statusEl = $("status");

let timer = null;

function setStatus(msg) {
  statusEl.textContent = msg;
}

function formatViewers(n) {
  if (n === null || n === undefined) return "—";
  return n.toLocaleString();
}

function render(results) {
  resultsEl.innerHTML = "";
  if (!results.length) {
    resultsEl.innerHTML = `<p class="meta">No live results found.</p>`;
    return;
  }

  for (const r of results) {
    const viewers = r.concurrentViewers;
    const viewersLabel = viewers === null ? "Viewers: —" : `Viewers: ${formatViewers(viewers)}`;

    const card = document.createElement("div");
    card.className = "card";
    card.innerHTML = `
      <img class="thumb" src="${r.thumbnail}" alt="thumbnail" />
      <div>
        <p class="title"><a href="${r.url}" target="_blank" rel="noreferrer">${escapeHtml(r.title)}</a></p>
        <p class="meta">Channel: ${escapeHtml(r.channelTitle)}</p>
        <p class="meta">Started: ${r.actualStartTime ? new Date(r.actualStartTime).toLocaleString() : "Unknown"}</p>
      </div>
      <div class="viewers">
        <div class="badge">${viewersLabel}</div>
      </div>
    `;
    resultsEl.appendChild(card);
  }
}

function escapeHtml(s) {
  return String(s)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

async function scanOnce() {
  const q = keywordsEl.value.trim();
  const max = maxEl.value;

  if (!q) {
    setStatus("Enter at least 1 keyword.");
    return;
  }

  setStatus("Scanning…");
  scanBtn.disabled = true;

  try {
    const url = `/api/live?q=${encodeURIComponent(q)}&max=${encodeURIComponent(max)}`;
    const res = await fetch(url);
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Request failed");

    render(data.results);
    setStatus(`Showing ${data.results.length} live result(s). Updated ${new Date().toLocaleTimeString()}.`);
  } catch (e) {
    setStatus(`Error: ${e.message}`);
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

scanBtn.addEventListener("click", async () => {
  await scanOnce();
  applyAutoRefresh();
});

refreshEl.addEventListener("change", applyAutoRefresh);

// Nice defaults
keywordsEl.value = "police, scanner, pursuit";
