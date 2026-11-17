using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class NamesCoverageTests
{
    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static readonly DayStore Store = DayStore.Load(FindData(), "gr");

    private static IEnumerable<(int M, int D)> Days() { for (var m = 1; m <= 12; m++) for (var d = 1; d <= DateTime.DaysInMonth(2024, m); d++) yield return (m, d); }

    [Fact]
    public void NoEtymologyIsEmpty()
    {
        var bad = new List<string>();
        foreach (var (m, d) in Days())
            foreach (var n in Store.GetDayIn(m, d, "el").Names)
                if (string.IsNullOrWhiteSpace(n.Etymology.Origin) || string.IsNullOrWhiteSpace(n.Etymology.Meaning)) bad.Add($"{m:00}-{d:00} {n.Name}");
        Assert.True(bad.Count == 0, bad.Count + ": " + string.Join(", ", bad.Take(30)));
    }

    [Fact]
    public void VariantsDoNotRepeatTheMainName()
    {
        var bad = new List<string>();
        foreach (var (m, d) in Days())
            foreach (var n in Store.GetDayIn(m, d, "el").Names)
                if (n.Variants.Any(v => string.Equals(v, n.Name, StringComparison.OrdinalIgnoreCase))) bad.Add($"{m:00}-{d:00} {n.Name}");
        Assert.True(bad.Count == 0, bad.Count + ": " + string.Join(", ", bad.Take(30)));
    }

    [Fact]
    public void EveryDayHasANameOrIsListed()
    {
        var empty = Days().Where(x => Store.GetDayIn(x.M, x.D, "el").Names.Any() == false).Select(x => $"{x.M:00}-{x.D:00}").ToList();
        Assert.True(empty.Count == 0, empty.Count + " days: " + string.Join(" ", empty));
    }

    [Theory]
    [InlineData(5, 21, "Κωνσταντίνος")] [InlineData(5, 21, "Ελένη")] [InlineData(10, 26, "Δημήτριος")] [InlineData(4, 23, "Γεώργιος")] [InlineData(12, 6, "Νικόλαος")]
    [InlineData(8, 15, "Μαρία")] [InlineData(8, 15, "Παναγιώτης")] [InlineData(6, 29, "Πέτρος")] [InlineData(6, 29, "Παύλος")] [InlineData(11, 8, "Μιχαήλ")] [InlineData(11, 8, "Γαβριήλ")]
    [InlineData(11, 25, "Αικατερίνη")] [InlineData(1, 17, "Αντώνιος")] [InlineData(12, 12, "Σπυρίδων")] [InlineData(12, 25, "Χρήστος")] [InlineData(12, 25, "Χριστίνα")]
    [InlineData(1, 1, "Βασίλειος")] [InlineData(1, 18, "Αθανάσιος")] [InlineData(9, 14, "Σταύρος")] [InlineData(11, 30, "Ανδρέας")]
    public void MajorNameDaysAreOnTheirDate(int m, int d, string name)
        => Assert.Contains(Store.GetDayIn(m, d, "el").Names, n => n.Name.StartsWith(name, StringComparison.Ordinal) || n.Variants.Any(v => v.StartsWith(name, StringComparison.Ordinal)));
}
