using System.Text.RegularExpressions;

namespace TTSCloudSync;

readonly partial struct UgcUrl : IEquatable<UgcUrl>
{
    private static readonly string[] SERVER_URLS = {
        // Newer URL since end of july 2024 (v13.2).
        "https://steamusercontent-a.akamaihd.net",
        // Former URL, still supported by TTS when resolving resource from a save (both in the cloud and the cache).
        "http://cloud-3.steamusercontent.com",
    };

    [GeneratedRegex("/ugc/(\\d+)/([0-9A-Z]+)/")]
    private static partial Regex UgcRegex();

    public static (UgcUrl?, int, int) Find(string line, int startIndex)
    {
        foreach (string serverUrl in SERVER_URLS)
        {
            int index = line[startIndex..].IndexOf(serverUrl);
            if (index != -1)
            {
                Regex regex = UgcRegex();
                Match match = regex.Match(line, startIndex + index + serverUrl.Length);
                if (match.Success)
                {
                    ulong ugcHandle = ulong.Parse(match.Groups[1].Value);
                    string sha1 = match.Groups[2].Value;
                    UgcUrl ugcUrl = new(ugcHandle, sha1);
                    return (ugcUrl, startIndex + index, match.Index + match.Length);
                }
                else
                {
                    return (null, -1, -1);
                }
            }
        }
        return (null, -1, -1);
    }

    public static UgcUrl? Parse(string line) => Find(line, 0).Item1;

    public ulong Handle { get; }

    public string Sha1 { get; }

    public UgcUrl(ulong handle, string sha1)
    {
        Handle = handle;
        Sha1 = sha1;
    }

    public readonly bool Equals(UgcUrl other) => other.Handle == Handle && other.Sha1 == Sha1;

    public override readonly bool Equals(object? obj) => obj is UgcUrl other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Handle, Sha1);

    public static bool operator ==(UgcUrl lhs, UgcUrl rhs) => lhs.Equals(rhs);

    public static bool operator !=(UgcUrl lhs, UgcUrl rhs) => !(lhs == rhs);

    public override string ToString() => $"{SERVER_URLS[0]}/ugc/{Handle}/{Sha1}/";
}
