using System.Net;

public class DownloadTests
{
    // Répond toujours le même contenu, sans réseau.
    sealed class FakeServer(byte[] body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
    }

    static readonly byte[] Content = "contenu du mod"u8.ToArray();
    const string GoodSha1 = "c33c4c09f327b618ececd50c8a07191e295f46b5";

    [Fact]
    public async Task Download_ecrit_le_fichier_quand_le_SHA1_correspond()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "mod.jar");
        await ServerFiles.Download(new HttpClient(new FakeServer(Content)), "https://exemple/mod.jar", path, GoodSha1.ToUpperInvariant());
        Assert.Equal(Content, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".part"));
    }

    [Fact]
    public async Task Download_refuse_un_fichier_modifie_et_ne_laisse_rien()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "mod.jar");
        await Assert.ThrowsAsync<IOException>(() =>
            ServerFiles.Download(new HttpClient(new FakeServer(Content)), "https://exemple/mod.jar", path, new string('0', 40)));
        Assert.Empty(Directory.GetFiles(tmp.Path));
    }

    [Fact]
    public async Task Download_ne_remplace_pas_l_ancien_fichier_si_le_nouveau_est_mauvais()
    {
        using var tmp = new TempDir();
        var path = tmp.File("mod.jar", "ancienne version");
        await Assert.ThrowsAsync<IOException>(() =>
            ServerFiles.Download(new HttpClient(new FakeServer(Content)), "https://exemple/mod.jar", path, new string('0', 40)));
        Assert.Equal("ancienne version", File.ReadAllText(path));
    }

    [Fact]
    public async Task Download_sans_SHA1_connu_ecrit_le_fichier()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "mod.jar");
        await ServerFiles.Download(new HttpClient(new FakeServer(Content)), "https://exemple/mod.jar", path, null);
        Assert.Equal(Content, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task Download_en_erreur_HTTP_ne_laisse_aucun_fichier()
    {
        using var tmp = new TempDir();
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            ServerFiles.Download(new HttpClient(new FakeServer([], HttpStatusCode.InternalServerError)), "https://exemple/mod.jar", Path.Combine(tmp.Path, "mod.jar"), null));
        Assert.Empty(Directory.GetFiles(tmp.Path));
    }

    [Fact]
    public void Sha1Of_lit_l_empreinte_SHA1_parmi_les_autres()
    {
        Assert.Equal("abc123", ServerFiles.Sha1Of(JsonNode.Parse("""{"hashes":[{"value":"md5","algo":2},{"value":"abc123","algo":1}]}""")!));
        Assert.Null(ServerFiles.Sha1Of(JsonNode.Parse("""{"hashes":[]}""")!));
        Assert.Null(ServerFiles.Sha1Of(JsonNode.Parse("""{}""")!));
    }
}
