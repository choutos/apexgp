using System;
using System.Linq;
using Godot;
using ApexGP.Core;

namespace ApexGP.Game;

/// <summary>
/// Career menus (title → hub → shop/tune/licence/events → race → results).
/// Functional-first UI: one canvas, rebuilt per screen.
/// </summary>
public partial class CareerUi : CanvasLayer
{
    private CareerProfile _profile = null!;
    private Main _main = null!;
    private Label _title = null!;
    private Label _info = null!;
    private VBoxContainer _list = null!;
    private Screen _screen = Screen.Title;
    private EventDef? _pendingEvent;

    public enum Screen { Title, Hub, Dealership, Tuning, Licences, Events, Results }

    public override void _Ready()
    {
        _profile = CareerProfile.Load(UserProfilePath);
        _main = GetParent<Main>();

        _title = new Label
        {
            Position = new Vector2(28f, 18f),
            LabelSettings = new LabelSettings
            {
                Font = ThemeDB.FallbackFont,
                FontSize = 40,
                FontColor = new Color(0.85f, 0.92f, 1f),
                OutlineSize = 8,
                OutlineColor = new Color(0.04f, 0.08f, 0.2f),
            },
        };
        AddChild(_title);

        _info = new Label
        {
            Position = new Vector2(28f, 62f),
            LabelSettings = new LabelSettings
            {
                Font = ThemeDB.FallbackFont,
                FontSize = 18,
                FontColor = new Color(1f, 0.85f, 0.35f),
                OutlineSize = 4,
                OutlineColor = new Color(0.04f, 0.08f, 0.2f),
            },
        };
        AddChild(_info);

        var scroll = new ScrollContainer
        {
            AnchorLeft = 0.03f, AnchorTop = 0.22f, AnchorRight = 0.5f, AnchorBottom = 0.93f,
        };
        AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Expand, CustomMinimumSize = new Vector2(430f, 0f) };
        scroll.AddChild(_list);

        ShowScreen(Screen.Title);
    }

    public static string UserProfilePath =>
        System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            ".apexgp", "profile.json");

    public void ShowResults(EventDef ev, RaceController race, double prize, int position, bool hasReplay = false)
    {
        _screen = Screen.Results;
        _pendingEvent = null;
        _main.SetRaceVisible(false);
        _title.Text = "RACE RESULTS";
        _info.Text = $"{ev.Name}";
        ClearList();
        int place = 1;
        foreach (var (name, time) in race.Result?.FinishOrder ?? new() { (race.Racers[0].Name, race.Racers[0].FinishTime) })
        {
            var row = new Label
            {
                Text = $"{place++}. {name}  {time:F1}s",
                LabelSettings = new LabelSettings { Font = ThemeDB.FallbackFont, FontSize = 22, FontColor = Colors.White },
            };
            _list.AddChild(row);
        }
        _list.AddChild(Btn($"Prize: {prize:F0} Cr  (P{position})", () => ShowScreen(Screen.Hub)));
        if (hasReplay)
            _list.AddChild(Btn("WATCH REPLAY (R)", () => RequestReplay()));
        _profile.Save(UserProfilePath);
    }

    private System.Action? _replayRequest;

    /// <summary>UI button hook — Main registers the replay starter here.</summary>
    public void SetReplayHandler(System.Action start) => _replayRequest = start;

    private void RequestReplay() => _replayRequest?.Invoke();

    public void ShowReplayBanner()
    {
        _title.Text = "REPLAY";
        _info.Text = "ESC to exit";
        _list.Visible = false;
    }

    public void HideReplayBanner()
    {
        _list.Visible = true;
        if (_screen == Screen.Results) _title.Text = "RACE RESULTS";
        _info.Text = "";
    }

    public void ShowScreen(Screen s)
    {
        _screen = s;
        _main.SetRaceVisible(s == Screen.Hub && false); // race only during an event
        ClearList();
        switch (s)
        {
            case Screen.Title:
                _title.Text = "APEXGP";
                _info.Text = "An original racing sim. v0.4";
                _list.AddChild(Btn("START CAREER", () => ShowScreen(Screen.Hub)));
                break;

            case Screen.Hub:
                _title.Text = "CAREER";
                _info.Text = $"{_profile.Credits:F0} Cr";
                if (_profile.Garage.Count == 0)
                {
                    _info.Text += "   — buy your first car at the dealership!";
                    _list.AddChild(Btn("DEALERSHIP", () => ShowScreen(Screen.Dealership)));
                }
                else
                {
                    var car = CarSpec.ById(_profile.ActiveCarId);
                    _info.Text += $"   — {car.Name}";
                    _list.AddChild(Btn("RACE EVENTS", () => ShowScreen(Screen.Events)));
                    _list.AddChild(Btn("DEALERSHIP", () => ShowScreen(Screen.Dealership)));
                    _list.AddChild(Btn("TUNING SHOP", () => ShowScreen(Screen.Tuning)));
                    _list.AddChild(Btn("LICENCE SCHOOL", () => ShowScreen(Screen.Licences)));
                    _list.AddChild(Btn("SAVE", () => { _profile.Save(UserProfilePath); ShowScreen(Screen.Hub); }));
                }
                break;

            case Screen.Dealership:
                _title.Text = "DEALERSHIP";
                _info.Text = $"{_profile.Credits:F0} Cr";
                foreach (var spec in CarSpec.All)
                {
                    var owned = _profile.Garage.Any(g => g.CarId == spec.Id);
                    _list.AddChild(Btn(
                        owned ? $"{spec.Name} — OWNED" : $"{spec.Name} — {spec.Price:F0} Cr",
                        () =>
                        {
                            if (owned) return;
                            try { Career.BuyCar(_profile, spec); }
                            catch (InvalidOperationException) { }
                            ShowScreen(Screen.Dealership);
                        }));
                }
                _list.AddChild(Btn("← BACK", () => ShowScreen(Screen.Hub)));
                break;

            case Screen.Tuning:
                _title.Text = "TUNING SHOP";
                _info.Text = $"{_profile.Credits:F0} Cr — {CarSpec.ById(_profile.ActiveCarId).Name}";
                var active = _profile.Garage.First(g => g.CarId == _profile.ActiveCarId);
                foreach (var part in TunePart.All)
                {
                    bool installed = active.TuningParts.Contains(part.Id);
                    _list.AddChild(Btn(
                        installed ? $"{part.Name} — INSTALLED" : $"{part.Name} — {part.Price:F0} Cr",
                        () =>
                        {
                            try { Career.TuneCar(_profile, part.Id); }
                            catch (InvalidOperationException) { }
                            ShowScreen(Screen.Tuning);
                        }));
                }
                _list.AddChild(Btn("← BACK", () => ShowScreen(Screen.Hub)));
                break;

            case Screen.Licences:
                _title.Text = "LICENCE SCHOOL";
                _info.Text = "Pass all gates before time runs out. (AI will drive for now — M2 will put you in the seat.)";
                foreach (var def in LicenseCatalog.All)
                {
                    bool has = _profile.Licenses.Contains(def.Id);
                    _list.AddChild(Btn(
                        has ? $"{def.Name} — EARNED" : $"{def.Name}",
                        () => _main.StartLicenseTest(def)));
                }
                _list.AddChild(Btn("← BACK", () => ShowScreen(Screen.Hub)));
                break;

            case Screen.Events:
                _title.Text = "RACE EVENTS";
                _info.Text = $"{_profile.Credits:F0} Cr";
                foreach (var ev in EventCalendar.All)
                {
                    bool done = _profile.CompletedEvents.Contains(ev.Id);
                    bool can = ev.CanEnter(_profile);
                    _list.AddChild(Btn(
                        done ? $"{ev.Name} — WON" :
                        can ? $"{ev.Name} — {ev.Laps} laps, {ev.EntryFee:F0} Cr entry, {ev.Prize:F0} Cr" :
                              $"{ev.Name} — locked",
                        () =>
                        {
                            if (!can) return;
                            _pendingEvent = ev;
                            _main.StartEvent(_profile, ev);
                            SetRaceVisible(true);
                        }));
                }
                _list.AddChild(Btn("← BACK", () => ShowScreen(Screen.Hub)));
                break;
        }
    }

    private Button Btn(string text, Action onClick)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40) };
        b.Pressed += () => onClick();
        return b;
    }

    private void ClearList()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
    }

    public void SetRaceVisible(bool visible)
    {
        _title.Visible = !visible;
        _info.Visible = !visible;
        _list.Visible = !visible;
    }
}
