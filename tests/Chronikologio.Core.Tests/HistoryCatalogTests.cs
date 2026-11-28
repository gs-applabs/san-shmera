using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class HistoryCatalogTests
{
    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir,"data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"),"data");
    }

    [Fact]
    public void ExistingLibraryIsProjectedWithoutInventingDatesOrDuplicates()
    {
        var store=DayStore.Load(FindData());
        Assert.Empty(store.Errors);
        var catalog=new HistoryCatalog(store,"el");
        var first=catalog.Search("","all",null,null,false);
        Assert.True(first.Total>100);
        Assert.Equal(100,first.Entries.Count);
        var second=catalog.Search("","all",null,null,false,100);
        Assert.Equal(first.Total,second.Total);
        Assert.Empty(first.Entries.Select(x=>x.Key).Intersect(second.Entries.Select(x=>x.Key)));
        var region=catalog.Regions.First();
        var regional=catalog.Search("","all",null,null,false,0,100,region);
        Assert.True(regional.Total>0);
        Assert.All(regional.Entries,x=>Assert.Equal(region,x.Region));
        Assert.Equal(first.Entries[0],catalog.FindById(first.Entries[0].Key));
        Assert.Null(catalog.FindById("absent"));
        var births=catalog.Search("","birth",null,null,false,0,500);
        Assert.True(births.Total>0);
        Assert.All(births.Entries,x=>{Assert.Equal("birth",x.Kind);Assert.Null(x.Status);});
        var verified=catalog.Search("","all",null,null,true,0,500);
        Assert.All(verified.Entries,x=>Assert.Equal(Verification.Verified,x.Status));
        var dated=catalog.Search("","event",1900,1999,false,0,500);
        Assert.All(dated.Entries,x=>Assert.InRange(x.Year!.Value,1900,1999));
        Assert.Throws<ArgumentException>(()=>catalog.Search("","all",2000,1900,false));
    }
    [Fact]
    public void QuizUsesOnlyVerifiedDatedEventsAndDistinctAnswerYears()
    {
        var store=DayStore.Load(FindData());
        Assert.Empty(store.Errors);
        var catalog=new HistoryCatalog(store,"el");
        var questions=catalog.CreateYearQuiz(10,new Random(42));
        Assert.InRange(questions.Count,1,10);
        Assert.Equal(questions.Count,questions.Select(x=>x.Evidence.Key).Distinct().Count());
        foreach(var q in questions)
        {
            Assert.Equal(Verification.Verified,q.Evidence.Status);
            Assert.Equal("event",q.Evidence.Kind);
            Assert.Equal(q.Evidence.Year,q.AnswerYear);
            Assert.Equal(4,q.Choices.Distinct().Count());
            Assert.Contains(q.AnswerYear,q.Choices);
            Assert.Contains(q.Evidence.Sources,x=>Uri.TryCreate(x.Url,UriKind.Absolute,out var uri)&&uri.Scheme=="https");
            Assert.Equal(q.Evidence,catalog.FindById(q.Evidence.Key));
        }
        Assert.Empty(catalog.CreateYearQuiz(10,new Random(42),"nonexistent-region"));
        Assert.Throws<ArgumentOutOfRangeException>(()=>catalog.CreateYearQuiz(21,new Random(42)));
    }

}
