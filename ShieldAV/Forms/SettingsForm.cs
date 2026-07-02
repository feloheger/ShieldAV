using ShieldAV.Engine;

namespace ShieldAV.Forms;

public sealed class SettingsForm : Form
{
    public SettingsForm()
    {
        Text          = "ShieldAV – Einstellungen";
        Size          = new Size(560, 280);
        MinimumSize   = new Size(500, 280);
        MaximumSize   = new Size(800, 280);
        StartPosition = FormStartPosition.CenterParent;
        BackColor     = Color.FromArgb(26, 26, 36);
        ForeColor     = Color.FromArgb(220, 220, 232);
        Font          = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox   = false;

        var green = Color.FromArgb(29, 158, 117);
        var surface = Color.FromArgb(34, 34, 46);

        // ── Title ─────────────────────────────────────────────────
        var title = new Label
        {
            Text      = "🔑  VirusTotal API-Key",
            Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize  = true,
            Location  = new Point(20, 20)
        };

        // ── Info text ─────────────────────────────────────────────
        var info = new Label
        {
            Text      = "Mit einem kostenlosen VirusTotal-Key prüft ShieldAV jede Datei\n" +
                        "gegen 70+ Antivirenprogramme gleichzeitig (500 Scans/Tag gratis).\n" +
                        "→ Kostenlos registrieren: virustotal.com",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(160, 160, 180),
            AutoSize  = false,
            Size      = new Size(510, 58),
            Location  = new Point(20, 55)
        };

        // ── Key input ─────────────────────────────────────────────
        var lblKey = new Label
        {
            Text      = "API-Key:",
            ForeColor = Color.FromArgb(180, 180, 200),
            AutoSize  = true,
            Location  = new Point(20, 126)
        };

        var txtKey = new TextBox
        {
            Text        = HashScanner.VirusTotalApiKey,
            Location    = new Point(20, 146),
            Width       = 430,
            BackColor   = surface,
            ForeColor   = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Consolas", 9f),
            UseSystemPasswordChar = false
        };

        // Show/hide toggle
        var btnShow = new Button
        {
            Text      = "👁",
            Location  = new Point(458, 144),
            Size      = new Size(70, 26),
            BackColor = surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand
        };
        btnShow.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 80);
        btnShow.Click += (_, _) =>
        {
            txtKey.UseSystemPasswordChar = !txtKey.UseSystemPasswordChar;
            btnShow.Text = txtKey.UseSystemPasswordChar ? "👁" : "🙈";
        };
        txtKey.UseSystemPasswordChar = !string.IsNullOrEmpty(txtKey.Text);

        // ── Status label ──────────────────────────────────────────
        var lblStatus = new Label
        {
            Text      = string.IsNullOrEmpty(HashScanner.VirusTotalApiKey)
                            ? "⚠  Kein API-Key gesetzt – nur MalwareBazaar aktiv"
                            : "✅  API-Key gespeichert – VirusTotal aktiv",
            ForeColor = string.IsNullOrEmpty(HashScanner.VirusTotalApiKey)
                            ? Color.FromArgb(186, 117, 23)
                            : Color.FromArgb(29, 158, 117),
            AutoSize  = true,
            Location  = new Point(20, 182)
        };

        // ── Buttons ───────────────────────────────────────────────
        var btnSave = new Button
        {
            Text      = "💾  Speichern",
            Location  = new Point(20, 210),
            Size      = new Size(140, 34),
            BackColor = green,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) =>
        {
            HashScanner.VirusTotalApiKey = txtKey.Text.Trim();
            lblStatus.Text      = string.IsNullOrEmpty(HashScanner.VirusTotalApiKey)
                                      ? "⚠  Kein API-Key gesetzt – nur MalwareBazaar aktiv"
                                      : "✅  API-Key gespeichert – VirusTotal aktiv";
            lblStatus.ForeColor = string.IsNullOrEmpty(HashScanner.VirusTotalApiKey)
                                      ? Color.FromArgb(186, 117, 23)
                                      : Color.FromArgb(29, 158, 117);
            txtKey.UseSystemPasswordChar = !string.IsNullOrEmpty(txtKey.Text);
            MessageBox.Show("Einstellungen gespeichert!", "Gespeichert",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnTest = new Button
        {
            Text      = "🔍  Verbindung testen",
            Location  = new Point(170, 210),
            Size      = new Size(180, 34),
            BackColor = Color.FromArgb(50, 50, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f),
            Cursor    = Cursors.Hand
        };
        btnTest.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 90);
        btnTest.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(txtKey.Text.Trim()))
            {
                MessageBox.Show("Bitte zuerst einen API-Key eingeben.", "Kein Key",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnTest.Enabled = false;
            btnTest.Text    = "Teste...";
            // Test with EICAR hash (standard AV test file)
            var oldKey = HashScanner.VirusTotalApiKey;
            HashScanner.VirusTotalApiKey = txtKey.Text.Trim();
            var (detected, count, total, name) = await HashScanner.CheckVirusTotal(
                "275a021bbfb6489e54d471899f7db9d1663fc695ec2fe2a2c4538aabf651fd0f");
            HashScanner.VirusTotalApiKey = oldKey;
            btnTest.Enabled = true;
            btnTest.Text    = "🔍  Verbindung testen";

            if (total > 0)
                MessageBox.Show(
                    $"✅ Verbindung erfolgreich!\nEICAR-Testdatei: {count}/{total} Engines erkannt.",
                    "Verbindung OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show(
                    "❌ Verbindung fehlgeschlagen.\nBitte API-Key prüfen.",
                    "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        var btnClose = new Button
        {
            Text      = "Schließen",
            Location  = new Point(420, 210),
            Size      = new Size(110, 34),
            BackColor = Color.FromArgb(50, 50, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 90);
        btnClose.Click += (_, _) => Close();

        Controls.AddRange([title, info, lblKey, txtKey, btnShow, lblStatus,
                           btnSave, btnTest, btnClose]);
    }
}
