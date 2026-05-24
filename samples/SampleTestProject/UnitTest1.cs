using SampleProject;

namespace SampleTestProject;

public class CalculatorTests
{
    [Fact] public void Add_ReturnsSum()                      => Assert.Equal(5,   Calculator.Add(2, 3));
    [Fact] public void Subtract_ReturnsDifference()          => Assert.Equal(1,   Calculator.Subtract(3, 2));
    [Fact] public void Multiply_ReturnsProduct()             => Assert.Equal(6,   Calculator.Multiply(2, 3));
    [Fact] public void Divide_ReturnsQuotient()              => Assert.Equal(2.5, Calculator.Divide(5, 2));
    [Fact] public void Divide_ByZero_ThrowsDivideByZero()    => Assert.Throws<DivideByZeroException>(() => Calculator.Divide(5, 0));

    // Grade — intentionally only cover A/B/F branches, leaving C and D uncovered
    // so get_coverage_summary shows a real partial coverage number
    [Fact] public void Grade_95_ReturnsA()  => Assert.Equal("A", Calculator.Grade(95));
    [Fact] public void Grade_85_ReturnsB()  => Assert.Equal("B", Calculator.Grade(85));
    [Fact] public void Grade_50_ReturnsF()  => Assert.Equal("F", Calculator.Grade(50));
    [Fact] public void Grade_Negative_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculator.Grade(-1));
}

public class OrderProcessorTests
{
    [Fact]
    public void Process_SmallAmount_ReturnsCompleted()
    {
        var processor = new OrderProcessor();
        var result = processor.Process("ORD-001", 99.99m);
        Assert.Equal("COMPLETED:ORD-001", result);
        Assert.Equal(1, processor.ProcessedCount);
    }

    [Fact]
    public void Process_LargeAmount_ReturnsPendingApproval()
    {
        var processor = new OrderProcessor();
        var result = processor.Process("ORD-002", 15_000m);
        Assert.Equal("PENDING_APPROVAL:ORD-002", result);
    }

    [Fact]
    public void Process_NegativeAmount_ReturnsRejected()
    {
        var processor = new OrderProcessor();
        var result = processor.Process("ORD-003", -50m);
        Assert.StartsWith("REJECTED:", result);
        Assert.Equal(0, processor.ProcessedCount);
    }

    [Fact]
    public void Process_EmptyOrderId_Throws()
    {
        var processor = new OrderProcessor();
        Assert.Throws<ArgumentException>(() => processor.Process("", 100m));
    }
}
