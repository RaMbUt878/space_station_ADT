using Content.Client.Lobby;
using Content.Client.UserInterface.Controls;
using Content.Shared.Access.Systems;
using Content.Shared.Administration;
using Content.Shared.CriminalRecords;
using Content.Shared.Dataset;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Robust.Client.AutoGenerated;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client.CriminalRecords;

// TODO: dedupe shitcode from general records theres a lot
[GenerateTypedNameReferences]
public sealed partial class CriminalRecordsConsoleWindow : FancyWindow
{
    private readonly IPlayerManager _player;
    private readonly IPrototypeManager _proto;
    private readonly IRobustRandom _random;
    private readonly AccessReaderSystem _accessReader;
    private readonly LobbyUIController _ui;
    private readonly IEntityManager _entMan;

    public readonly EntityUid Console;

    [ValidatePrototypeId<DatasetPrototype>]
    private const string ReasonPlaceholders = "CriminalRecordsWantedReasonPlaceholders";

    public Action<uint?>? OnKeySelected;
    public Action<StationRecordFilterType, string>? OnFiltersChanged;
    public Action<SecurityStatus>? OnStatusSelected;
    public Action<CriminalRecord, bool, bool>? OnHistoryUpdated;
    public Action? OnHistoryClosed;
    public Action<SecurityStatus, string>? OnDialogConfirmed;

    private uint _maxLength;
    private bool _access;
    private uint? _selectedKey;
    private CriminalRecord? _selectedRecord;

    private DialogWindow? _reasonDialog;

    private StationRecordFilterType _currentFilterType;
    public EntityUid? CurrentShowcase;  // ADT Station Records Showcase Tweaked

    public CriminalRecordsConsoleWindow(EntityUid console, uint maxLength, IPlayerManager playerManager, IPrototypeManager prototypeManager, IRobustRandom robustRandom, AccessReaderSystem accessReader, IEntityManager entMan)    // ADT Station Records Showcase Tweaked
    {
        RobustXamlLoader.Load(this);

        Console = console;
        _player = playerManager;
        _proto = prototypeManager;
        _random = robustRandom;
        _accessReader = accessReader;
        // ADT Station Records Showcase Start
        _ui = UserInterfaceManager.GetUIController<LobbyUIController>();
        _entMan = entMan;
        // ADT Station Records Showcase End

        _maxLength = maxLength;
        _currentFilterType = StationRecordFilterType.Name;

        OpenCentered();

        foreach (var item in Enum.GetValues<StationRecordFilterType>())
        {
            FilterType.AddItem(GetTypeFilterLocals(item), (int)item);
        }

        foreach (var status in Enum.GetValues<SecurityStatus>())
        {
            AddStatusSelect(status);
        }

        OnClose += () => _reasonDialog?.Close();

        RecordListing.OnItemSelected += args =>
        {
            if (RecordListing[args.ItemIndex].Metadata is not uint cast)
                return;

            OnKeySelected?.Invoke(cast);
        };

        RecordListing.OnItemDeselected += _ =>
        {
            OnKeySelected?.Invoke(null);
        };

        FilterType.OnItemSelected += eventArgs =>
        {
            var type = (StationRecordFilterType)eventArgs.Id;

            if (_currentFilterType != type)
            {
                _currentFilterType = type;
                FilterListingOfRecords(FilterText.Text);
            }
        };

        FilterText.OnTextEntered += args =>
        {
            FilterListingOfRecords(args.Text);
        };

        StatusOptionButton.OnItemSelected += args =>
        {
            SetStatus((SecurityStatus) args.Id);
        };

        HistoryButton.OnPressed += _ =>
        {
            if (_selectedRecord is {} record)
                OnHistoryUpdated?.Invoke(record, _access, true);
        };
    }

    public void UpdateState(CriminalRecordsConsoleState state)
    {
        if (state.Filter != null)
        {
            if (state.Filter.Type != _currentFilterType)
            {
                _currentFilterType = state.Filter.Type;
            }

            if (state.Filter.Value != FilterText.Text)
            {
                FilterText.Text = state.Filter.Value;
            }
        }

        _selectedKey = state.SelectedKey;

        FilterType.SelectId((int)_currentFilterType);

        NoRecords.Visible = state.RecordListing == null || state.RecordListing.Count == 0;
        PopulateRecordListing(state.RecordListing);

        // set up the selected person's record
        var selected = _selectedKey != null;

        PersonContainer.Visible = selected;
        RecordUnselected.Visible = !selected;

        _access = _player.LocalSession?.AttachedEntity is {} player
            && _accessReader.IsAllowed(player, Console);

        // hide access-required editing parts when no access
        var editing = _access && selected;
        StatusOptionButton.Disabled = !editing;

        if (state is { CriminalRecord: not null, StationRecord: not null })
        {
            PopulateRecordContainer(state.StationRecord, state.CriminalRecord);
            OnHistoryUpdated?.Invoke(state.CriminalRecord, _access, false);
            _selectedRecord = state.CriminalRecord;
        }
        else
        {
            _selectedRecord = null;
            OnHistoryClosed?.Invoke();
        }
    }

    private void PopulateRecordListing(Dictionary<uint, string>? listing)
    {
        if (listing == null)
        {
            RecordListing.Clear();
            return;
        }

        var entries = listing.ToList();
        entries.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.Ordinal));
        // `entries` now contains the definitive list of items which should be in
        // our list of records and is in the order we want to present those items.

        // Walk through the existing items in RecordListing and in the updated listing
        // in parallel to synchronize the items in RecordListing with `entries`.
        int i = RecordListing.Count - 1;
        int j = entries.Count - 1;
        while(i >= 0 && j >= 0)
        {
            var strcmp = string.Compare(RecordListing[i].Text, entries[j].Value, StringComparison.Ordinal);
            if (strcmp == 0)
            {
                // This item exists in both RecordListing and `entries`. Nothing to do.
                i--;
                j--;
            }
            else if (strcmp > 0)
            {
                // Item exists in RecordListing, but not in `entries`. Remove it.
                RecordListing.RemoveAt(i);
                i--;
            }
            else if (strcmp < 0)
            {
                // A new entry which doesn't exist in RecordListing. Create it.
                RecordListing.Insert(i + 1, new ItemList.Item(RecordListing){Text = entries[j].Value, Metadata = entries[j].Key});
                j--;
            }
        }

        // Any remaining items in RecordListing don't exist in `entries`, so remove them
        while (i >= 0)
        {
            RecordListing.RemoveAt(i);
            i--;
        }

        // And finally, any remaining items in `entries`, don't exist in RecordListing. Create them.
        while (j >= 0)
        {
            RecordListing.Insert(0, new ItemList.Item(RecordListing){Text = entries[j].Value, Metadata = entries[j].Key});
            j--;
        }
    }

    private void PopulateRecordContainer(GeneralStationRecord stationRecord, CriminalRecord criminalRecord)
    {
        var na = Loc.GetString("generic-not-available-shorthand");
        // PersonName.Text = stationRecord.Name;    // ADT Commented
        PersonPrints.Text = Loc.GetString("general-station-record-console-record-fingerprint", ("fingerprint", stationRecord.Fingerprint ?? na));
        PersonDna.Text = Loc.GetString("general-station-record-console-record-dna", ("dna", stationRecord.DNA ?? na));

        // ADT Station Records Showcase Start
        _entMan.DeleteEntity(CurrentShowcase);
        var dummy = _ui.LoadProfileEntity(stationRecord.Profile, _proto.Index<JobPrototype>(stationRecord.JobPrototype), true);
        Showcase.SetEntity(dummy);
        PersonName.SetMessage(stationRecord.Name, defaultColor: Color.White);
        PersonJob.SetMessage(Loc.GetString("general-station-record-console-record-title", ("job", stationRecord.JobTitle ?? na)), defaultColor: Color.White);
        // ADT Station Records Showcase End

        StatusOptionButton.SelectId((int) criminalRecord.Status);
        if (criminalRecord.Reason is {} reason)
        {
            var message = FormattedMessage.FromMarkupOrThrow(Loc.GetString("criminal-records-console-wanted-reason"));
            message.AddText($": {reason}");
            WantedReason.SetMessage(message);
            WantedReason.Visible = true;
        }
        else
        {
            WantedReason.Visible = false;
        }
    }

    private void AddStatusSelect(SecurityStatus status)
    {
        var name = Loc.GetString($"criminal-records-status-{status.ToString().ToLower()}");
        StatusOptionButton.AddItem(name, (int)status);
    }

    private void FilterListingOfRecords(string text = "")
    {
        OnFiltersChanged?.Invoke(_currentFilterType, text);
    }

    private void SetStatus(SecurityStatus status)
    {
        if (status == SecurityStatus.Wanted || status == SecurityStatus.Suspected)
        {
            GetReason(status);
            return;
        }

        OnStatusSelected?.Invoke(status);
    }

    private void GetReason(SecurityStatus status)
    {
        if (_reasonDialog != null)
        {
            _reasonDialog.MoveToFront();
            return;
        }

        var field = "reason";
        var title = Loc.GetString("criminal-records-status-" + status.ToString().ToLower());
        var placeholders = _proto.Index<DatasetPrototype>(ReasonPlaceholders);
        var placeholder = Loc.GetString("criminal-records-console-reason-placeholder", ("placeholder", _random.Pick(placeholders.Values))); // just funny it doesn't actually get used
        var prompt = Loc.GetString("criminal-records-console-reason");
        var entry = new QuickDialogEntry(field, QuickDialogEntryType.LongText, prompt, placeholder);
        var entries = new List<QuickDialogEntry>() { entry };
        _reasonDialog = new DialogWindow(title, entries);

        _reasonDialog.OnConfirmed += responses =>
        {
            var reason = responses[field];
            if (reason.Length < 1 || reason.Length > _maxLength)
                return;

            OnDialogConfirmed?.Invoke(status, reason);
        };

        _reasonDialog.OnClose += () => { _reasonDialog = null; };
    }

    private string GetTypeFilterLocals(StationRecordFilterType type)
    {
        return Loc.GetString($"criminal-records-{type.ToString().ToLower()}-filter");
    }

    // ADT Station Records Showcase Start
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _entMan.DeleteEntity(CurrentShowcase);
    }
    // ADT Station Records Showcase End
}
