namespace StreamScanner.Maui.Services;

/// <summary>
/// AI-powered search enhancement service.
/// This service provides intelligent search suggestions and query optimization.
///
/// To enable full AI features, you would integrate with:
/// - Microsoft Semantic Kernel (Microsoft.SemanticKernel NuGet package)
/// - Azure OpenAI or OpenAI API
/// - Microsoft Agent Framework (when GA in Q1 2026)
///
/// For now, this provides rule-based intelligence that can be enhanced later.
/// </summary>
public interface IAISearchService
{
    /// <summary>
    /// Get search suggestions based on user input
    /// </summary>
    Task<List<string>> GetSearchSuggestionsAsync(string partialQuery);

    /// <summary>
    /// Optimize a search query for better YouTube results
    /// </summary>
    Task<string> OptimizeQueryAsync(string userQuery);

    /// <summary>
    /// Get related search terms
    /// </summary>
    Task<List<string>> GetRelatedTermsAsync(string query);

    /// <summary>
    /// Analyze search results and provide insights
    /// </summary>
    Task<SearchInsights> AnalyzeResultsAsync(List<StreamScanner.Maui.Models.LiveStream> streams);
}

public class AISearchService : IAISearchService
{
    // Common gaming/streaming related terms for suggestions
    private static readonly Dictionary<string, List<string>> RelatedTerms = new()
    {
        ["gaming"] = new() { "gameplay", "lets play", "walkthrough", "speedrun", "esports" },
        ["music"] = new() { "live concert", "dj set", "acoustic", "karaoke", "radio" },
        ["news"] = new() { "breaking news", "live news", "politics", "world news", "local news" },
        ["sports"] = new() { "live sports", "football", "basketball", "soccer", "baseball" },
        ["podcast"] = new() { "talk show", "interview", "discussion", "commentary", "debate" },
        ["asmr"] = new() { "relaxing", "sleep", "triggers", "whisper", "sounds" },
        ["cooking"] = new() { "recipe", "chef", "baking", "food", "kitchen" },
        ["art"] = new() { "drawing", "painting", "digital art", "illustration", "creative" },
        ["education"] = new() { "tutorial", "learning", "class", "lecture", "how to" },
        ["vtuber"] = new() { "virtual", "anime", "avatar", "hololive", "nijisanji" }
    };

    // Popular search categories
    private static readonly List<string> PopularCategories = new()
    {
        "gaming", "music", "news", "sports", "just chatting",
        "irl", "asmr", "cooking", "art", "education"
    };

    public Task<List<string>> GetSearchSuggestionsAsync(string partialQuery)
    {
        var suggestions = new List<string>();
        var query = partialQuery.ToLowerInvariant().Trim();

        if (string.IsNullOrEmpty(query))
        {
            // Return popular categories
            return Task.FromResult(PopularCategories.Take(5).ToList());
        }

        // Find matching categories
        var matchingCategories = PopularCategories
            .Where(c => c.Contains(query) || query.Contains(c))
            .Take(3)
            .ToList();

        suggestions.AddRange(matchingCategories);

        // Add related terms if we have a match
        foreach (var term in RelatedTerms.Keys)
        {
            if (query.Contains(term) || term.Contains(query))
            {
                var related = RelatedTerms[term].Take(2);
                suggestions.AddRange(related);
            }
        }

        // Add query variations
        if (!suggestions.Contains(query))
        {
            suggestions.Insert(0, query);
        }

        return Task.FromResult(suggestions.Distinct().Take(8).ToList());
    }

    public Task<string> OptimizeQueryAsync(string userQuery)
    {
        var query = userQuery.Trim();

        // Remove common filler words that don't help search
        var fillerWords = new[] { "show me", "find", "search for", "look for", "i want to watch", "streams about", "live streams of" };
        foreach (var filler in fillerWords)
        {
            query = query.Replace(filler, "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        // Expand abbreviations
        var expansions = new Dictionary<string, string>
        {
            ["fps"] = "first person shooter",
            ["mmo"] = "mmorpg",
            ["jc"] = "just chatting",
            ["irl"] = "in real life"
        };

        foreach (var expansion in expansions)
        {
            if (query.Equals(expansion.Key, StringComparison.OrdinalIgnoreCase))
            {
                query = expansion.Value;
            }
        }

        // Add "live" keyword if not present for better live stream results
        if (!query.Contains("live", StringComparison.OrdinalIgnoreCase))
        {
            // Only add for certain categories that benefit from it
            var liveCategories = new[] { "concert", "news", "sports", "event" };
            if (liveCategories.Any(c => query.Contains(c, StringComparison.OrdinalIgnoreCase)))
            {
                query = $"live {query}";
            }
        }

        return Task.FromResult(query);
    }

    public Task<List<string>> GetRelatedTermsAsync(string query)
    {
        var related = new List<string>();
        var lowerQuery = query.ToLowerInvariant();

        foreach (var term in RelatedTerms.Keys)
        {
            if (lowerQuery.Contains(term))
            {
                related.AddRange(RelatedTerms[term]);
            }
        }

        // If no matches, suggest based on first word
        if (related.Count == 0)
        {
            var firstWord = query.Split(' ').FirstOrDefault()?.ToLowerInvariant();
            if (firstWord != null && RelatedTerms.ContainsKey(firstWord))
            {
                related.AddRange(RelatedTerms[firstWord]);
            }
        }

        return Task.FromResult(related.Distinct().Take(5).ToList());
    }

    public Task<SearchInsights> AnalyzeResultsAsync(List<StreamScanner.Maui.Models.LiveStream> streams)
    {
        var insights = new SearchInsights();

        if (streams.Count == 0)
        {
            insights.Summary = "No live streams found. Try different keywords or check back later.";
            insights.Suggestions.Add("Try broader search terms");
            insights.Suggestions.Add("Check popular categories like 'gaming' or 'music'");
            return Task.FromResult(insights);
        }

        // Calculate statistics
        var streamsWithViewers = streams.Where(s => s.ConcurrentViewers.HasValue).ToList();
        if (streamsWithViewers.Any())
        {
            insights.TotalViewers = streamsWithViewers.Sum(s => s.ConcurrentViewers!.Value);
            insights.AverageViewers = (int)streamsWithViewers.Average(s => s.ConcurrentViewers!.Value);
            insights.TopStream = streamsWithViewers.OrderByDescending(s => s.ConcurrentViewers).First().Title;
        }

        // Find popular channels
        var channelGroups = streams.GroupBy(s => s.ChannelTitle)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToList();

        insights.PopularChannels = channelGroups;

        // Generate summary
        var viewerText = insights.TotalViewers > 0
            ? $" with {insights.TotalViewers:N0} total viewers"
            : "";

        insights.Summary = $"Found {streams.Count} live streams{viewerText}.";

        if (streamsWithViewers.Count < streams.Count)
        {
            insights.Suggestions.Add($"{streams.Count - streamsWithViewers.Count} streams don't report viewer counts");
        }

        return Task.FromResult(insights);
    }
}

public class SearchInsights
{
    public string Summary { get; set; } = string.Empty;
    public int TotalViewers { get; set; }
    public int AverageViewers { get; set; }
    public string? TopStream { get; set; }
    public List<string> PopularChannels { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

/*
 * FUTURE ENHANCEMENT: Full Semantic Kernel Integration
 *
 * To enable AI-powered features with Microsoft Semantic Kernel:
 *
 * 1. Add NuGet packages:
 *    - Microsoft.SemanticKernel
 *    - Microsoft.SemanticKernel.Connectors.OpenAI (or Azure)
 *
 * 2. Configure in MauiProgram.cs:
 *    var kernel = Kernel.CreateBuilder()
 *        .AddAzureOpenAIChatCompletion(
 *            deploymentName: "gpt-4",
 *            endpoint: "https://your-resource.openai.azure.com/",
 *            apiKey: "your-api-key")
 *        .Build();
 *
 * 3. Create AI-powered prompts:
 *    var optimizePrompt = @"
 *        Optimize this YouTube search query for finding live streams: {{$query}}
 *        Return only the optimized query, nothing else.";
 *
 *    var result = await kernel.InvokePromptAsync(optimizePrompt,
 *        new KernelArguments { ["query"] = userQuery });
 *
 * 4. When Microsoft Agent Framework reaches GA (Q1 2026):
 *    - Use ChatClientAgent for multi-turn conversations
 *    - Implement function calling for search refinement
 *    - Add streaming responses for real-time suggestions
 */
