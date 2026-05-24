namespace SampleBrokenProject;

public static class BrokenCode
{
    // CS0029: cannot convert string to int
    public static int GetValue() => "this is not an int";

    // CS0103: name does not exist in current context
    public static void UseUndefined() { var x = undefinedVariable; }

    // CS0161: not all code paths return a value
    public static string GetStatus(bool flag)
    {
        if (flag)
            return "yes";
        // missing return for false branch
    }
}
