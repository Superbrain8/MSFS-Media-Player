using System.Windows.Forms;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Simple editor for the radio station list: a two-column grid (name + stream URL). The trailing
/// blank row adds a station; Remove deletes the selected one. On Save, <see cref="Stations"/> holds
/// the edited list and DialogResult is OK.
/// </summary>
internal sealed class StationsForm : Form
{
    private readonly DataGridView _grid;

    /// <summary>The edited list, valid after the dialog closes with DialogResult.OK.</summary>
    public List<RadioStation> Stations { get; private set; } = new();

    public StationsForm(IReadOnlyList<RadioStation> stations)
    {
        Text = "Edit radio stations";
        Width = 720;
        Height = 460;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(520, 320);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowHeadersVisible = false,
        };
        var nameCol = new DataGridViewTextBoxColumn { HeaderText = "Name", FillWeight = 40 };
        var urlCol = new DataGridViewTextBoxColumn { HeaderText = "Stream URL", FillWeight = 60 };
        _grid.Columns.Add(nameCol);
        _grid.Columns.Add(urlCol);
        foreach (var s in stations) _grid.Rows.Add(s.Name, s.Url);

        var removeBtn = new Button { Text = "Remove", Width = 90, Height = 30 };
        removeBtn.Click += (_, _) => RemoveSelected();
        var saveBtn = new Button { Text = "Save", Width = 90, Height = 30, DialogResult = DialogResult.OK };
        saveBtn.Click += (_, _) => OnSave();
        var cancelBtn = new Button { Text = "Cancel", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8),
        };
        buttons.Controls.Add(cancelBtn);
        buttons.Controls.Add(saveBtn);
        buttons.Controls.Add(removeBtn);

        Controls.Add(_grid);
        Controls.Add(buttons);
        AcceptButton = saveBtn;
        CancelButton = cancelBtn;
    }

    private void RemoveSelected()
    {
        foreach (DataGridViewRow row in _grid.SelectedRows)
            if (!row.IsNewRow) _grid.Rows.Remove(row);
    }

    private void OnSave()
    {
        var list = new List<RadioStation>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            var name = (row.Cells[0].Value as string)?.Trim() ?? "";
            var url = (row.Cells[1].Value as string)?.Trim() ?? "";
            if (name.Length == 0 || url.Length == 0) continue; // skip incomplete rows
            list.Add(new RadioStation(name, url));
        }
        Stations = list;
    }
}
