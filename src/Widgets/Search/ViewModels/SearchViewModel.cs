using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Search.Locales;
using Search.Models;
using Search.Services;

namespace Search.ViewModels;

public class SearchViewModel : INotifyPropertyChanged
{
    private SearchModel model;
    private string queryText = "";
    private SearchEngine currentEngine;
    private string selectedCategory = "全部";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<SearchModel>? ModelChanged;

    public ObservableCollection<SearchEngine> AllEngines { get; } = [];
    public ObservableCollection<SearchEngine> DisplayEngines { get; } = [];
    public ObservableCollection<string> Categories { get; } = [];
    public ObservableCollection<string> RecentSearches { get; } = [];

    public SearchViewModel(SearchModel initialModel)
    {
        model = initialModel;
        ReloadEngines();

        currentEngine = AllEngines.FirstOrDefault(e => e.Id.Equals(model.CurrentEngineId, StringComparison.OrdinalIgnoreCase))
                        ?? AllEngines.FirstOrDefault()
                        ?? PresetEngines.GetDefaults().First();

        UpdateCategories();
        UpdateDisplayEngines();
        UpdateHistory();
    }

    public SearchModel Model => model;

    public SearchEngine CurrentEngine
    {
        get => currentEngine;
        set
        {
            if (currentEngine?.Id == value?.Id) return;
            currentEngine = value ?? AllEngines.FirstOrDefault() ?? PresetEngines.GetDefaults().First();
            model = model with { CurrentEngineId = currentEngine.Id };
            OnPropertyChanged();
            OnPropertyChanged(nameof(EngineName));
            OnPropertyChanged(nameof(EngineIconPath));
            OnPropertyChanged(nameof(EngineColor));
            OnPropertyChanged(nameof(SearchPlaceholder));
            ModelChanged?.Invoke(model);
        }
    }

    public string QueryText
    {
        get => queryText;
        set
        {
            if (queryText == value) return;
            queryText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasQuery));
        }
    }

    public bool HasQuery => !string.IsNullOrWhiteSpace(queryText);

    public string EngineName => CurrentEngine?.Name ?? Locale.Search;
    public string EngineIconPath => SearchIconProvider.GetPath(CurrentEngine?.IconKey ?? "Google");
    public string EngineColor => CurrentEngine?.IconColor ?? "#4285F4";
    public string SearchPlaceholder => $"{Locale.Search} {EngineName}...";

    public string SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (selectedCategory == value) return;
            selectedCategory = value;
            OnPropertyChanged();
            UpdateDisplayEngines();
        }
    }

    public void UpdateModel(SearchModel newModel)
    {
        model = newModel;
        ReloadEngines();

        currentEngine = AllEngines.FirstOrDefault(e => e.Id.Equals(model.CurrentEngineId, StringComparison.OrdinalIgnoreCase))
                        ?? AllEngines.FirstOrDefault()
                        ?? PresetEngines.GetDefaults().First();

        UpdateCategories();
        UpdateDisplayEngines();
        UpdateHistory();

        OnPropertyChanged(nameof(CurrentEngine));
        OnPropertyChanged(nameof(EngineName));
        OnPropertyChanged(nameof(EngineIconPath));
        OnPropertyChanged(nameof(EngineColor));
        OnPropertyChanged(nameof(SearchPlaceholder));
    }

    public void SelectEngine(SearchEngine engine)
    {
        if (engine != null)
        {
            CurrentEngine = engine;
        }
    }

    public void ExecuteSearch(string? explicitQuery = null, SearchEngine? overrideEngine = null)
    {
        var targetEngine = overrideEngine ?? CurrentEngine;
        var text = explicitQuery ?? QueryText;

        if (string.IsNullOrWhiteSpace(text))
        {
            // If empty, open search engine homepage
            SearchLauncher.Launch(targetEngine, "");
            return;
        }

        var trimmed = text.Trim();

        if (model.SaveHistory)
        {
            AddHistory(trimmed);
        }

        SearchLauncher.Launch(targetEngine, trimmed);

        if (model.ClearInputAfterSearch)
        {
            QueryText = "";
        }
    }

    public void ClearQuery()
    {
        QueryText = "";
    }

    public void ClearAllHistory()
    {
        model = model with { RecentHistory = [] };
        RecentSearches.Clear();
        ModelChanged?.Invoke(model);
    }

    private void AddHistory(string item)
    {
        var list = model.RecentHistory.ToList();
        list.RemoveAll(x => x.Equals(item, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, item);
        if (list.Count > 10) list = list.Take(10).ToList();

        model = model with { RecentHistory = list };
        UpdateHistory();
        ModelChanged?.Invoke(model);
    }

    private void UpdateHistory()
    {
        RecentSearches.Clear();
        foreach (var item in model.RecentHistory)
        {
            RecentSearches.Add(item);
        }
        OnPropertyChanged(nameof(RecentSearches));
        OnPropertyChanged(nameof(HasHistory));
    }

    public bool HasHistory => RecentSearches.Count > 0;

    private void ReloadEngines()
    {
        AllEngines.Clear();
        var defaults = PresetEngines.GetDefaults();

        // Check if user has custom engines or override
        var list = new List<SearchEngine>();
        if (model.CustomEngines != null && model.CustomEngines.Count > 0)
        {
            list.AddRange(model.CustomEngines.Select(e => e.Clone()));
        }
        else
        {
            list.AddRange(defaults.Select(e => e.Clone()));
        }

        foreach (var eng in list.OrderBy(e => e.Priority))
        {
            AllEngines.Add(eng);
        }
    }

    private void UpdateCategories()
    {
        Categories.Clear();
        Categories.Add("全部");
        var cats = AllEngines.Where(e => e.IsEnabled).Select(e => e.Category).Distinct().Where(c => !string.IsNullOrEmpty(c) && c != "全部");
        foreach (var cat in cats)
        {
            Categories.Add(cat);
        }
    }

    private void UpdateDisplayEngines()
    {
        DisplayEngines.Clear();
        var filtered = (selectedCategory == "全部"
            ? AllEngines.Where(e => e.IsEnabled)
            : AllEngines.Where(e => e.IsEnabled && e.Category == selectedCategory)).ToList();

        foreach (var eng in filtered)
        {
            DisplayEngines.Add(eng);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
