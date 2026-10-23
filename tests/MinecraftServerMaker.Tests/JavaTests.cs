public class JavaTests
{
    [Theory]
    [InlineData("1.7.10", 8)]
    [InlineData("1.12.2", 8)]
    [InlineData("1.16.5", 8)]
    [InlineData("1.17.1", 17)]
    [InlineData("1.18.2", 17)]
    [InlineData("1.20.1", 17)]
    [InlineData("1.20.4", 17)]
    [InlineData("1.20.5", 21)]
    [InlineData("1.21", 21)]
    [InlineData("1.21.1", 21)]
    [InlineData("26.1", 25)]
    public void MajorFor_donne_la_version_de_Java_attendue_par_Minecraft(string mc, int major) =>
        Assert.Equal(major, Java.MajorFor(mc));

    [Fact]
    public void MajorFor_prend_le_Java_le_plus_recent_pour_une_version_illisible() =>
        Assert.Equal(25, Java.MajorFor("snapshot-x"));
}
