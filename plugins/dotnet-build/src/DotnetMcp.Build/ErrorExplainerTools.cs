using System.ComponentModel;
using DotnetMcp.Core.Models;
using ModelContextProtocol.Server;

namespace DotnetMcp.Build;

[McpServerToolType]
public static class ErrorExplainerTools
{
    [McpServerTool(Name = "explain_build_error", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Explain a C# compiler (CS) or MSBuild (MSB) error code in plain English. " +
        "Returns the error title, what it means, common causes, and a suggested fix. " +
        "Use this immediately after build_project returns errors to avoid a web search.")]
    public static Task<BuildErrorExplanation> ExplainBuildError(
        [Description("The error code to explain, e.g. CS0103, CS1061, MSB4019")] string errorCode,
        [Description("The full error message from the build output (optional — provides extra context)")] string? message = null,
        CancellationToken cancellationToken = default)
    {
        var code = errorCode.Trim().ToUpperInvariant();

        if (KnownErrors.TryGetValue(code, out var known))
            return Task.FromResult(known with { Code = code, IsKnown = true });

        // Unknown code — give a generic fallback based on prefix
        var fallback = code.StartsWith("CS")  ? GenericCsError(code, message) :
                       code.StartsWith("MSB") ? GenericMsbError(code, message) :
                                                GenericUnknown(code, message);

        return Task.FromResult(fallback);
    }

    // -------------------------------------------------------------------------
    // Lookup table — most common C# and MSBuild errors
    // -------------------------------------------------------------------------

    private static readonly Dictionary<string, BuildErrorExplanation> KnownErrors = new()
    {
        // --- Type / conversion errors ---
        ["CS0029"] = new()
        {
            Title       = "Cannot implicitly convert type",
            Explanation = "You're assigning a value to a variable of an incompatible type without an explicit cast.",
            CommonCauses = ["Assigning int to string (or vice versa)", "Returning the wrong type from a method", "Passing wrong argument type to a method"],
            ExampleFix  = "Add an explicit cast: `int x = (int)myDouble;` or use a conversion method like `.ToString()`.",
        },
        ["CS0266"] = new()
        {
            Title       = "Cannot implicitly convert type (explicit cast exists)",
            Explanation = "An explicit cast exists between the two types but you haven't written it.",
            CommonCauses = ["Narrowing numeric conversions (long → int)", "Casting between related class types"],
            ExampleFix  = "Add the cast explicitly: `int x = (int)longValue;`",
        },

        // --- Name / scope errors ---
        ["CS0103"] = new()
        {
            Title       = "Name does not exist in the current context",
            Explanation = "You're referencing a variable, method, or type that isn't visible in the current scope.",
            CommonCauses = ["Missing `using` directive for a namespace", "Typo in identifier name", "Variable declared inside an `if` block used outside it", "Static member accessed on an instance"],
            ExampleFix  = "Add the missing `using`, fix the typo, or move the variable declaration to the correct scope.",
        },
        ["CS0117"] = new()
        {
            Title       = "Type does not contain a definition for member",
            Explanation = "You're calling a method or accessing a property that doesn't exist on the type.",
            CommonCauses = ["Typo in member name", "Member exists on a different overload or base class", "Using a method that was renamed or removed in a newer package version"],
            ExampleFix  = "Check spelling, verify the member exists on the actual runtime type, and check package changelogs if you recently upgraded.",
        },
        ["CS0234"] = new()
        {
            Title       = "Type or namespace does not exist in the namespace",
            Explanation = "A type or sub-namespace you're referencing doesn't exist in the specified namespace.",
            CommonCauses = ["Missing NuGet package reference", "Wrong namespace — type moved between packages", "Typo in the namespace path"],
            ExampleFix  = "Add the NuGet package containing the type, or fix the namespace in your `using` directive.",
        },
        ["CS0246"] = new()
        {
            Title       = "Type or namespace name could not be found",
            Explanation = "The compiler can't find the type at all — it's either missing a `using`, a package reference, or the type simply doesn't exist.",
            CommonCauses = ["Missing `using` directive", "Missing NuGet `<PackageReference>` in .csproj", "Typo in type name", "Type is in a different target framework"],
            ExampleFix  = "Add `using TheNamespace;` at the top, or add the NuGet package via `dotnet add package PackageName`.",
        },
        ["CS1061"] = new()
        {
            Title       = "Type does not contain a definition (no accessible extension method either)",
            Explanation = "The method or property doesn't exist on the type, and no extension method matching that name is in scope.",
            CommonCauses = ["Missing `using` for an extension method (e.g. LINQ needs `using System.Linq`)", "Calling a method on the wrong type", "Using a method from a package that isn't referenced"],
            ExampleFix  = "Add the missing `using` directive for the extension method, or verify the method name on the actual type.",
        },

        // --- Accessibility / static errors ---
        ["CS0120"] = new()
        {
            Title       = "Object reference required for non-static field or method",
            Explanation = "You're accessing an instance member (field, property, or method) without an instance — you can't call it on the class name directly.",
            CommonCauses = ["Calling an instance method from a static method without `new`", "Forgetting to create an instance of the class first"],
            ExampleFix  = "Create an instance first: `var obj = new MyClass(); obj.MyMethod();` — or make the member `static` if it doesn't need instance state.",
        },
        ["CS0122"] = new()
        {
            Title       = "Inaccessible due to protection level",
            Explanation = "You're trying to access a `private` or `protected` member from outside the class that owns it.",
            CommonCauses = ["Accessing a `private` field from another class", "Calling a `protected` method from outside the class hierarchy"],
            ExampleFix  = "Change the access modifier to `public` or `internal`, or expose the value through a public property.",
        },

        // --- Null-safety errors (C# 8+ nullable reference types) ---
        ["CS8600"] = new()
        {
            Title       = "Converting null to non-nullable type",
            Explanation = "You're assigning a potentially-null value to a variable declared as non-nullable.",
            CommonCauses = ["Assigning a nullable variable to a non-nullable one without a null check", "Return value of a method that could return null assigned to non-nullable"],
            ExampleFix  = "Use the null-coalescing operator: `string s = maybeNull ?? string.Empty;` — or declare the variable as nullable: `string? s`.",
        },
        ["CS8602"] = new()
        {
            Title       = "Dereference of a possibly null reference",
            Explanation = "You're accessing a member on a variable that the compiler thinks might be null.",
            CommonCauses = ["Not checking for null before accessing a property or method", "Variable is of a nullable type (`string?`) but used without null guard"],
            ExampleFix  = "Add a null check: `if (obj is not null) obj.Method();` — or use null-conditional: `obj?.Method();`",
        },
        ["CS8604"] = new()
        {
            Title       = "Possible null reference argument",
            Explanation = "You're passing a possibly-null value to a parameter that's declared as non-nullable.",
            CommonCauses = ["Passing a nullable variable to a method that expects non-nullable", "Not null-checking before passing to a method"],
            ExampleFix  = "Assert non-null with `ArgumentNullException.ThrowIfNull(arg)` before the call, or use `arg!` if you're certain it's not null.",
        },
        ["CS8618"] = new()
        {
            Title       = "Non-nullable property not initialized",
            Explanation = "A non-nullable property or field isn't guaranteed to be set in the constructor, so it could be null at runtime.",
            CommonCauses = ["Missing constructor initialization for a required property", "Using `required` keyword without setting it in object initializer"],
            ExampleFix  = "Initialize it in the constructor, mark it `required`, set a default value `= \"\";`, or declare it nullable `string?`.",
        },
        ["CS8625"] = new()
        {
            Title       = "Cannot convert null literal to non-nullable reference type",
            Explanation = "You're explicitly passing `null` where a non-nullable type is expected.",
            CommonCauses = ["Passing `null` to a method parameter typed as non-nullable", "Returning `null` from a method returning a non-nullable type"],
            ExampleFix  = "Change the parameter/return type to nullable (`string?`), or avoid passing null by providing a real value.",
        },

        // --- Async / await ---
        ["CS4014"] = new()
        {
            Title       = "Call is not awaited — execution continues before call completes",
            Explanation = "You're calling an async method but not awaiting it, so exceptions are silently swallowed and execution doesn't wait for completion.",
            CommonCauses = ["Forgot to add `await` before an async call", "Intentionally fire-and-forget (but the warning still fires)"],
            ExampleFix  = "Add `await`: `await DoWorkAsync();` — or to suppress intentionally: `_ = DoWorkAsync();`",
        },

        // --- Return / flow control ---
        ["CS0161"] = new()
        {
            Title       = "Not all code paths return a value",
            Explanation = "Your method has a return type but at least one code path exits without returning a value.",
            CommonCauses = ["Missing `return` in an `if/else` branch", "Switch statement missing a default case that returns"],
            ExampleFix  = "Add a `return` statement to every branch, or add a `throw new NotImplementedException()` as a fallback.",
        },
        ["CS0165"] = new()
        {
            Title       = "Use of possibly unassigned local variable",
            Explanation = "You're reading a local variable that might not have been assigned on all code paths before this point.",
            CommonCauses = ["Variable declared but only assigned inside an `if` block", "Conditional assignment missing an `else` branch"],
            ExampleFix  = "Initialize the variable when you declare it: `string result = string.Empty;`",
        },

        // --- Member / interface / override ---
        ["CS0200"] = new()
        {
            Title       = "Cannot assign to property because it is read-only",
            Explanation = "You're trying to set a property that only has a getter (no setter).",
            CommonCauses = ["Property declared with `get` only", "Init-only property (`init`) being set outside an object initializer or constructor"],
            ExampleFix  = "Add a `set;` accessor, change to `init;` and set it in the object initializer, or make the field public instead.",
        },
        ["CS0535"] = new()
        {
            Title       = "Does not implement interface member",
            Explanation = "Your class claims to implement an interface but is missing one or more of the required members.",
            CommonCauses = ["Interface updated with a new method that wasn't added to implementing classes", "Typo in the method signature that doesn't match the interface"],
            ExampleFix  = "In Visual Studio/Rider, use 'Implement interface' quick-fix. Otherwise, add the missing method with the exact signature from the interface.",
        },
        ["CS1612"] = new()
        {
            Title       = "Cannot modify return value — it is not a variable",
            Explanation = "You're trying to modify a property of a struct returned from a property or method, but since structs are value types the return is a copy.",
            CommonCauses = ["Modifying a field on a struct returned from a property (`obj.Point.X = 5`)"],
            ExampleFix  = "Copy the struct to a local variable first, modify the copy, then assign it back: `var p = obj.Point; p.X = 5; obj.Point = p;`",
        },

        // --- Syntax errors ---
        ["CS1002"] = new()
        {
            Title       = "Semicolon expected",
            Explanation = "The compiler expected a `;` at this position.",
            CommonCauses = ["Missing semicolon at end of statement", "Typo that pushed the parser out of sync — the real error may be on the previous line"],
            ExampleFix  = "Add the missing `;`. If the location seems wrong, check the line above for a missing semicolon or brace.",
        },
        ["CS1003"] = new()
        {
            Title       = "Syntax error",
            Explanation = "The parser hit something it didn't expect. Often a cascade error from an earlier typo.",
            CommonCauses = ["Missing or mismatched bracket/brace/parenthesis earlier in the file", "Typo in keyword"],
            ExampleFix  = "Look at the lines before the reported location — a missing `{`, `}`, or `(` on a previous line often causes cascade syntax errors.",
        },
        ["CS1501"] = new()
        {
            Title       = "No overload takes N arguments",
            Explanation = "You're calling a method with the wrong number of arguments.",
            CommonCauses = ["Passing too many or too few arguments", "Using a method signature that changed in a newer version"],
            ExampleFix  = "Check the method's signature (hover in IDE or check docs) and match the argument count.",
        },
        ["CS1503"] = new()
        {
            Title       = "Argument cannot convert from type A to type B",
            Explanation = "One of the arguments you're passing has an incompatible type for that parameter position.",
            CommonCauses = ["Passing a string where an int is expected (or vice versa)", "Passing null to a non-nullable parameter", "Argument order swapped"],
            ExampleFix  = "Cast or convert the argument to the expected type, or reorder the arguments.",
        },
        ["CS1729"] = new()
        {
            Title       = "Type does not contain a constructor that takes N arguments",
            Explanation = "You're calling `new SomeType(...)` with arguments that don't match any available constructor.",
            CommonCauses = ["Constructor signature changed in a library update", "Typo — calling the wrong constructor overload", "Missing required argument"],
            ExampleFix  = "Check the constructors available on the type and match argument count/types exactly.",
        },
        ["CS1674"] = new()
        {
            Title       = "Type used in 'using' statement must implement IDisposable",
            Explanation = "The `using` statement only works with types that implement `IDisposable`.",
            CommonCauses = ["Using a type in a `using` block that doesn't have a `Dispose()` method", "Using `IAsyncDisposable` type with `using` instead of `await using`"],
            ExampleFix  = "Use `await using` for `IAsyncDisposable` types, or remove the `using` if the type doesn't need disposal.",
        },
        ["CS1579"] = new()
        {
            Title       = "foreach cannot operate on type — no public GetEnumerator",
            Explanation = "The type you're iterating over doesn't implement `IEnumerable` or doesn't have a public `GetEnumerator()` method.",
            CommonCauses = ["Trying to `foreach` over a non-collection type", "Missing `using System.Collections.Generic`", "Custom type not implementing the enumerable pattern"],
            ExampleFix  = "Make sure the type implements `IEnumerable<T>`, or convert it first: `.ToList()`, `.ToArray()`.",
        },

        // --- Unused / unread ---
        ["CS0649"] = new()
        {
            Title       = "Field is never assigned — will always have default value",
            Explanation = "A private field is declared but never written to, so it always holds its default value (null, 0, false).",
            CommonCauses = ["Forgot to assign the field in the constructor or a method", "Field is leftover from deleted code"],
            ExampleFix  = "Assign the field, remove it, or suppress the warning with `#pragma warning disable CS0649` if intentional.",
        },

        // --- MSBuild errors ---
        ["MSB4019"] = new()
        {
            Title       = "Imported project not found",
            Explanation = "A `<Import>` in your .csproj references a `.targets` or `.props` file that doesn't exist at the given path.",
            CommonCauses = ["Missing SDK or NuGet package that provides the imported targets", "Wrong path in a custom Import element", "Running on a machine without the required SDK workload"],
            ExampleFix  = "Run `dotnet restore`, install the missing SDK workload (`dotnet workload install`), or fix the `<Import>` path in your .csproj.",
        },
        ["MSB3644"] = new()
        {
            Title       = "Reference assemblies for framework not found",
            Explanation = "The target framework in your .csproj isn't installed on this machine.",
            CommonCauses = ["Targeting `net6.0` but only .NET 8 SDK is installed", "Targeting a framework version that was never installed"],
            ExampleFix  = "Install the target framework SDK, or change `<TargetFramework>` in your .csproj to a version you have installed.",
        },
        ["MSB3277"] = new()
        {
            Title       = "Found conflicts between different versions of the same assembly",
            Explanation = "Two or more of your dependencies require different versions of the same assembly, and MSBuild can't reconcile them.",
            CommonCauses = ["Different NuGet packages pulling in incompatible versions of a shared dependency", "Mixing packages that target different framework versions"],
            ExampleFix  = "Add an explicit `<PackageReference>` to the conflicting assembly at the version you want to force, or upgrade all packages to versions that agree.",
        },
        ["MSB3021"] = new()
        {
            Title       = "Unable to copy file",
            Explanation = "MSBuild can't copy a build output file — typically because the destination file is locked.",
            CommonCauses = ["The app is still running (locks the output DLL/EXE)", "Another build is in progress", "Antivirus is scanning the output folder"],
            ExampleFix  = "Stop the running app, wait for the other build to finish, or temporarily disable AV on the output folder.",
        },
        ["MSB4057"] = new()
        {
            Title       = "Target does not exist",
            Explanation = "You're invoking an MSBuild target (e.g. via `dotnet msbuild /t:MyTarget`) that isn't defined.",
            CommonCauses = ["Typo in the target name", "Target is defined in a .targets file that wasn't imported", "Target is only available in a specific SDK"],
            ExampleFix  = "Check the target name spelling, ensure the relevant .targets file is imported, or use `dotnet build` instead of specifying a target manually.",
        },
        ["MSB6003"] = new()
        {
            Title       = "Specified task executable could not be run",
            Explanation = "MSBuild tried to launch an external tool (like a compiler or code generator) and failed.",
            CommonCauses = ["Tool not installed or not on PATH", "Wrong path to the executable in the task definition", "Permissions issue on the executable"],
            ExampleFix  = "Install the missing tool, fix the path, or run `dotnet restore` to pull in required tool packages.",
        },
    };

    private static BuildErrorExplanation GenericCsError(string code, string? message) => new()
    {
        Code        = code,
        Title       = $"C# compiler error {code}",
        Explanation = message is not null
            ? $"The C# compiler reported: {message}"
            : "This is a C# compiler diagnostic. The compiler found a problem with your source code.",
        CommonCauses = ["Syntax or type error in the code near the reported location"],
        ExampleFix  = $"Search for '{code}' in the Microsoft C# compiler error reference for the exact meaning.",
        IsKnown     = false,
    };

    private static BuildErrorExplanation GenericMsbError(string code, string? message) => new()
    {
        Code        = code,
        Title       = $"MSBuild error {code}",
        Explanation = message is not null
            ? $"MSBuild reported: {message}"
            : "This is an MSBuild error — a problem occurred during the build orchestration, not in your C# source.",
        CommonCauses = ["Missing SDK, targets file, or build tool", "Configuration issue in .csproj or .targets"],
        ExampleFix  = $"Run `dotnet restore` first. If the issue persists, search for '{code}' in the MSBuild error reference.",
        IsKnown     = false,
    };

    private static BuildErrorExplanation GenericUnknown(string code, string? message) => new()
    {
        Code        = code,
        Title       = $"Unknown error code {code}",
        Explanation = message ?? "No additional context available.",
        CommonCauses = [],
        ExampleFix  = null,
        IsKnown     = false,
    };
}
