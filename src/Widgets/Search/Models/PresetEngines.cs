using System.Collections.Generic;

namespace Search.Models;

public static class PresetEngines
{
    public static List<SearchEngine> GetDefaults()
    {
        return
        [
            new SearchEngine
            {
                Id = "google",
                Name = "Google",
                UrlTemplate = "https://www.google.com/search?q={q}",
                IconKey = "Google",
                IconColor = "#4285F4",
                Category = "通用",
                IsEnabled = true,
                IsCustom = false,
                Priority = 0
            },
            new SearchEngine
            {
                Id = "bing",
                Name = "必应",
                UrlTemplate = "https://www.bing.com/search?q={q}",
                IconKey = "Bing",
                IconColor = "#008373",
                Category = "通用",
                IsEnabled = true,
                IsCustom = false,
                Priority = 1
            },
            new SearchEngine
            {
                Id = "baidu",
                Name = "百度",
                UrlTemplate = "https://www.baidu.com/s?wd={q}",
                IconKey = "Baidu",
                IconColor = "#2932E1",
                Category = "通用",
                IsEnabled = true,
                IsCustom = false,
                Priority = 2
            },
            new SearchEngine
            {
                Id = "duckduckgo",
                Name = "DuckDuckGo",
                UrlTemplate = "https://duckduckgo.com/?q={q}",
                IconKey = "DuckDuckGo",
                IconColor = "#DE5833",
                Category = "通用",
                IsEnabled = true,
                IsCustom = false,
                Priority = 3
            },
            new SearchEngine
            {
                Id = "github",
                Name = "GitHub",
                UrlTemplate = "https://github.com/search?q={q}",
                IconKey = "GitHub",
                IconColor = "#24292E",
                Category = "开发者",
                IsEnabled = true,
                IsCustom = false,
                Priority = 4
            },
            new SearchEngine
            {
                Id = "bilibili",
                Name = "哔哩哔哩",
                UrlTemplate = "https://search.bilibili.com/all?keyword={q}",
                IconKey = "Bilibili",
                IconColor = "#00AEEC",
                Category = "影音媒体",
                IsEnabled = true,
                IsCustom = false,
                Priority = 5
            },
            new SearchEngine
            {
                Id = "zhihu",
                Name = "知乎",
                UrlTemplate = "https://www.zhihu.com/search?type=content&q={q}",
                IconKey = "Zhihu",
                IconColor = "#0084FF",
                Category = "问答社区",
                IsEnabled = true,
                IsCustom = false,
                Priority = 6
            },
            new SearchEngine
            {
                Id = "metaso",
                Name = "秘塔 AI",
                UrlTemplate = "https://metaso.cn/?q={q}",
                IconKey = "Metaso",
                IconColor = "#175CD3",
                Category = "AI 智能",
                IsEnabled = true,
                IsCustom = false,
                Priority = 7
            },
            new SearchEngine
            {
                Id = "kimi",
                Name = "Kimi",
                UrlTemplate = "https://kimi.moonshot.cn/?q={q}",
                IconKey = "Kimi",
                IconColor = "#1677FF",
                Category = "AI 智能",
                IsEnabled = true,
                IsCustom = false,
                Priority = 8
            },
            new SearchEngine
            {
                Id = "youtube",
                Name = "YouTube",
                UrlTemplate = "https://www.youtube.com/results?search_query={q}",
                IconKey = "YouTube",
                IconColor = "#FF0000",
                Category = "影音媒体",
                IsEnabled = true,
                IsCustom = false,
                Priority = 9
            }
        ];
    }
}
