public class ChatCommandTests
{
    [Theory]
    [InlineData("[14:02:11] [Server thread/INFO]: <Dylan> !ASWorld disable", "Dylan", "disable", null)]
    [InlineData("[14Oct2026 14:02:11.123] [Server thread/INFO] [minecraft/MinecraftServer]: <Dylan> !asworld enable", "Dylan", "enable", null)]
    [InlineData("[14:02:11] [Server thread/INFO]: [Not Secure] <Pote_42> !ASWorld set 45", "Pote_42", "set", "45")]
    [InlineData("[14:02:11] [Server thread/INFO]: <Dylan> !ASWorld", "Dylan", "", null)]
    public void Parse_lit_la_commande_et_son_auteur(string line, string player, string action, string? arg)
    {
        var cmd = ChatCommands.Parse(line);
        Assert.NotNull(cmd);
        Assert.Equal(new AsWorldCommand(player, action, arg), cmd);
    }

    [Theory]
    [InlineData("[14:02:11] [Server thread/INFO]: <Bob> <Dylan> !ASWorld disable")] // Bob imite Dylan dans son message
    [InlineData("[14:02:11] [Server thread/INFO]: <Bob> ]: <Dylan> !ASWorld disable")]
    [InlineData("[14:02:11] [Server thread/INFO]: <Bob> je tape !ASWorld disable")]
    [InlineData("[14:02:11] [Server thread/INFO]: Dylan joined the game")]
    [InlineData(null)]
    public void Parse_ignore_tout_le_reste(string? line) => Assert.Null(ChatCommands.Parse(line));

    [Fact]
    public void IsOp_lit_ops_json_sans_tenir_compte_des_majuscules()
    {
        using var tmp = new TempDir();
        tmp.File("ops.json", """[{"uuid":"x","name":"Dylan","level":4,"bypassesPlayerLimit":false}]""");
        Assert.True(ChatCommands.IsOp(tmp.Path, "dylan"));
        Assert.False(ChatCommands.IsOp(tmp.Path, "Bob"));
    }

    [Fact]
    public void IsOp_refuse_tout_le_monde_sans_ops_json_valide()
    {
        using var tmp = new TempDir();
        Assert.False(ChatCommands.IsOp(tmp.Path, "Dylan"));
        tmp.File("ops.json", "pas du json");
        Assert.False(ChatCommands.IsOp(tmp.Path, "Dylan"));
    }

    [Fact]
    public void Tell_echappe_le_texte_en_JSON()
    {
        var cmd = ChatCommands.Tell("Dylan", "Intervalle \"30\" min");
        Assert.StartsWith("tellraw Dylan {", cmd);
        var json = JsonNode.Parse(cmd["tellraw Dylan ".Length..])!;
        Assert.Equal("[ASWorld] Intervalle \"30\" min", json["text"]!.GetValue<string>());
    }
}
