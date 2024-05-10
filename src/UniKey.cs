using System.Diagnostics;

namespace TTSCloudSync;

/*
Union of all the unicity rules for the local file system, Steam Cloud and TSS
index. In fact, it happens to be the rules for the Steam Cloud alone since it
doesn't support folders and is case insensitive.
*/
public class UniKey : IEquatable<UniKey>
{
    public string Name { get; private set; }
    public string Sha1 { get; private set; }

    public UniKey(string name)
    {
        int i = name.IndexOf('_');
        Debug.Assert(i > 0);
        Name = name[(i + 1)..];
        Sha1 = name[0..i];
    }

    public UniKey(string name, string sha1)
    {
        Name = name;
        Sha1 = sha1;
    }

    public override bool Equals(object? obj) => obj is UniKey other && Equals(other);

    public bool Equals(UniKey? other) => other is not null && Name == other.Name && Sha1 == other.Sha1;

    public override int GetHashCode() => (Name.ToUpper(), Sha1).GetHashCode();

    public static bool operator ==(UniKey lhs, UniKey rhs) => lhs.Equals(rhs);

    public static bool operator !=(UniKey lhs, UniKey rhs) => !(lhs == rhs);

    public override string? ToString() => $"{Sha1}_{Name}";
}
