namespace MissingFiles.Cli;

/// <summary>Invalid command line arguments (exit code 3).</summary>
internal sealed class CliArgumentException(string message) : Exception(message);

/// <summary>
/// Parsed options of one command. Options are written <c>--name value</c> or <c>--name=value</c>.
/// </summary>
internal sealed class CommandLine
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    private CommandLine()
    {
    }

    /// <summary>True if <c>--help</c>, <c>-h</c> or <c>/?</c> was given; required options are then not checked.</summary>
    public bool Help { get; private set; }

    public string? Get(string name) => _values.GetValueOrDefault(name);

    public string Require(string name) =>
        Get(name) ?? throw new CliArgumentException($"Missing required option --{name}.");

    public bool Has(string flag) => _flags.Contains(flag);

    /// <exception cref="CliArgumentException">An option is unknown, duplicated, missing its value, or a required option is missing.</exception>
    public static CommandLine Parse(
        IReadOnlyList<string> args,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string>? flagOptions = null,
        IReadOnlyCollection<string>? requiredOptions = null)
    {
        var result = new CommandLine();
        flagOptions ??= [];

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h" or "/?")
            {
                result.Help = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Length == 2)
            {
                throw new CliArgumentException($"Unexpected argument '{arg}'.");
            }

            var name = arg[2..];
            string? inlineValue = null;
            var equals = name.IndexOf('=', StringComparison.Ordinal);
            if (equals >= 0)
            {
                inlineValue = name[(equals + 1)..];
                name = name[..equals];
            }

            if (Contains(valueOptions, name))
            {
                var value = inlineValue;
                if (value is null && i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++i];
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new CliArgumentException($"Option --{name} needs a value.");
                }

                if (!result._values.TryAdd(name, value))
                {
                    throw new CliArgumentException($"Option --{name} is given more than once.");
                }
            }
            else if (Contains(flagOptions, name))
            {
                if (inlineValue is not null)
                {
                    throw new CliArgumentException($"Option --{name} does not take a value.");
                }

                result._flags.Add(name);
            }
            else
            {
                throw new CliArgumentException($"Unknown option --{name}.");
            }
        }

        if (!result.Help)
        {
            foreach (var required in requiredOptions ?? [])
            {
                result.Require(required);
            }
        }

        return result;
    }

    private static bool Contains(IReadOnlyCollection<string> names, string name) =>
        names.Contains(name, StringComparer.OrdinalIgnoreCase);
}
