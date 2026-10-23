// Clé API chiffrée, empreintes CurseForge et versions de loader lues dans les modpacks.
public class SecurityTests
{
    [Fact]
    public void La_cle_API_est_chiffree_puis_relue()
    {
        var stored = new Settings { ApiKey = "$2a$10$cle-de-test" }.ApiKeyProtected;
        Assert.DoesNotContain("cle-de-test", stored);
        Assert.Equal("$2a$10$cle-de-test", new Settings { ApiKeyProtected = stored }.ApiKey);
    }

    [Fact]
    public void La_cle_API_n_est_jamais_ecrite_en_clair() =>
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(new Settings { ApiKey = "secret" }));

    [Fact]
    public void Une_ancienne_cle_en_clair_est_encore_lue() =>
        Assert.Equal("ancienne", JsonSerializer.Deserialize<Settings>("""{"ApiKey":"ancienne"}""")!.ApiKey);

    [Fact]
    public void Une_cle_chiffree_illisible_est_ignoree_sans_planter() =>
        Assert.Equal("", new Settings { ApiKeyProtected = "pas-du-base64" }.ApiKey);

    [Theory]
    [InlineData("", 0x5bd15e36u)]
    [InlineData("abc", 1621425345u)]
    [InlineData("Minecraft", 2572622835u)]
    [InlineData("hello world 123", 3055458430u)]
    public void Fingerprint_calcule_l_empreinte_CurseForge(string content, uint expected) =>
        Assert.Equal(expected, Fingerprint.Of(System.Text.Encoding.UTF8.GetBytes(content)));

    [Fact]
    public void Fingerprint_ignore_espaces_tabulations_et_retours_a_la_ligne() =>
        Assert.Equal(Fingerprint.Of("abc"u8.ToArray()), Fingerprint.Of("a b\r\n\tc"u8.ToArray()));

    [Theory]
    [InlineData("47.4.6", "1.20.1")]
    [InlineData("0.16.10", "1.21")]
    [InlineData("21.1.77-beta", "1.21.1")]
    public void LoaderPartsValid_accepte_les_vraies_versions(string ver, string mc) =>
        Assert.True(ServerBuilder.LoaderPartsValid(ver, mc));

    [Theory]
    [InlineData("../../evil", "1.20.1")]
    [InlineData("47.4.6", "1.20.1/../x")]
    [InlineData("1?x=y", "1.20.1")]
    [InlineData("", "1.20.1")]
    public void LoaderPartsValid_refuse_les_versions_piegees(string ver, string mc) =>
        Assert.False(ServerBuilder.LoaderPartsValid(ver, mc));
}
