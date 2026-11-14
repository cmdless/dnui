// Run as a file-based app by PackRuntime: dotnet VerifyPackage.cs <nupkg> <required entry>
// Fails if the package lacks the runtime executable at the path the downloader expects.
using System.IO.Compression;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: VerifyPackage.cs <nupkg> <entry>");
    return 2;
}

using var zip = ZipFile.OpenRead(args[0]);
if (zip.GetEntry(args[1]) is null)
{
    Console.Error.WriteLine($"error: {Path.GetFileName(args[0])} has no '{args[1]}'. Entries under runtime/ (first 10):");
    foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("runtime/", StringComparison.Ordinal)).Take(10))
    {
        Console.Error.WriteLine("  " + entry.FullName);
    }

    return 1;
}

Console.WriteLine($"verified: {Path.GetFileName(args[0])} contains {args[1]} ({zip.Entries.Count} entries)");
return 0;
