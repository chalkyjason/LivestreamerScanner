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
 * GET /api/live?q=keyword1,keyword2&max=25
 * - Uses search.list to find live videos
 * - Uses videos.list to get concurrentViewers (liveStreamingDetails)
 */
app.get("/api/live", async (req, res) => {
  try {
    const qRaw = (req.query.q || "").toString().trim();
    const max = Math.min(parseInt(req.query.max || "25", 10) || 25, 50);

    if (!qRaw) {
      return res.status(400).json({ error: "Missing q (keywords)" });
    }

    // Build a single query string: "kw1 OR kw2 OR kw3"
    const keywords = qRaw
      .split(",")
      .map(s => s.trim())
      .filter(Boolean);

    const query = keywords.length === 1 ? keywords[0] : keywords.map(k => `"${k}"`).join(" OR ");
    const cacheKey = `q=${query}&max=${max}`;
    const cached = getCache(cacheKey);
    if (cached) return res.json(cached);

    // 1) Search only LIVE videos
    const searchUrl =
      "https://www.googleapis.com/youtube/v3/search" +
      `?part=snippet&type=video&eventType=live&maxResults=${max}` +
      `&q=${encodeURIComponent(query)}` +
      `&key=${encodeURIComponent(API_KEY)}`;

    const searchData = await ytFetch(searchUrl);

    const videoIds = (searchData.items || [])
      .map(it => it?.id?.videoId)
      .filter(Boolean);

    if (videoIds.length === 0) {
      const empty = { query, keywords, results: [] };
      setCache(cacheKey, empty);
      return res.json(empty);
    }

    // 2) Pull liveStreamingDetails (concurrentViewers) + snippet
    // Note: concurrentViewers may be missing for some streams.
    const videosUrl =
      "https://www.googleapis.com/youtube/v3/videos" +
      `?part=snippet,liveStreamingDetails&id=${encodeURIComponent(videoIds.join(","))}` +
      `&key=${encodeURIComponent(API_KEY)}`;

    const videosData = await ytFetch(videosUrl);

    const results = (videosData.items || []).map(v => {
      const viewers = v?.liveStreamingDetails?.concurrentViewers
        ? parseInt(v.liveStreamingDetails.concurrentViewers, 10)
        : null;

      return {
        videoId: v.id,
        url: `https://www.youtube.com/watch?v=${v.id}`,
        title: v?.snippet?.title || "",
        channelTitle: v?.snippet?.channelTitle || "",
        thumbnail: v?.snippet?.thumbnails?.medium?.url || v?.snippet?.thumbnails?.default?.url || "",
        publishedAt: v?.snippet?.publishedAt || "",
        actualStartTime: v?.liveStreamingDetails?.actualStartTime || null,
        concurrentViewers: Number.isFinite(viewers) ? viewers : null
      };
    });

    // Sort: known viewer counts first, descending
    results.sort((a, b) => {
      const av = a.concurrentViewers ?? -1;
      const bv = b.concurrentViewers ?? -1;
      return bv - av;
    });

    const payload = { query, keywords, results };
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
