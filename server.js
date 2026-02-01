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
app.use(express.json());

/**
 * Simple in-memory cache to reduce API calls on rapid refreshes.
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

/** Track API quota usage per session */
let quotaUsed = 0;
const QUOTA_COSTS = { search: 100, videos: 1, channels: 1 };

async function ytFetch(url, quotaType = "search") {
  quotaUsed += QUOTA_COSTS[quotaType] || 0;
  const res = await fetch(url);
  const text = await res.text();
  if (!res.ok) {
    throw new Error(`YouTube API error ${res.status}: ${text}`);
  }
  return JSON.parse(text);
}

/** GET /api/quota - Return current quota usage */
app.get("/api/quota", (_req, res) => {
  res.json({ used: quotaUsed, limit: 10000 });
});

/**
 * GET /api/live - Search for live (or upcoming/completed) streams
 * Params: q, max, minViewers, maxViewers, blocked, region, lang, topic, order, safeSearch, eventType
 */
app.get("/api/live", async (req, res) => {
  try {
    const qRaw = (req.query.q || "").toString().trim();
    const max = Math.min(parseInt(req.query.max || "25", 10) || 25, 50);
    const minViewers = parseInt(req.query.minViewers || "0", 10) || 0;
    const maxViewers = parseInt(req.query.maxViewers || "0", 10) || 0;

    // New filter params
    const region = (req.query.region || "").toString().trim();
    const lang = (req.query.lang || "").toString().trim();
    const topic = (req.query.topic || "").toString().trim();
    const order = (req.query.order || "viewCount").toString().trim();
    const safeSearch = (req.query.safeSearch || "none").toString().trim();
    const eventType = (req.query.eventType || "live").toString().trim();

    // Parse blocked channels list
    const blockedRaw = (req.query.blocked || "").toString().trim();
    const blockedChannels = blockedRaw
      ? blockedRaw.split(",").map(s => s.trim().toLowerCase()).filter(Boolean)
      : [];

    if (!qRaw) {
      return res.status(400).json({ error: "Missing q (keywords)" });
    }

    // Build optimized query
    const keywords = qRaw
      .split(",")
      .map(s => s.trim())
      .filter(Boolean);

    const query = keywords.length === 1
      ? keywords[0]
      : keywords.map(k => k.includes(" ") ? `"${k}"` : k).join(" OR ");

    const cacheKey = `q=${query}&max=${max}&min=${minViewers}&maxV=${maxViewers}&bl=${blockedChannels.sort().join(",")}&r=${region}&l=${lang}&t=${topic}&o=${order}&ss=${safeSearch}&et=${eventType}`;
    const cached = getCache(cacheKey);
    if (cached) return res.json(cached);

    // Fetch extra results to account for filtering
    const fetchMax = Math.min(max + blockedChannels.length * 2 + 10, 50);

    // Build search URL with all params
    let searchUrl =
      "https://www.googleapis.com/youtube/v3/search" +
      `?part=snippet&type=video&eventType=${encodeURIComponent(eventType)}` +
      `&maxResults=${fetchMax}` +
      `&order=${encodeURIComponent(order)}` +
      `&safeSearch=${encodeURIComponent(safeSearch)}` +
      `&q=${encodeURIComponent(query)}` +
      `&key=${encodeURIComponent(API_KEY)}`;

    if (region) searchUrl += `&regionCode=${encodeURIComponent(region)}`;
    if (lang) searchUrl += `&relevanceLanguage=${encodeURIComponent(lang)}`;
    if (topic) searchUrl += `&topicId=${encodeURIComponent(topic)}`;

    const searchData = await ytFetch(searchUrl, "search");

    const videoIds = (searchData.items || [])
      .map(it => it?.id?.videoId)
      .filter(Boolean);

    if (videoIds.length === 0) {
      const empty = { query, keywords, results: [], filtered: 0, quotaUsed };
      setCache(cacheKey, empty);
      return res.json(empty);
    }

    // Pull liveStreamingDetails + snippet + description
    const videosUrl =
      "https://www.googleapis.com/youtube/v3/videos" +
      `?part=snippet,liveStreamingDetails` +
      `&id=${encodeURIComponent(videoIds.join(","))}` +
      `&key=${encodeURIComponent(API_KEY)}`;

    const videosData = await ytFetch(videosUrl, "videos");

    let results = (videosData.items || []).map(v => {
      const viewers = v?.liveStreamingDetails?.concurrentViewers
        ? parseInt(v.liveStreamingDetails.concurrentViewers, 10)
        : null;

      return {
        videoId: v.id,
        url: `https://www.youtube.com/watch?v=${v.id}`,
        title: v?.snippet?.title || "",
        description: v?.snippet?.description || "",
        channelTitle: v?.snippet?.channelTitle || "",
        channelId: v?.snippet?.channelId || "",
        thumbnail: v?.snippet?.thumbnails?.medium?.url || v?.snippet?.thumbnails?.default?.url || "",
        publishedAt: v?.snippet?.publishedAt || "",
        actualStartTime: v?.liveStreamingDetails?.actualStartTime || null,
        scheduledStartTime: v?.liveStreamingDetails?.scheduledStartTime || null,
        concurrentViewers: Number.isFinite(viewers) ? viewers : null
      };
    });

    const totalBeforeFilter = results.length;

    // Remove blocked channels
    if (blockedChannels.length > 0) {
      results = results.filter(r =>
        !blockedChannels.includes(r.channelTitle.toLowerCase())
      );
    }

    // Apply viewer count filters (only for live, not upcoming)
    if (eventType === "live") {
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
    }

    // Sort
    if (eventType === "upcoming") {
      // Sort upcoming by scheduled start time (soonest first)
      results.sort((a, b) => {
        const at = a.scheduledStartTime ? new Date(a.scheduledStartTime).getTime() : Infinity;
        const bt = b.scheduledStartTime ? new Date(b.scheduledStartTime).getTime() : Infinity;
        return at - bt;
      });
    } else {
      // Sort live by viewer count descending
      results.sort((a, b) => {
        const av = a.concurrentViewers ?? -1;
        const bv = b.concurrentViewers ?? -1;
        return bv - av;
      });
    }

    results = results.slice(0, max);

    const filtered = totalBeforeFilter - results.length;
    const payload = { query, keywords, results, filtered, quotaUsed };
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
