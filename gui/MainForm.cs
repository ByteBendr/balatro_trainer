using System.Diagnostics;

namespace BalatroTrainer.Gui;

public sealed class MainForm : Form
{
    private const string GameProcessName = "Balatro";
    private const string DllFileName = "BalatroTrainer.dll";
    private const int Gutter = 24;
    private const int ContentWidth = 572;

    private readonly TrainerClient _client = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private readonly string _dllPath = Path.Combine(AppContext.BaseDirectory, DllFileName);
    private readonly List<RoundedButton> _actionButtons = new();

    private readonly StatusPill _pill = new();
    private readonly CheckBox _chkAuto = new();
    private readonly Label _lblMessage = new();

    private bool _busy;
    private string _lastSeq = "";
    private int _y;

    public MainForm()
    {
        SuspendLayout();

        Text = "Balatro Trainer";
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Felt;
        ForeColor = Theme.Cream;
        Font = Theme.Body;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        BuildHeader();

        AddSection("Economy");
        AddRow("Money ($)", 100, 50, "Add", "money_add", "Set to", "money_set");
        _y += 4;

        AddSection("Hands & cards");
        AddRow("Hands", 1, 1, "Add", "hands_add");
        AddRow("Discards", 1, 1, "Add", "discards_add");
        AddRow("Hand size", 1, 1, "Add", "handsize_add");
        AddRow("Joker slots", 1, 1, "Add", "jokerslots_add");
        AddRow("Consumable slots", 1, 1, "Add", "consumableslots_add");
        _y += 4;

        AddSection("Scoring");
        AddRow("Round score", 1000, 500, "Add", "chips_add", "Meet blind target", "chips_to_blind", secondUsesAmount: false);
        _y += 4;

        BuildFooter();

        ClientSize = new Size(ContentWidth + 2 * Gutter, _y + 10);
        ResumeLayout(false);

        SetReady(false);
        SetStatus("Waiting for Balatro to start…", Theme.Muted);

        _timer.Tick += OnTick;
        _timer.Start();
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _client.Dispose();
        };
    }

    private void BuildHeader()
    {
        Controls.Add(new Label
        {
            Text = "Balatro Trainer",
            Font = Theme.Title,
            ForeColor = Theme.Cream,
            BackColor = Theme.Felt,
            AutoSize = true,
            Location = new Point(Gutter, 12),
        });

        string[] glyphs = { "♠", "♥", "♣", "♦" };
        Color[] colors = { Color.FromArgb(0xcf, 0xd8, 0xdc), Theme.Red, Theme.Blue, Theme.Amber };
        for (int i = 0; i < glyphs.Length; i++)
        {
            Controls.Add(new Label
            {
                Text = glyphs[i],
                Font = Theme.Suit,
                ForeColor = colors[i],
                BackColor = Theme.Felt,
                AutoSize = true,
                Location = new Point(Gutter + ContentWidth - (4 - i) * 30, 20),
            });
        }

        Controls.Add(new Label
        {
            Text = "A cozy little helper for your offline runs",
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            BackColor = Theme.Felt,
            AutoSize = true,
            Location = new Point(Gutter + 2, 56),
        });

        _pill.SetBounds(Gutter, 84, 432, 36);
        Controls.Add(_pill);

        var btnLog = new RoundedButton { Text = "Open log", Ghost = true };
        btnLog.SetBounds(Gutter + 444, 84, 128, 36);
        btnLog.Click += (_, _) => OpenLog();
        Controls.Add(btnLog);

        _chkAuto.Text = "Auto-inject when Balatro starts";
        _chkAuto.Checked = true;
        _chkAuto.AutoSize = true;
        _chkAuto.ForeColor = Theme.Cream;
        _chkAuto.BackColor = Theme.Felt;
        _chkAuto.Location = new Point(Gutter + 2, 130);
        Controls.Add(_chkAuto);

        _y = 164;
    }

    private void AddSection(string name)
    {
        Controls.Add(new Label
        {
            Text = name,
            Font = Theme.Section,
            ForeColor = Theme.Amber,
            BackColor = Theme.Felt,
            AutoSize = true,
            Location = new Point(Gutter + 2, _y),
        });
        _y += 28;
    }

    private void AddRow(string label, long initial, long step,
                        string firstText, string firstCommand,
                        string? secondText = null, string? secondCommand = null,
                        bool secondUsesAmount = true)
    {
        var card = new RoundedPanel { Radius = 14 };
        card.SetBounds(Gutter, _y, ContentWidth, 46);

        var lbl = new Label
        {
            Text = label,
            ForeColor = Theme.Cream,
            BackColor = Theme.Card,
            Font = Theme.BodyBold,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        lbl.SetBounds(18, 0, 124, 46);
        card.Controls.Add(lbl);

        var amount = new AmountBox(initial, step);
        amount.Location = new Point(146, 4);
        card.Controls.Add(amount);

        var first = new RoundedButton { Text = firstText };
        first.SetBounds(332, 6, 96, 34);
        first.Click += async (_, _) => await SendCommandAsync(firstCommand, amount.Value);
        card.Controls.Add(first);
        _actionButtons.Add(first);

        if (secondText is not null && secondCommand is not null)
        {
            var second = new RoundedButton { Text = secondText, Ghost = secondText.Length < 9 };
            second.SetBounds(434, 6, 128, 34);
            second.Click += async (_, _) =>
                await SendCommandAsync(secondCommand, secondUsesAmount ? amount.Value : (long?)null);
            card.Controls.Add(second);
            _actionButtons.Add(second);
        }

        Controls.Add(card);
        _y += 52;
    }

    private void BuildFooter()
    {
        var toast = new RoundedPanel { Fill = Theme.FeltDark, Radius = 12 };
        toast.SetBounds(Gutter, _y, ContentWidth, 38);

        _lblMessage.Text = "Pick a button once Balatro is ready.";
        _lblMessage.ForeColor = Theme.Cream;
        _lblMessage.BackColor = Theme.FeltDark;
        _lblMessage.TextAlign = ContentAlignment.MiddleLeft;
        _lblMessage.AutoEllipsis = true;
        _lblMessage.SetBounds(16, 0, ContentWidth - 32, 38);
        toast.Controls.Add(_lblMessage);
        Controls.Add(toast);
        _y += 46;

        Controls.Add(new Label
        {
            Text = "Negative amounts subtract · changes only apply inside a run · " +
                   "use a spare profile slot if you care about your legit stats.",
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            BackColor = Theme.Felt,
            AutoSize = false,
            Bounds = new Rectangle(Gutter + 2, _y, ContentWidth - 4, 32),
        });
        _y += 34;
    }

    private void SetStatus(string text, Color dot) => _pill.SetState(text, dot);

    private void SetReady(bool ready)
    {
        foreach (var b in _actionButtons) b.Enabled = ready;
    }

    private void ShowMessage(string text)
    {
        string shown = text.StartsWith("ok: ", StringComparison.Ordinal) ? "Done · " + text[4..] : text;

        bool bad = text.StartsWith("error", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("Rejected", StringComparison.Ordinal) ||
                   text.StartsWith("No response", StringComparison.Ordinal) ||
                   text.StartsWith("bootstrap failed", StringComparison.Ordinal);
        bool warn = text.Contains("ignored", StringComparison.Ordinal);

        _lblMessage.ForeColor = bad ? Theme.Red : warn ? Theme.Amber : Theme.Cream;
        _lblMessage.Text = shown;
    }

    private static void OpenLog()
    {
        string path = Path.Combine(Path.GetTempPath(), "BalatroTrainer.log");
        if (!File.Exists(path))
        {
            MessageBox.Show("No log file yet.", "Balatro Trainer");
            return;
        }
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }


    private async Task SendCommandAsync(string command, long? amount)
    {
        string line = amount is null ? command : $"{command} {amount.Value}";
        string? reply = await _client.SendAsync(line);

        ShowMessage(reply switch
        {
            null => "No response from the game backend.",
            "OK" => $"Sent · {line}",
            _ => $"Rejected: {reply}",
        });
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            Process? game = FindGame();
            if (game is null)
            {
                _client.Disconnect();
                _lastSeq = "";
                SetReady(false);
                SetStatus("Waiting for Balatro to start…", Theme.Muted);
                return;
            }

            using (game)
            {
                if (!_client.IsConnected && !await _client.ConnectAsync(150))
                {
                    if (!_chkAuto.Checked)
                    {
                        SetReady(false);
                        SetStatus($"Balatro found (PID {game.Id}) – auto-inject is off", Theme.Amber);
                        return;
                    }

                    if (!Injector.IsLoaded(game, DllFileName))
                    {
                        SetStatus("Injecting backend…", Theme.Amber);
                        await Task.Run(() => Injector.Inject(game.Id, _dllPath));
                    }

                    if (!await _client.ConnectAsync(3000))
                    {
                        SetReady(false);
                        SetStatus("Backend loaded but not reachable (another trainer window open?)", Theme.Red);
                        return;
                    }
                }

                await RefreshStatusAsync();
            }
        }
        catch (Exception ex)
        {
            SetReady(false);
            SetStatus("Error: " + ex.Message, Theme.Red);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RefreshStatusAsync()
    {
        string? reply = await _client.SendAsync("STATUS");
        if (reply is null)
        {
            SetReady(false);
            SetStatus("Lost connection to the backend – retrying…", Theme.Amber);
            return;
        }

        string[] parts = reply.Split('|', 3);
        if (parts.Length != 3)
        {
            SetStatus("Unexpected reply from backend: " + reply, Theme.Red);
            return;
        }

        switch (parts[0])
        {
            case "0":
                SetReady(false);
                SetStatus("Backend loaded – waiting for the game's Lua engine…", Theme.Amber);
                break;
            case "1":
                SetReady(false);
                SetStatus("Hooked – waiting for the game to finish loading…", Theme.Amber);
                break;
            case "2":
                SetReady(true);
                SetStatus("Connected – ready to play", Theme.Green);
                break;
            default:
                SetReady(false);
                SetStatus("Backend failed – click \"Open log\" for details", Theme.Red);
                break;
        }

        if (parts[1] != _lastSeq)
        {
            _lastSeq = parts[1];
            if (parts[2].Length > 0) ShowMessage(parts[2]);
        }
    }

    private static Process? FindGame()
    {
        Process? found = null;
        foreach (Process p in Process.GetProcessesByName(GameProcessName))
        {
            bool exited;
            try { exited = p.HasExited; } catch (Exception) { exited = true; }

            if (found is null && !exited) found = p;
            else p.Dispose();
        }
        return found;
    }
}
