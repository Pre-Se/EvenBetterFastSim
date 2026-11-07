using System;
using System.IO;
using System.Text.Json;
using EvenBetterFastSim.Services;
using MessagePack;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EvenBetterFastSim.Tests;

/// <summary>
/// A library file the parser rejects returns an empty library and logs one error that carries the
/// parser's exception. Files that parse but do not match the library structure are not covered here.
/// </summary>
public class LibraryLoadErrorTests
{
    [Fact]
    public void JsonLibrary_InvalidFile_LoadsEmpty_AndLogsTheException()
    {
        var path = WriteTempFile(".json", "this is not JSON");
        try
        {
            var logger = new ListLogger<LibraryJsonService>();

            var library = new LibraryJsonService(logger).Load(path);

            Assert.Empty(library);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.IsAssignableFrom<JsonException>(entry.Exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MessagePackLibrary_InvalidFile_LoadsEmpty_AndLogsTheException()
    {
        var path = WriteTempFile(".msgpack", "this is not MessagePack");
        try
        {
            var logger = new ListLogger<LibraryMessagePackService>();

            var library = new LibraryMessagePackService(logger).Load(path);

            Assert.Empty(library);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.IsAssignableFrom<MessagePackSerializationException>(entry.Exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempFile(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ebfs-test-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);
        return path;
    }
}
