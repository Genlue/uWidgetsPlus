using ReactiveUI;
using Reminders.Models;

namespace Reminders.ViewModels;

/// <summary>
/// Single, long-lived view model for the widget. Kept in sync with the model
/// in place (no DataContext swaps) so the editing session, focus and caret
/// survive saves and external updates.
/// </summary>
public class RemindersViewModel : ReactiveObject
{
    private RemindersListModel model;

    public RemindersViewModel(RemindersListModel model) => this.model = model;

    /// <summary>
    /// The current model — the single source of truth during the session
    /// (the bindings write through to it, the view persists it on focus loss).
    /// </summary>
    public RemindersListModel Model => model;

    /// <summary>
    /// Replace the underlying model and notify the bindings.
    /// </summary>
    public void Update(RemindersListModel newModel)
    {
        model = newModel;
        this.RaisePropertyChanged(nameof(ListName));
        this.RaisePropertyChanged(nameof(Reminders));
        this.RaisePropertyChanged(nameof(Count));
    }

    /// <summary>
    /// The list title. Writable so the TwoWay TextBox binding keeps the model
    /// in sync while typing (the view still persists on focus loss).
    /// </summary>
    public string? ListName
    {
        get => model.ListName;
        set
        {
            if (value == model.ListName) return;
            model = model with { ListName = value };
            this.RaisePropertyChanged();
        }
    }

    public IEnumerable<ReminderModel> Reminders => model.Reminders;
    public int Count => model.Reminders.Count;
}
