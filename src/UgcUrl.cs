using System.Text.RegularExpressions;

public readonly partial struct UgcUrl : IEquatable<UgcUrl>
{
    [GeneratedRegex("http://cloud-3\\.steamusercontent\\.com/ugc/(\\d+)/([0-9A-Z]+)/")]
    public static partial Regex Regex();

    public ulong Handle { get; }

    public string Sha1 { get; }

    public UgcUrl(ulong handle, string sha1)
    {
        Handle = handle;
        Sha1 = sha1;
    }

    public readonly bool Equals(UgcUrl other) => other.Handle == Handle && other.Sha1 == Sha1;

    public override readonly bool Equals(object? obj) => obj is UgcUrl resource && Equals(resource);

    public override int GetHashCode() => HashCode.Combine(Handle, Sha1);

    public override string ToString() => $"http://cloud-3.steamusercontent.com/ugc/{Handle}/{Sha1}/";
}
