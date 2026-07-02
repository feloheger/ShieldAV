using System.Drawing.Drawing2D;
using ShieldAV.Engine;
using ShieldAV.Models;

namespace ShieldAV.Forms;

public sealed class MainForm : Form
{
    // ── engine ──────────────────────────────────────────────────────
    ScanEngine        _engine     = new();
    QuarantineManager _quarantine = new();
    ScanStats         _stats      = new();
    string _folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    volatile bool _closing;
    int _logLines; const int LOG_MAX = 1200;

    // ── UI refs ──────────────────────────────────────────────────────
    StatCard   _cardStatus   = null!;
    StatCard   _cardThreats  = null!;
    StatCard   _cardScanned  = null!;
    StatCard   _cardQuar     = null!;
    RichTextBox _rtfLog      = null!;
    Panel      _threatPanel  = null!;
    Label      _lblNoThreats = null!;
    Button      _btnQuick     = null!;
    Button      _btnFull      = null!;
    Button      _btnCancel    = null!;
    ProgressBar _progress     = null!;
    Label       _lblProgress  = null!;

    // ── palette (from screenshot) ────────────────────────────────────
    static readonly Color C_BG      = Color.FromArgb(26,  26,  36);   // main bg
    static readonly Color C_SURFACE = Color.FromArgb(34,  34,  46);   // card/box bg
    static readonly Color C_BORDER  = Color.FromArgb(50,  50,  68);   // borders
    static readonly Color C_GREEN   = Color.FromArgb(29,  158, 117);  // accent green
    static readonly Color C_GREEN_D = Color.FromArgb(15,  110,  86);  // icon bg green
    static readonly Color C_TEXT    = Color.FromArgb(220, 220, 232);  // main text
    static readonly Color C_MUTED   = Color.FromArgb(130, 130, 155);  // muted text
    static readonly Color C_RED     = Color.FromArgb(226,  75,  74);  // threat red
    static readonly Color C_AMBER   = Color.FromArgb(186, 117,  23);  // amber/warn
    static readonly Color C_THREAT_BG  = Color.FromArgb(55, 18, 18);
    static readonly Color C_THREAT_TXT = Color.FromArgb(255,140,130);

    static readonly Font F_TITLE  = new("Segoe UI", 18f, FontStyle.Bold);
    static readonly Font F_SUB    = new("Segoe UI",  9f);
    static readonly Font F_UI     = new("Segoe UI",  9.5f);
    static readonly Font F_BOLD   = new("Segoe UI",  9.5f, FontStyle.Bold);
    static readonly Font F_CAPS   = new("Segoe UI",  7.5f, FontStyle.Bold);
    static readonly Font F_MONO   = new("Consolas",  8.5f);
    static readonly Font F_BTN    = new("Segoe UI",  9.5f, FontStyle.Bold);

    public MainForm()
    {
        Build();
        LoadQuarantine();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing = true;
        _engine.Cancel();
        base.OnFormClosing(e);
    }

    // ════════════════════════════════════════════════════════════════
    void Build()
    {
        SuspendLayout();
        Text           = "ShieldAV Antivirus";
        Size           = new Size(1020, 860);
        MinimumSize    = new Size(900,  760);
        StartPosition  = FormStartPosition.CenterScreen;
        BackColor      = C_BG;
        ForeColor      = C_TEXT;
        Font           = F_UI;
        Icon           = SystemIcons.Shield;
        DoubleBuffered = true;

        // ── scrollable main area ────────────────────────────────────
        var scroll = new Panel
        {
            Dock      = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = C_BG
        };

        // ── inner layout (fixed width, scrolls) ─────────────────────
        var inner = new Panel
        {
            Width     = 980,
            BackColor = C_BG,
            Padding   = new Padding(20, 20, 20, 20)
        };

        int y = 20;

        // ── HEADER ──────────────────────────────────────────────────
        var hdr = new HeaderPanel
        {
            Location = new Point(20, y),
            Size     = new Size(940, 88)
        };
        inner.Controls.Add(hdr);
        y += 108;

        // ── STAT CARDS ──────────────────────────────────────────────
        _cardStatus  = new StatCard("STATUS",            "Geschützt", C_GREEN);
        _cardThreats = new StatCard("BEDROHUNGEN",       "0",         C_RED);
        _cardScanned = new StatCard("GESCANNTE DATEIEN", "0",         C_TEXT);
        _cardQuar    = new StatCard("QUARANTÄNE",        "0",         C_AMBER);

        int cw = 222, ch = 90, gap = 10;
        int cx = 20;
        foreach (var card in new[]{_cardStatus,_cardThreats,_cardScanned,_cardQuar})
        {
            card.Location = new Point(cx, y);
            card.Size     = new Size(cw, ch);
            inner.Controls.Add(card);
            cx += cw + gap;
        }
        y += ch + 20;

        // ── SCAN FORTSCHRITTSBALKEN ─────────────────────────────────
        _lblProgress = new Label
        {
            Text      = "Bereit",
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = C_MUTED,
            AutoSize  = true,
            Location  = new Point(20, y)
        };
        inner.Controls.Add(_lblProgress);
        y += 20;

        _progress = new ProgressBar
        {
            Location  = new Point(20, y),
            Size      = new Size(940, 10),
            Style     = ProgressBarStyle.Continuous,
            ForeColor = C_GREEN,
            BackColor = Color.FromArgb(45, 45, 58),
            Minimum   = 0,
            Maximum   = 100,
            Value     = 0
        };
        inner.Controls.Add(_progress);
        y += 20;

        // ── SCHUTZBEREICHE ──────────────────────────────────────────
        inner.Controls.Add(SectionLabel("SCHUTZBEREICHE", 20, y)); y += 26;
        var bars = new (string, int, Color)[]
        {
            ("Echtzeit",      100, C_GREEN),
            ("Webschutz",     100, C_GREEN),
            ("E-Mail-Filter",  87, C_AMBER),
            ("Firewall",      100, C_GREEN),
        };
        foreach (var (lbl, pct, col) in bars)
        {
            inner.Controls.Add(new ProgressRow(lbl, pct, col) { Location=new Point(20,y), Size=new Size(940,28) });
            y += 32;
        }
        y += 10;

        // ── SCAN-PROTOKOLL BOX ──────────────────────────────────────
        inner.Controls.Add(SectionLabel("SCAN-PROTOKOLL", 20, y)); y += 26;
        var logBox = new Panel
        {
            Location  = new Point(20, y),
            Size      = new Size(940, 160),
            BackColor = C_SURFACE,
            Padding   = new Padding(14, 12, 14, 12)
        };
        logBox.Paint += PaintRoundedBorder;
        _rtfLog = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = C_SURFACE,
            ForeColor   = C_TEXT,
            Font        = F_MONO,
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
            ScrollBars  = RichTextBoxScrollBars.Vertical
        };
        logBox.Controls.Add(_rtfLog);
        inner.Controls.Add(logBox);
        y += 170;

        // ── SCAN BUTTONS ────────────────────────────────────────────
        _btnQuick = DarkBtn("Schnellscan",        220);
        _btnFull  = DarkBtn("Vollständiger Scan", 220);
        var btnClearLog = DarkBtn("Log leeren",   180);
        var btnSettings = DarkBtn("⚙  Einstellungen", 190);
        btnSettings.BackColor = Color.FromArgb(50, 50, 70);

        _btnQuick.Location    = new Point(20,  y);
        _btnFull.Location     = new Point(175, y);
        _btnCancel.Location   = new Point(400, y);
        btnClearLog.Location  = new Point(560, y);
        btnSettings.Location  = new Point(750, y);

        _btnCancel = DarkBtn("⏹  Abbrechen", 150);
        _btnCancel.BackColor = Color.FromArgb(180, 50, 50);
        _btnCancel.ForeColor = Color.White;
        _btnCancel.Location  = new Point(250 + 230, y);
        _btnCancel.Enabled   = false;

        _btnQuick.Click    += (_, _) => StartScan(quick: true);
        _btnFull.Click     += (_, _) => StartScan(quick: false);
        _btnCancel.Click   += (_, _) => { _engine.Cancel(); _btnCancel.Enabled = false; _lblProgress.Text = "Scan abgebrochen"; };
        btnClearLog.Click  += (_, _) => { _rtfLog.Clear(); _logLines = 0; };
        btnSettings.Click  += (_, _) =>
        {
            using var dlg = new ShieldAV.Forms.SettingsForm();
            dlg.ShowDialog(this);
            // Refresh VT status in log
            string vtStatus = string.IsNullOrEmpty(ShieldAV.Engine.HashScanner.VirusTotalApiKey)
                ? "VirusTotal: nicht konfiguriert (nur MalwareBazaar aktiv)"
                : "VirusTotal: API-Key gesetzt ✅ – 70+ Engines aktiv";
            Log(vtStatus, string.IsNullOrEmpty(ShieldAV.Engine.HashScanner.VirusTotalApiKey)
                ? Color.FromArgb(186, 117, 23)
                : Color.FromArgb(29, 158, 117));
        };

        inner.Controls.AddRange([_btnQuick, _btnFull, _btnCancel, btnClearLog, btnSettings]);
        y += 52;

        // ── ERKANNTE BEDROHUNGEN ────────────────────────────────────
        inner.Controls.Add(SectionLabel("ERKANNTE BEDROHUNGEN", 20, y)); y += 26;
        _threatPanel = new Panel
        {
            Location  = new Point(20, y),
            Size      = new Size(940, 10),   // height grows dynamically
            BackColor = C_BG
        };
        _lblNoThreats = new Label
        {
            Text      = "Keine Bedrohungen gefunden",
            Font      = F_MONO,
            ForeColor = C_MUTED,
            AutoSize  = false,
            Size      = new Size(940, 36),
            Location  = new Point(0, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _threatPanel.Controls.Add(_lblNoThreats);
        _threatPanel.Height = 40;
        inner.Controls.Add(_threatPanel);
        y += 50;

        // ── SCHUTZMODULE ────────────────────────────────────────────
        inner.Controls.Add(SectionLabel("SCHUTZMODULE", 20, y)); y += 26;
        var modules = new (string, bool)[]
        {
            ("Echtzeitschutz",    true),  ("Verhaltensanalyse", true),
            ("Phishing-Schutz",   true),  ("Ransomware-Schutz", true),
            ("USB-Überwachung",  false),  ("Dunkles Web-Alarm", false),
        };
        int mx = 20, my = y;
        for (int i = 0; i < modules.Length; i++)
        {
            var (name, on) = modules[i];
            var row = new ModuleRow(name, on)
            {
                Location = new Point(mx, my),
                Size     = new Size(460, 52)
            };
            inner.Controls.Add(row);
            if (i % 2 == 1) { mx = 20; my += 58; }
            else mx = 490;
        }
        y = my + 58 + 20;

        // ── QUARANTÄNE-VERWALTUNG ───────────────────────────────────
        inner.Controls.Add(SectionLabel("QUARANTÄNE-VERWALTUNG", 20, y)); y += 26;

        var quarListPanel = new Panel
        {
            Location  = new Point(20, y),
            Size      = new Size(940, 10),
            BackColor = C_BG,
            Tag       = "quarList"
        };
        inner.Controls.Add(quarListPanel);
        y += 14;

        inner.Height = y;

        // ── wire scroll ─────────────────────────────────────────────
        scroll.Controls.Add(inner);
        inner.Left = 0; inner.Top = 0;
        scroll.Resize += (_, _) =>
        {
            inner.Width = Math.Max(scroll.ClientSize.Width, 900);
            // stretch cards & rows
        };

        Controls.Add(scroll);
        ResumeLayout(true);

        // Initial log entry
        Log("ShieldAV bereit. Echtzeitschutz aktiv.", C_GREEN);
        if (!string.IsNullOrEmpty(ShieldAV.Engine.HashScanner.VirusTotalApiKey))
            Log("VirusTotal API aktiv ✅  –  70+ Antivirus-Engines werden genutzt.", Color.FromArgb(29, 158, 117));
        else
            Log("⚙  Tipp: Klicke 'Einstellungen' und gib deinen VirusTotal API-Key ein.", Color.FromArgb(186, 117, 23));
    }

    // ════════════════════════════════════════════════════════════════
    //  SCAN
    // ════════════════════════════════════════════════════════════════
    async void StartScan(bool quick)
    {
        if (_engine.IsScanning) return;
        _stats = new ScanStats();
        _btnQuick.Enabled  = _btnFull.Enabled = false;
        _btnCancel.Enabled = true;
        _progress.Value    = 0;
        _lblProgress.Text  = "Scan läuft…";
        ClearThreats();
        RefreshCards();

        _engine = new ScanEngine();
        _engine.FileScanned     += r     => Post(() => OnFile(r));
        _engine.ProgressChanged += (c,t) => Post(() => OnProg(c, t));
        _engine.ScanCompleted   += s     => Post(() => OnDone(s));
        _engine.StatusChanged   += s     => Post(() => Log(s, C_MUTED));

        string scanPath = quick
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Log($"{(quick?"Schnell":"Vollständiger")} Scan gestartet: {scanPath}", C_GREEN);
        try   { await _engine.StartScan([scanPath], _stats); }
        catch (Exception ex) { Post(() => { Log($"Fehler: {ex.Message}", C_RED); ResetBtns(); }); }
    }

    void OnFile(ScanResult r)
    {
        if (r.Level == ThreatLevel.Clean) return;
        bool t = r.Level == ThreatLevel.Threat;
        AddThreat(r.FileName, t ? r.ThreatName : r.Reason, r.FilePath, t);
        Log($"[{(t?"BEDROHUNG":"VERDÄCHTIG")}] {r.FilePath}", t ? C_RED : C_AMBER);
        RefreshCards(); RefreshStatus();
    }

    void OnProg(int c, int t)
    {
        if (t <= 0) return;
        // Update progress bar
        _progress.Maximum = t;
        _progress.Value   = Math.Clamp(c, 0, t);
        int pct = t > 0 ? (int)(c * 100.0 / t) : 0;
        _lblProgress.Text = $"Scanne… {c:N0} / {t:N0} Dateien  ({pct}%)";
        RefreshCards();
    }

    void OnDone(ScanStats s)
    {
        ResetBtns();
        RefreshCards(); RefreshStatus();
        string msg = s.ThreatsFound == 0
            ? $"✅ Keine Bedrohungen in {s.ScannedFiles:N0} Dateien."
            : $"⚠  {s.ThreatsFound} Bedrohung(en) in {s.ScannedFiles:N0} Dateien!";
        Log(msg, s.ThreatsFound == 0 ? C_GREEN : C_RED);
        MessageBox.Show(msg, "Scan abgeschlossen", MessageBoxButtons.OK,
            s.ThreatsFound == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    void ResetBtns()
    {
        _btnQuick.Enabled  = _btnFull.Enabled = true;
        _btnCancel.Enabled = false;
        _progress.Value    = _progress.Maximum > 0 ? _progress.Maximum : 0;
        _lblProgress.Text  = "Scan abgeschlossen";
    }

    // ════════════════════════════════════════════════════════════════
    //  THREATS PANEL
    // ════════════════════════════════════════════════════════════════
    void ClearThreats()
    {
        _threatPanel.Controls.Clear();
        _threatPanel.Controls.Add(_lblNoThreats);
        _lblNoThreats.Visible = true;
        _threatPanel.Height   = 40;
    }

    void AddThreat(string name, string reason, string path, bool isThreaat)
    {
        _lblNoThreats.Visible = false;
        int yy = _threatPanel.Controls.Count == 1 ? 0
                 : _threatPanel.Controls.OfType<ThreatRow>().Count() * 52;

        var row = new ThreatRow(name, reason, path, isThreaat)
        {
            Location = new Point(0, yy),
            Size     = new Size(940, 48)
        };
        row.QuarantineClicked += () =>
        {
            // find matching result and quarantine
            var res = new ScanResult { FilePath=path, ThreatName=reason, Level= isThreaat?ThreatLevel.Threat:ThreatLevel.Suspicious };
            if (_quarantine.Quarantine(res))
            {
                _threatPanel.Controls.Remove(row);
                // restack
                int ry = 0;
                foreach (Control c in _threatPanel.Controls.OfType<ThreatRow>())
                { c.Location=new Point(0,ry); ry+=52; }
                _threatPanel.Height = Math.Max(40, ry);
                if(!_threatPanel.Controls.OfType<ThreatRow>().Any()) _lblNoThreats.Visible=true;
                LoadQuarantine(); RefreshCards();
                Log($"Quarantäne: {path}", C_AMBER);
            }
        };
        _threatPanel.Controls.Add(row);
        _threatPanel.Height = (yy + 52) + 4;
    }

    // ════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════
    void Post(Action a)
    {
        if (_closing) return;  // check FIRST before any property access
        try
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(a);
        }
        catch { }
    }

    void LoadQuarantine()
    {
        RefreshCards();
        RebuildQuarantineList();
    }

    void RebuildQuarantineList()
    {
        // Find the quarantine list panel
        Panel? quarPanel = null;
        foreach (Control c in Controls)
            if (c is Panel scroll2)
                foreach (Control c2 in scroll2.Controls)
                    if (c2 is Panel inner2)
                        foreach (Control c3 in inner2.Controls)
                            if (c3 is Panel p && p.Tag?.ToString() == "quarList")
                            { quarPanel = p; break; }

        if (quarPanel == null) return;

        quarPanel.Controls.Clear();
        var entries = _quarantine.Entries.ToList();

        if (entries.Count == 0)
        {
            var lbl = new Label
            {
                Text      = "Keine Dateien in Quarantäne",
                Font      = F_MONO,
                ForeColor = C_MUTED,
                AutoSize  = false,
                Size      = new Size(940, 32),
                Location  = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            quarPanel.Controls.Add(lbl);
            quarPanel.Height = 36;
            return;
        }

        int qy = 0;
        foreach (var entry in entries)
        {
            var row = new Panel
            {
                Location  = new Point(0, qy),
                Size      = new Size(940, 44),
                BackColor = Color.FromArgb(40, 30, 10)
            };
            row.Paint += (s2, e2) =>
            {
                using var pen = new System.Drawing.Pen(Color.FromArgb(80, 60, 20));
                e2.Graphics.DrawRectangle(pen, 0, 0, row.Width-1, row.Height-1);
            };

            var lblName = new Label
            {
                Text      = $"🔒  {Path.GetFileName(entry.OriginalPath)}  —  {entry.ThreatName}",
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 200, 100),
                AutoSize  = false,
                Size      = new Size(680, 22),
                Location  = new Point(10, 4),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblPath = new Label
            {
                Text      = entry.OriginalPath,
                Font      = new Font("Consolas", 7.5f),
                ForeColor = C_MUTED,
                AutoSize  = false,
                Size      = new Size(680, 16),
                Location  = new Point(10, 26),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnDel = new Button
            {
                Text      = "🗑 Löschen",
                Size      = new Size(100, 28),
                Location  = new Point(728, 8),
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnDel.FlatAppearance.BorderSize = 0;
            var capturedEntry = entry;
            btnDel.Click += (_, _) =>
            {
                if (MessageBox.Show($"Datei endgültig löschen?

{capturedEntry.OriginalPath}",
                        "Löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _quarantine.Delete(capturedEntry);
                    Log($"Gelöscht: {capturedEntry.OriginalPath}", C_RED);
                    LoadQuarantine();
                }
            };

            var btnRestore = new Button
            {
                Text      = "↩ Restore",
                Size      = new Size(100, 28),
                Location  = new Point(834, 8),
                BackColor = Color.FromArgb(186, 117, 23),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnRestore.FlatAppearance.BorderSize = 0;
            var capturedEntry2 = entry;
            btnRestore.Click += (_, _) =>
            {
                if (MessageBox.Show($"Datei wiederherstellen?

{capturedEntry2.OriginalPath}

Achtung: als Bedrohung eingestuft!",
                        "Wiederherstellen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    _quarantine.Restore(capturedEntry2);
                    Log($"Wiederhergestellt: {capturedEntry2.OriginalPath}", C_AMBER);
                    LoadQuarantine();
                }
            };

            row.Controls.AddRange([lblName, lblPath, btnDel, btnRestore]);
            quarPanel.Controls.Add(row);
            qy += 48;
        }
        quarPanel.Height = qy + 4;
    }

    void RefreshCards()
    {
        _cardStatus.Value  = _stats.ThreatsFound == 0 ? "Geschützt" : "Bedroht!";
        _cardStatus.ValueColor = _stats.ThreatsFound == 0 ? C_GREEN : C_RED;
        _cardThreats.Value = _stats.ThreatsFound.ToString();
        _cardScanned.Value = _stats.ScannedFiles.ToString("N0");
        _cardQuar.Value    = (_stats.Quarantined + _quarantine.Entries.Count).ToString();
    }

    void RefreshStatus() { }

    void Log(string msg, Color col)
    {
        if (_closing) return;
        try
        {
            if (_rtfLog == null || _rtfLog.IsDisposed) return;
            // Simple trim: just clear when full. No GetFirstCharIndexFromLine = no crash.
            if (_logLines >= LOG_MAX)
            {
                _rtfLog.Clear();
                _logLines = 0;
            }
            _rtfLog.SelectionStart  = _rtfLog.TextLength;
            _rtfLog.SelectionLength = 0;
            _rtfLog.SelectionColor  = C_MUTED;
            _rtfLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ");
            _rtfLog.SelectionColor  = col;
            _rtfLog.AppendText(msg + "\n");
            _rtfLog.ScrollToCaret();
            _logLines++;
        }
        catch { }
    }

    static Label SectionLabel(string text, int x, int y) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
        ForeColor = Color.FromArgb(130, 130, 155),
        AutoSize  = true,
        Location  = new Point(x, y)
    };

    static void PaintRoundedBorder(object? s, PaintEventArgs e)
    {
        var p = (Panel)s!;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(50, 50, 68), 1f);
        using var path = RoundRect(p.ClientRectangle, 8);
        e.Graphics.DrawPath(pen, path);
    }

    static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, radius*2, radius*2, 180, 90);
        path.AddArc(r.Right-radius*2, r.Y, radius*2, radius*2, 270, 90);
        path.AddArc(r.Right-radius*2, r.Bottom-radius*2, radius*2, radius*2, 0, 90);
        path.AddArc(r.X, r.Bottom-radius*2, radius*2, radius*2, 90, 90);
        path.CloseFigure();
        return path;
    }

    static Button DarkBtn(string text, int w)
    {
        var b = new Button
        {
            Text      = text,
            Width     = w,
            Height    = 42,
            BackColor = Color.FromArgb(40, 40, 54),
            ForeColor = Color.FromArgb(220, 220, 232),
            FlatStyle = FlatStyle.Flat,
            Font      = F_BTN,
            Cursor    = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 80);
        b.FlatAppearance.BorderSize  = 1;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 72);
        return b;
    }
}

// ═══════════════════════════════════════════════════════════════════
//  HEADER PANEL — self-painting
// ═══════════════════════════════════════════════════════════════════
sealed class HeaderPanel : Panel
{
    static readonly Font FTitle = new("Segoe UI", 18f, FontStyle.Bold);
    static readonly Font FSub   = new("Segoe UI",  9f);
    static readonly Color CIcon = Color.FromArgb(29, 158, 117);

    public HeaderPanel()
    {
        BackColor = Color.FromArgb(26, 26, 36);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Icon square with rounded corners
        var iconRect = new Rectangle(0, 10, 64, 64);
        using var iconPath = RoundRect(iconRect, 12);
        using var iconBr   = new SolidBrush(CIcon);
        g.FillPath(iconBr, iconPath);
        TextRenderer.DrawText(g, "🛡", new Font("Segoe UI Emoji", 26f),
            iconRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // Title
        TextRenderer.DrawText(g, "ShieldAV", FTitle,
            new Rectangle(80, 14, 500, 36), Color.White, TextFormatFlags.Left);

        // Subtitle
        TextRenderer.DrawText(g, "Virendatenbank: v2026.06.02  ·  Letztes Update: heute",
            FSub, new Rectangle(82, 48, 600, 20),
            Color.FromArgb(130, 130, 155), TextFormatFlags.Left);

        // Separator line
        using var pen = new Pen(Color.FromArgb(50, 50, 68), 1f);
        g.DrawLine(pen, 0, Height-1, Width, Height-1);
    }

    static GraphicsPath RoundRect(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, rad*2, rad*2, 180, 90);
        p.AddArc(r.Right-rad*2, r.Y, rad*2, rad*2, 270, 90);
        p.AddArc(r.Right-rad*2, r.Bottom-rad*2, rad*2, rad*2, 0, 90);
        p.AddArc(r.X, r.Bottom-rad*2, rad*2, rad*2, 90, 90);
        p.CloseFigure();
        return p;
    }
}

// ═══════════════════════════════════════════════════════════════════
//  STAT CARD
// ═══════════════════════════════════════════════════════════════════
sealed class StatCard : Panel
{
    readonly string _label;
    string _value;
    Color  _valCol;

    public string Value      { get => _value; set { _value=value; Invalidate(); } }
    public Color ValueColor  { get => _valCol; set { _valCol=value; Invalidate(); } }

    static readonly Font FL = new("Segoe UI", 7.5f, FontStyle.Bold);
    static readonly Font FV = new("Segoe UI", 22f);
    static readonly Color CBg  = Color.FromArgb(34, 34, 46);
    static readonly Color CLbl = Color.FromArgb(130, 130, 155);

    public StatCard(string label, string value, Color valCol)
    {
        _label=label; _value=value; _valCol=valCol;
        BackColor=CBg;
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // bg + border
        g.FillRectangle(new SolidBrush(CBg), ClientRectangle);
        using var pen = new Pen(Color.FromArgb(50,50,68));
        g.DrawRectangle(pen, 0, 0, Width-1, Height-1);

        // label
        TextRenderer.DrawText(g, _label, FL,
            new Rectangle(14, 12, Width-28, 18), CLbl, TextFormatFlags.Left);
        // value
        TextRenderer.DrawText(g, _value, FV,
            new Rectangle(14, 30, Width-28, Height-34), _valCol,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  PROGRESS ROW
// ═══════════════════════════════════════════════════════════════════
sealed class ProgressRow : Panel
{
    readonly string _label;
    readonly int    _pct;
    readonly Color  _col;

    static readonly Font FL    = new("Segoe UI", 9.5f, FontStyle.Bold);
    static readonly Font FPCT  = new("Segoe UI", 9f);
    static readonly Color CBg  = Color.FromArgb(26, 26, 36);
    static readonly Color CBar = Color.FromArgb(45, 45, 58);

    public ProgressRow(string label, int pct, Color col)
    { _label=label; _pct=pct; _col=col; BackColor=CBg;
      SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int lw = 140, pw = Width - lw - 60, bh = 8, by = (Height - bh) / 2;

        // label
        TextRenderer.DrawText(g, _label, FL,
            new Rectangle(0, 0, lw, Height), Color.FromArgb(210,210,225),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        // bar bg
        using var bgBr = new SolidBrush(CBar);
        g.FillRectangle(bgBr, lw, by, pw, bh);

        // bar fill
        int fw = (int)(pw * _pct / 100.0);
        using var fBr = new SolidBrush(_col);
        g.FillRectangle(fBr, lw, by, fw, bh);

        // pct
        TextRenderer.DrawText(g, $"{_pct}%", FPCT,
            new Rectangle(Width-52, 0, 50, Height),
            Color.FromArgb(130,130,155),
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  THREAT ROW
// ═══════════════════════════════════════════════════════════════════
sealed class ThreatRow : Panel
{
    public event Action? QuarantineClicked;
    readonly string _name, _reason, _path;
    readonly bool   _threat;

    static readonly Font FN = new("Segoe UI", 9.5f, FontStyle.Bold);
    static readonly Font FR = new("Segoe UI", 8.5f);
    static readonly Color CBg  = Color.FromArgb(55, 18, 18);
    static readonly Color CTxt = Color.FromArgb(255, 140, 130);

    public ThreatRow(string name, string reason, string path, bool threat)
    {
        _name=name; _reason=reason; _path=path; _threat=threat;
        BackColor = threat ? Color.FromArgb(55,18,18) : Color.FromArgb(50,38,10);
        Cursor    = Cursors.Default;
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);

        var btn = new Button
        {
            Text      = "Quarantäne",
            Size      = new Size(100, 28),
            Location  = new Point(Width-110, 10),
            BackColor = Color.FromArgb(186,117,23),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Anchor    = AnchorStyles.Right | AnchorStyles.Top
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => QuarantineClicked?.Invoke();
        Controls.Add(btn);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color tc = _threat ? Color.FromArgb(255,140,130) : Color.FromArgb(255,210,100);
        Color bc = _threat ? Color.FromArgb(55,18,18) : Color.FromArgb(50,38,10);
        g.FillRectangle(new SolidBrush(bc), ClientRectangle);
        using var pen = new Pen(_threat ? Color.FromArgb(120,40,40) : Color.FromArgb(100,80,20));
        g.DrawRectangle(pen, 0, 0, Width-1, Height-1);
        TextRenderer.DrawText(g, (_threat?"⚠  ":"⚡  ")+_name, FN,
            new Rectangle(12, 6, Width-130, 20), tc, TextFormatFlags.Left);
        TextRenderer.DrawText(g, _reason, FR,
            new Rectangle(12, 26, Width-130, 18),
            Color.FromArgb(180,160,140), TextFormatFlags.Left);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        foreach (Control c in Controls)
            if (c is Button b) b.Location = new Point(Width-110, 10);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  MODULE ROW (toggle switch)
// ═══════════════════════════════════════════════════════════════════
sealed class ModuleRow : Panel
{
    bool _on;
    readonly string _label;
    static readonly Font FL = new("Segoe UI", 9.5f, FontStyle.Bold);
    static readonly Color CBg = Color.FromArgb(34, 34, 46);

    public ModuleRow(string label, bool on)
    {
        _label=label; _on=on;
        BackColor=CBg;
        Cursor=Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        // toggle only if click is on the right side
        if (e.X > Width - 60) { _on = !_on; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        g.FillRectangle(new SolidBrush(CBg), ClientRectangle);
        using var pen = new Pen(Color.FromArgb(50,50,68));
        g.DrawRectangle(pen, 0, 0, Width-1, Height-1);

        TextRenderer.DrawText(g, _label, FL,
            new Rectangle(12, 0, Width-80, Height),
            Color.FromArgb(210,210,225),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        // Toggle switch
        int tw=44, th=24, tx=Width-56, ty=(Height-th)/2;
        Color trackCol = _on ? Color.FromArgb(29,158,117) : Color.FromArgb(60,60,80);
        using var trackBr = new SolidBrush(trackCol);
        using var trackPath = RoundRect(new Rectangle(tx, ty, tw, th), th/2);
        g.FillPath(trackBr, trackPath);

        int thumbX = _on ? tx+tw-th+2 : tx+2;
        using var thumbBr = new SolidBrush(Color.White);
        g.FillEllipse(thumbBr, thumbX, ty+2, th-4, th-4);
    }

    static GraphicsPath RoundRect(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, rad*2, rad*2, 180, 90);
        p.AddArc(r.Right-rad*2, r.Y, rad*2, rad*2, 270, 90);
        p.AddArc(r.Right-rad*2, r.Bottom-rad*2, rad*2, rad*2, 0, 90);
        p.AddArc(r.X, r.Bottom-rad*2, rad*2, rad*2, 90, 90);
        p.CloseFigure();
        return p;
    }
}
