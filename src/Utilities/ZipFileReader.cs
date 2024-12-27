using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Utilities;

public sealed class ZipFileReader(string zipFilePath) : IDisposable
{
    private readonly ZipArchive _archive = ZipFile.OpenRead(zipFilePath);

    /// <summary>
    /// Enumerates all the file names in a ZIP file without recursing into subdirectories.
    /// May throw.
    /// </summary>
    public IEnumerable<string> EnumerateFileNames()
    {
        foreach (ZipArchiveEntry entry in _archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            yield return entry.FullName;
        }
    }

    /// <summary>
    /// Extracts the content of a specified file within a ZIP archive and returns it as a string.
    /// </summary>
    public string ExtractFileContent(string fileName)
    {
        ZipArchiveEntry? entry = _archive.GetEntry(fileName)
            ?? throw new FileNotFoundException($"The file '{fileName}' was not found in the ZIP archive.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    public void Dispose() => _archive.Dispose();
}
