// Recherche et ajout de mods dans le serveur sélectionné (version de MC et loader du serveur uniquement).
class ModsForm : Form
{
    readonly TextBox search = new() { Width = 330, PlaceholderText = "Nom, lien CurseForge ou Project ID du mod" };
    readonly Button searchBtn = new() { Text = "Rechercher", AutoSize = true };
    readonly ListBox results = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly Button installBtn = new() { Text = "Installer le mod sélectionné", AutoSize = true, Tag = "primary" };
    readonly TextBox log = new() { Dock = DockStyle.Bottom, Height = 150, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f) };

    public ModsForm(CurseForge api, string root, string mc, int loader)
    {
        Text = $"Ajouter des mods - {Path.GetFileName(root)}";
        ClientSize = new Size(520, 480);
        StartPosition = FormStartPosition.CenterParent;

        var info = new Label { Text = $"Minecraft {mc} {ServerFiles.LoaderNames[loader]} : seuls les mods compatibles sont proposés.", AutoSize = true, Margin = new Padding(3, 3, 3, 6) };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        top.Controls.AddRange([info, search, searchBtn]);
        top.SetFlowBreak(info, true);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        bottom.Controls.Add(installBtn);
        Controls.AddRange([results, bottom, log, top]); // ordre inverse du docking

        search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; searchBtn.PerformClick(); } };
        results.DoubleClick += (_, _) => installBtn.PerformClick();
        Theme.Apply(this);
        searchBtn.Click += async (_, _) => await Busy(async () =>
        {
            results.Items.Clear();
            var found = await api.Find(search.Text.Trim(), "mc-mods", 6, mc, loader);
            results.Items.AddRange(found.ToArray());
            if (found.Count > 0) results.SelectedIndex = 0;
            Log($"{found.Count} mod(s) compatible(s) trouvé(s).");
        });
        installBtn.Click += async (_, _) => await Busy(async () =>
        {
            if (results.SelectedItem is not Pack mod) throw new Exception("Sélectionnez un mod dans la liste.");
            Log($"Installation de « {mod.Name} »… (analyse des mods déjà présents)");
            var installed = await api.InstalledMods(root);
            var plan = await api.PlanInstall(mod.Id, mc, loader, installed, Log);
            if (plan.Count == 0) { Log($"« {mod.Name} » est déjà installé : rien à faire."); return; }
            await ServerFiles.DownloadMods(plan, Path.Combine(root, "mods"));
            Log($"{plan.Count} fichier(s) ajouté(s) dans mods. Redémarrez le serveur pour les charger.");
            Log("Les joueurs doivent aussi installer ces mods dans leur jeu (sauf mods réservés au serveur).");
        });
    }

    void Log(string line)
    {
        AppLog.Write("[mods] " + line);
        log.AppendText(line + Environment.NewLine);
    }

    async Task Busy(Func<Task> action)
    {
        searchBtn.Enabled = installBtn.Enabled = false;
        UseWaitCursor = true;
        try { await action(); }
        catch (Exception ex)
        {
            Log("ERREUR : " + ex.Message);
            MessageBox.Show(this, ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { searchBtn.Enabled = installBtn.Enabled = true; UseWaitCursor = false; }
    }
}
