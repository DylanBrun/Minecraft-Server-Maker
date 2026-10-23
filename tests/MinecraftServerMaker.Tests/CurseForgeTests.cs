public class CurseForgeTests
{
    [Theory]
    [InlineData("https://www.curseforge.com/minecraft/modpacks/abyssal-ascent")]
    [InlineData("http://curseforge.com/minecraft/modpacks/abyssal-ascent")]
    [InlineData("www.curseforge.com/minecraft/modpacks/abyssal-ascent/files")]
    [InlineData("curseforge.com/minecraft/modpacks/abyssal-ascent?page=2")]
    [InlineData("HTTPS://WWW.CurseForge.com/minecraft/modpacks/Abyssal-Ascent")]
    [InlineData(" https://www.curseforge.com/minecraft/modpacks/abyssal-ascent/ ")]
    public void SlugFromLink_accepte_toutes_les_formes_de_lien(string link) =>
        Assert.Equal("abyssal-ascent", CurseForge.SlugFromLink(link));

    [Fact]
    public void SlugFromLink_ne_prend_pas_un_nom_pour_un_lien() =>
        Assert.Null(CurseForge.SlugFromLink("Abyssal Ascent"));

    [Fact]
    public void SlugFromLink_distingue_mods_et_modpacks()
    {
        const string mod = "https://www.curseforge.com/minecraft/mc-mods/jei";
        Assert.Equal("jei", CurseForge.SlugFromLink(mod, "mc-mods"));
        Assert.Null(CurseForge.SlugFromLink(mod));
    }

    [Theory]
    [InlineData("Abyssal Ascent", "abyssal-ascent")]
    [InlineData(" All the Mods 9 - ATM9 ", "all-the-mods-9-atm9")]
    [InlineData("SkyFactory 4", "skyfactory-4")]
    public void Slugify_transforme_un_nom_en_lien(string name, string slug) =>
        Assert.Equal(slug, CurseForge.Slugify(name));

    [Fact]
    public void DownloadUrl_utilise_le_lien_fourni_par_l_API()
    {
        var file = JsonNode.Parse("""{"id":4567008,"fileName":"a.jar","downloadUrl":"https://edge.forgecdn.net/files/4567/8/a.jar"}""")!;
        Assert.Equal("https://edge.forgecdn.net/files/4567/8/a.jar", CurseForge.DownloadUrl(file));
    }

    [Fact]
    public void DownloadUrl_reconstruit_le_lien_CDN_quand_l_auteur_l_a_bloque()
    {
        var file = JsonNode.Parse("""{"id":4567008,"fileName":"Mon Pack 1.0.zip","downloadUrl":null}""")!;
        Assert.Equal("https://edge.forgecdn.net/files/4567/8/Mon%20Pack%201.0.zip", CurseForge.DownloadUrl(file));
    }

    [Theory]
    [InlineData("""{"isServerPack":true}""", true)]
    [InlineData("""{"isServerPack":false,"serverPackFileId":123}""", true)]
    [InlineData("""{"isServerPack":false,"serverPackFileId":null}""", false)]
    [InlineData("""{"isServerPack":false,"serverPackFileId":0}""", false)]
    public void HasServerPack_reconnait_les_fichiers_avec_server_pack(string json, bool expected) =>
        Assert.Equal(expected, CurseForge.HasServerPack(JsonNode.Parse(json)!));
}
