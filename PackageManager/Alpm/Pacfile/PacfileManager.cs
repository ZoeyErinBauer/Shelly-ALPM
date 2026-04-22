using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PackageManager.Alpm.Pacfile;

public class PacfileManager : IPacfileManager, IAsyncDisposable
{
    private readonly string _pacfileStore;
    private readonly MemoryStream _tarBuffer;
    private bool _dirty;

    public PacfileManager(string pacfileStore)
    {
        _pacfileStore = pacfileStore;
        _tarBuffer = new MemoryStream();

        if (File.Exists(pacfileStore))
        {
            using var fs = File.OpenRead(pacfileStore);
            fs.CopyTo(_tarBuffer);
        }
    }

    public async Task SavePacfile(PacfileRecord pacfile)
    {
        var entries = new List<(string Name, byte[] Data)>();

        _tarBuffer.Position = 0;
        if (_tarBuffer.Length > 0)
        {
            await using var reader = new TarReader(_tarBuffer, leaveOpen: true);
            while (await reader.GetNextEntryAsync() is { } entry)
            {
                if (entry.Name == pacfile.Name)
                {
                    continue;
                }

                using var ms = new MemoryStream();
                if (entry.DataStream is not null)
                {
                    await entry.DataStream.CopyToAsync(ms);
                }
                entries.Add((entry.Name, ms.ToArray()));
            }
        }

        entries.Add((pacfile.Name, Encoding.UTF8.GetBytes(pacfile.Text)));

        _dirty = true;
        _tarBuffer.SetLength(0);
        await using (var writer = new TarWriter(_tarBuffer, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                var newEntry = new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(data)
                };
                await writer.WriteEntryAsync(newEntry);
            }
        }

        await FlushAsync();
    }

    public async Task<PacfileRecord?> GetPacfile(string name)
    {
        _tarBuffer.Position = 0;
        if (_tarBuffer.Length == 0)
        {
            return null;
        }

        await using var reader = new TarReader(_tarBuffer, leaveOpen: true);
        while (await reader.GetNextEntryAsync() is { } entry)
        {
            if (entry.Name != name)
            {
                continue;
            }

            using var memoryStream = new MemoryStream();
            if (entry.DataStream is not null)
            {
                await entry.DataStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;
            using var streamReader = new StreamReader(memoryStream);
            return new PacfileRecord(entry.Name, await streamReader.ReadToEndAsync());
        }

        return null;
    }

    public async Task<List<PacfileRecord>> GetPacfiles()
    {
        var list = new List<PacfileRecord>();
        _tarBuffer.Position = 0;
        if (_tarBuffer.Length == 0)
        {
            return list;
        }

        await using var reader = new TarReader(_tarBuffer, leaveOpen: true);
        while (await reader.GetNextEntryAsync() is { } entry)
        {
            using var memoryStream = new MemoryStream();
            if (entry.DataStream is not null)
            {
                await entry.DataStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;
            using var streamReader = new StreamReader(memoryStream);
            list.Add(new PacfileRecord(entry.Name, await streamReader.ReadToEndAsync()));
        }

        return list;
    }

    public async Task FlushAsync()
    {
        await using var fs = File.Create(_pacfileStore);
        _tarBuffer.Position = 0;
        await _tarBuffer.CopyToAsync(fs);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_dirty)
            {
                await FlushAsync();
            }
        }
        finally
        {
            await _tarBuffer.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
