import express from "express";
import dotenv from "dotenv";

dotenv.config();

const app = express();
const PORT = process.env.PORT || 3000;

const API_KEY = process.env.YOUTUBE_API_KEY;
if (!API_KEY) {
  console.error("Missing YOUTUBE_API_KEY. Create a .env file (see .env.example).");
  process.exit(1);
}

app.use(express.static("public"));

/**
 * Simple in-memory cache to reduce API calls on rapid refreshes.
 * Keyed by query params; TTL short since live data changes fast.
 */
const cache = new Map();
const CACHE_TTL_MS = 10_000;

function getCache(key) {
  const hit = cache.get(key);
  if (!hit) return null;
  if (Date.now() - hit.ts > CACHE_TTL_MS) {
    cache.delete(key);
    return null;
  }
  return hit.value;
}
function setCache(key, value) {
  cache.set(key, { ts: Date.now(), value });
}

async function ytFetch(url) {
  const res = await fetch(url);
  const text = await res.text();
  if (!res.ok) {
    throw new Error(`YouTube API error ${res.status}: ${text}`);
  }
  return JSON.parse(text);
}

/**
 * GET /api/live?q=keyword1,keyword2&max=25&minViewers=0&maxViewers=0&blocked=ch1,ch2
 * - Uses search.list with order=viewCount for best live stream discovery
 * - Uses videos.list to get concurrentViewers (liveStreamingDetails)
 * - Filters by min/max viewer count and removes blocked channels
 */
app.get("/api/live", async (req, res) => {
  try {
    const qRaw = (req.query.q || "").toString().trim();
    const max = Math.min(parseInt(req.query.max || "25", 10) || 25, 50);
    const minViewers = parseInt(req.query.minViewers || "0", 10) || 0;
    const maxViewers = parseInt(req.query.maxViewers || "0", 10) || 0;

    // Parse blocked channels list (comma-separated, case-insensitive)
    const blockedRaw = (req.query.blocked || "").toString().trim();
    const blockedChannels = blockedRaw
      ? blockedRaw.split(",").map(s => s.trim().toLowerCase()).filter(Boolean)
      : [];

    if (!qRaw) {
      return res.status(400).json({ error: "Missing q (keywords)" });
    }

    // Build optimized query: quote multi-word phrases, join with OR
    const keywords = qRaw
      .split(",")
      .map(s => s.trim())
      .filter(Boolean);

    const query = keywords.length === 1
      ? keywords[0]
      : keywords.map(k => k.includes(" ") ? `"${k}"` : k).join(" OR ");

    const cacheKey = `q=${query}&max=${max}&min=${minViewers}&maxV=${maxViewers}&bl=${blockedChannels.sort().join(",")}`;
    const cached = getCache(cacheKey);
    if (cached) return res.json(cached);

    // Fetch extra results to account for filtering
    const fetchMax = Math.min(max + blockedChannels.length * 2 + 10, 50);

    // 1) Search LIVE videos, ordered by viewCount for best discovery
    const searchUrl =
      "https://www.googleapis.com/youtube/v3/search" +
      `?part=snippet&type=video&eventType=live&maxResults=${fetchMax}` +
      `&order=viewCount` +
      `&safeSearch=none` +
      `&q=${encodeURIComponent(query)}` +
      `&key=${encodeURIComponent(API_KEY)}`;

    const searchData = await ytFetch(searchUrl);

    const videoIds = (searchData.items || [])
      .map(it => it?.id?.videoId)
      .filter(Boolean);

    if (videoIds.length === 0) {
      const empty = { query, keywords, results: [], filtered: 0 };
      setCache(cacheKey, empty);
      return res.json(empty);
    }

    // 2) Pull liveStreamingDetails (concurrentViewers) + snippet
    const videosUrl =
      "https://www.googleapis.com/youtube/v3/videos" +
      `?part=snippet,liveStreamingDetails&id=${encodeURIComponent(videoIds.join(","))}` +
      `&key=${encodeURIComponent(API_KEY)}`;

    const videosData = await ytFetch(videosUrl);

    let results = (videosData.items || []).map(v => {
      const viewers = v?.liveStreamingDetails?.concurrentViewers
        ? parseInt(v.liveStreamingDetails.concurrentViewers, 10)
        : null;

      return {
        videoId: v.id,
        url: `https://www.youtube.com/watch?v=${v.id}`,
        title: v?.snippet?.title || "",
        channelTitle: v?.snippet?.channelTitle || "",
        channelId: v?.snippet?.channelId || "",
        thumbnail: v?.snippet?.thumbnails?.medium?.url || v?.snippet?.thumbnails?.default?.url || "",
        publishedAt: v?.snippet?.publishedAt || "",
        actualStartTime: v?.liveStreamingDetails?.actualStartTime || null,
        concurrentViewers: Number.isFinite(viewers) ? viewers : null
      };
    });

    const totalBeforeFilter = results.length;

    // 3) Remove blocked channels
    if (blockedChannels.length > 0) {
      results = results.filter(r =>
        !blockedChannels.includes(r.channelTitle.toLowerCase())
      );
    }

    // 4) Apply viewer count filters
    if (minViewers > 0) {
      results = results.filter(r =>
        r.concurrentViewers !== null && r.concurrentViewers >= minViewers
      );
    }
    if (maxViewers > 0) {
      results = results.filter(r =>
        r.concurrentViewers !== null && r.concurrentViewers <= maxViewers
      );
    }

    // Sort: known viewer counts first, descending
    results.sort((a, b) => {
      const av = a.concurrentViewers ?? -1;
      const bv = b.concurrentViewers ?? -1;
      return bv - av;
    });

    // Trim to requested max
    results = results.slice(0, max);

    const filtered = totalBeforeFilter - results.length;
    const payload = { query, keywords, results, filtered };
    setCache(cacheKey, payload);
    res.json(payload);
  } catch (err) {
    console.error(err);
    res.status(500).json({ error: err.message || "Server error" });
  }
});

app.listen(PORT, () => {
  console.log(`Stream Scanner running: http://localhost:${PORT}`);
});
