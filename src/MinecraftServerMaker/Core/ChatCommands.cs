// "!ASWorld enable|disable|set <minutes>" tapé dans le chat par un opérateur.
// Minecraft n'accepte pas de nouvelle commande "/" sans mod : l'app lit donc le chat dans la console du serveur.
record AsWorldCommand(string Player, string Action, string? Argument);

static class ChatCommands
{
    // Le pseudo doit être le premier "<...>" après le préfixe du log ("[…]: <Dylan> !ASWorld …") :
    // un joueur ne peut pas se faire passer pour un autre en écrivant "<Dylan> !ASWorld" dans son propre message.
    static readonly Regex AsWorld = new(@"^[^<]*?\]: (?:\[Not Secure\] )?<(\w{3,16})> !asworld(?:\s+(\S+))?(?:\s+(\S+))?\s*$", RegexOptions.IgnoreCase);

    public static AsWorldCommand? Parse(string? line)
    {
        var m = line == null ? null : AsWorld.Match(line);
        return m is { Success: true } ? new(m.Groups[1].Value, m.Groups[2].Value.ToLowerInvariant(), m.Groups[3].Success ? m.Groups[3].Value : null) : null;
    }

    // Opérateur = présent dans ops.json, quel que soit son niveau.
    public static bool IsOp(string root, string player)
    {
        try
        {
            var ops = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "ops.json")))?.AsArray();
            return ops?.Any(o => string.Equals(o?["name"]?.GetValue<string>(), player, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { return false; }
    }

    // Applique une commande !ASWorld aux réglages du serveur. Changed = les réglages ont été modifiés et doivent être enregistrés.
    public static (string Reply, bool Changed) Apply(AsWorldCommand cmd, bool isOp, ServerConfig config)
    {
        if (!isOp) return ("Commande réservée aux opérateurs.", false);
        switch (cmd.Action)
        {
            case "enable": config.AutoBackup = true; break;
            case "disable": config.AutoBackup = false; break;
            case "set" when int.TryParse(cmd.Argument, out var minutes) && minutes is >= 1 and <= 1440: config.BackupMinutes = minutes; break;
            case "set": return ("Utilisation : !ASWorld set <minutes>, entre 1 et 1440.", false);
            default:
                return ($"Sauvegardes {(config.AutoBackup ? "activées" : "désactivées")}, toutes les {config.BackupMinutes} min. " +
                        "Commandes : !ASWorld enable, !ASWorld disable, !ASWorld set <minutes>.", false);
        }
        var state = config.AutoBackup ? $"activées, toutes les {config.BackupMinutes} min" : "désactivées";
        return ($"Sauvegardes automatiques {state}.", true);
    }

    // Message privé en jeu (tellraw existe depuis Minecraft 1.7).
    public static string Tell(string player, string text) =>
        $"tellraw {player} {JsonSerializer.Serialize(new { text = "[ASWorld] " + text, color = "gold" })}";
}
