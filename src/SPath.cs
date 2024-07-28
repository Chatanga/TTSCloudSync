namespace TTSCloudSync;

class SPath
{
    public static SPath FromNativePath(string path)
    {
        string? root = Path.GetPathRoot(path);
        List<string> elements = new();
        string? p = path;
        while (p is not null)
        {
            string element = Path.GetFileName(p);
            if (element.Length > 0)
            {
                elements.Add(element);
            }
            p = Path.GetDirectoryName(p);
        }
        elements.Reverse();
        return new SPath(root is not null && root.Length > 0 ? root : null, elements.ToArray());
    }

    public static SPath FromAnyPath(char separator, string path)
    {
        return new SPath(
            path.StartsWith(separator) ? "" : null,
            path.Split(separator, StringSplitOptions.RemoveEmptyEntries).ToArray());
    }

    public static SPath FromTTSPath(string path)
    {
        return FromAnyPath(TabletopSimulatorCloud.FOLDER_SEPARATOR, path);
    }

    // Null for a relative path.
    // Otherwise, equals to something like "/", "C:", "//share", etc. (native path) or "" (any path).
    private readonly string? Root;

    private readonly string[] Elements;

    private SPath(string? root, string[] elements)
    {
        Root = root;
        Elements = new string[elements.Length];
        elements.CopyTo(Elements, 0);
    }

    public string ToNativePath()
    {
        string path = Path.Combine(Elements);
        if (Root is not null)
        {
            path = Path.Combine(Root, path);
        }
        return path;
    }

    public string ToAnyPath(char separator)
    {
        string path = string.Join(separator, Elements);
        if (Root is not null)
        {
            path = Root + separator + path;
        }
        return path;
    }

    public string ToTTSPath()
    {
        return ToAnyPath(TabletopSimulatorCloud.FOLDER_SEPARATOR);
    }

    public override string ToString()
    {
        return $"({Root})>[{string.Join(", ", Elements)}]";
    }

    public bool IsAbsolute()
    {
        return Root is not null;
    }

    public bool IsRelative()
    {
        return Root is null;
    }

    public string? GetName()
    {
        return Elements.Length > 0 ? Elements[0] : null;
    }

    public int GetLength()
    {
        return Elements.Length;
    }

    public SPath Resolve(params string[] elements)
    {
        string[] allElements = new string[Elements.Length + elements.Length];
        Elements.CopyTo(allElements, 0);
        elements.CopyTo(allElements, Elements.Length);
        return new SPath(Root, allElements);
    }

    public SPath Combine(SPath relativeSubPath)
    {
        if (!relativeSubPath.IsRelative())
        {
            throw new ArgumentException($"Not a relative path: {relativeSubPath}");
        }

        return Resolve(relativeSubPath.Elements);
    }

    public SPath? Relativize(SPath referenceAbsolutePath)
    {
        if (!referenceAbsolutePath.IsAbsolute())
        {
            throw new ArgumentException($"Not an absolute path: {referenceAbsolutePath}");
        }
        if (!IsAbsolute())
        {
            throw new ArgumentException($"Not an absolute path: {this}");
        }

        if (referenceAbsolutePath.Root != Root)
        {
            return null;
        }

        for (int i = 0; i < referenceAbsolutePath.Elements.Length; ++i)
        {
            if (i >= Elements.Length || Elements[i] != referenceAbsolutePath.Elements[i])
            {
                return null;
            }
        }

        return new SPath(null, Elements[referenceAbsolutePath.Elements.Length..]);
    }

    public SPath? Prune(SPath baseRelativePath)
    {
        if (!baseRelativePath.IsRelative())
        {
            throw new ArgumentException($"Not a relative path: {baseRelativePath}");
        }
        if (!IsRelative())
        {
            throw new ArgumentException($"Not a relative path: {this}");
        }

        int length = baseRelativePath.Elements.Length;
        for (int i = 0; i < length; ++i)
        {
            if (i >= Elements.Length || Elements[i] != baseRelativePath.Elements[i])
            {
                return null;
            }
        }

        return new SPath(Root, Elements[length..]);
    }

    public SPath? Strip(SPath extRelativePath)
    {
        if (!extRelativePath.IsRelative())
        {
            throw new ArgumentException($"Not a relative path: {extRelativePath}");
        }

        int length = extRelativePath.Elements.Length;
        for (int i = length - 1; i >= 0; --i)
        {
            if (i >= Elements.Length || Elements[i] != extRelativePath.Elements[i])
            {
                return null;
            }
        }

        return new SPath(Root, Elements[length..]);
    }

    public SPath? GetParent()
    {
        if (Elements.Length > 0)
        {
            return new SPath(Root, Elements[0..(Elements.Length - 1)]);
        }
        else
        {
            return null;
        }
    }
}
