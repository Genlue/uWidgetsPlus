using Clock.Models;
using ReactiveUI;

namespace Clock.ViewModels;

public class WorldClockViewModel : ReactiveObject, IDisposable
{
    private readonly List<AnalogClockViewModel> viewModels;
    private readonly DigitalClockViewModel digital;

    public WorldClockViewModel(WorldClockModel worldClockModel)
    {
        viewModels = Enumerable
            .Range(0, 4)
            .Select(i =>
            {
                var vm = new AnalogClockViewModel(new ClockModel(
                    false,
                    false,
                    false,
                    worldClockModel.TimeZoneIds.ElementAtOrDefault(i)))
                {
                    CustomCityName = worldClockModel.CityNames?.ElementAtOrDefault(i)
                };
                return vm;
            })
            .ToList();

        // Center digital time on 2x2: local time, 24-hour, no seconds.
        digital = new DigitalClockViewModel(new ClockModel(false, false, true, null));
    }

    public AnalogClockViewModel First => viewModels[0];
    public AnalogClockViewModel Second => viewModels[1];
    public AnalogClockViewModel Third => viewModels[2];
    public AnalogClockViewModel Fourth => viewModels[3];

    public DigitalClockViewModel Digital => digital;

    public void Dispose()
    {
        viewModels.ForEach(x => x.Dispose());
        digital.Dispose();
        GC.SuppressFinalize(this);
    }
}
