namespace SampleFailingProject;

// Two test classes so we can test filter-by-class scenarios

public class MathTests
{
    [Fact] public void Add_ShouldPass()  => Assert.Equal(4, 2 + 2);
    [Fact] public void Mul_ShouldPass()  => Assert.Equal(6, 2 * 3);

    [Fact]
    public void Add_ShouldFail()
    {
        // deliberate: wrong expected value
        Assert.Equal(99, 1 + 1);
    }
}

public class StringTests
{
    [Fact] public void Concat_ShouldPass()    => Assert.Equal("ab", "a" + "b");
    [Fact] public void Length_ShouldPass()    => Assert.Equal(5,    "hello".Length);

    [Fact]
    public void Contains_ShouldFail()
    {
        // deliberate: "hello" does not contain "xyz"
        Assert.Contains("xyz", "hello");
    }
}
