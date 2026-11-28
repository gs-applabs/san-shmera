using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class HistoryCatalogTests
{
    [Fact]
    public void ExistingLibraryIsProjectedWithoutInventingDatesOrDuplicates()
    {
        var store=DayStore.Load(Path.Combine(AppContext.BaseDirectory,"data"));
        var catalog=new HistoryCatalog(store,"el");
        var first=catalog.Search("","all",null,null,false);
        Assert.True(first.Total>100);
        Assert.Equal(100,first.Entries.Count);
        var second=catalog.Search("","all",null,null,false,100);
        Assert.Equal(first.Total,second.Total);
        Assert.Empty(first.Entries.Select(x=>x.Key).Intersect(second.Entries.Select(x=>x.Key)));
        var births=catalog.Search("","birth",null,null,false,0,500);
        Assert.True(births.Total>0);
        Assert.All(births.Entries,x=>{Assert.Equal("birth",x.Kind);Assert.Null(x.Status);});
        var verified=catalog.Search("","all",null,null,true,0,500);
        Assert.All(verified.Entries,x=>Assert.Equal(Verification.Verified,x.Status));
        var dated=catalog.Search("","event",1900,1999,false,0,500);
        Assert.All(dated.Entries,x=>Assert.InRange(x.Year!.Value,1900,1999));
        Assert.Throws<ArgumentException>(()=>catalog.Search("","all",2000,1900,false));
    }
}
