namespace TTSCloudSync;

class CommandLineParser
{
    private readonly struct OptionDescriptor
    {
        public readonly string Name;
        public readonly bool HasValue;

        public OptionDescriptor(string name, bool hasValue = false)
        {
            Name = name;
            HasValue = hasValue;
        }
    }

    private readonly List<OptionDescriptor> OptionDescriptors = new();

    public CommandLineParser AddOption(string name, bool hasValue = false)
    {
        OptionDescriptors.Add(new OptionDescriptor(name, hasValue));
        return this;
    }

    public (Dictionary<string, string?>, List<string>) Parse(string[] args)
    {
        Dictionary<string, string?> options = new();
        List<string> arguments = new();

        OptionDescriptor? lastOption = null;

        for (int i = 0, count = args.Length; i < count; ++i)
        {
            string arg = args[i];

            bool found = false;
            foreach (OptionDescriptor desc in OptionDescriptors)
            {
                if (arg == desc.Name)
                {
                    found = true;

                    if (lastOption is not null)
                    {
                        throw new Exception($"Unvalued option '{lastOption.Value.Name}'");
                    }

                    if (arguments.Count > 0)
                    {
                        throw new Exception($"Unexpected option '{desc.Name}' past one or more arguments");
                    }

                    if (desc.HasValue)
                    {
                        lastOption = desc;
                    }
                    else
                    {
                        options.Add(desc.Name, null);
                    }
                }
            }
            if (!found)
            {
                if (lastOption is not null)
                {
                    options.Add(lastOption.Value.Name, arg);
                    lastOption = null;
                }
                else
                {
                    arguments.Add(arg);
                }
            }
        }

        if (lastOption is not null)
        {
            throw new Exception($"Unvalued option '{lastOption.Value.Name}'");
        }

        return (options, arguments);
    }
}
