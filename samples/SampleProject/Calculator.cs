namespace SampleProject;

public static class Calculator
{
    public static int Add(int a, int b) => a + b;
    public static int Subtract(int a, int b) => a - b;
    public static int Multiply(int a, int b) => a * b;

    public static double Divide(int a, int b)
    {
        if (b == 0)
            throw new DivideByZeroException("Cannot divide by zero.");
        return (double)a / b;
    }

    public static string Grade(int score)
    {
        if (score < 0 || score > 100)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");

        return score switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _     => "F",
        };
    }
}
