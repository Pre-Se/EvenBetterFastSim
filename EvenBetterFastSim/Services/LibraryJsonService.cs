using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Enums;

namespace EvenBetterFastSim.Services;

public class LibraryJsonService(ILogger<LibraryJsonService> logger)
{
    public void Save(string path, ObservableCollection<SecsGemTransaction> library)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteString("Name", "Default Library");

        writer.WritePropertyName("Transactions");
        writer.WriteStartArray();
        foreach (var tx in library)
            WriteTransaction(writer, tx);
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static void WriteTransaction(Utf8JsonWriter writer, SecsGemTransaction tx)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", tx.Name);
        if (!string.IsNullOrEmpty(tx.Description))
            writer.WriteString("Desc", tx.Description);
        writer.WriteNumber("Stream", tx.PrimaryMessage.Stream);
        writer.WriteNumber("Function", tx.PrimaryMessage.Function);
        writer.WriteBoolean("Reply", tx.PrimaryMessage.Reply);

        writer.WritePropertyName("Primary");
        WriteMessage(writer, tx.PrimaryMessage);

        if (tx.PrimaryMessage.Reply)
        {
            writer.WritePropertyName("Secondary");
            WriteMessage(writer, tx.ReplyMessage);
        }

        writer.WriteEndObject();
    }

    private static void WriteMessage(Utf8JsonWriter writer, SecsGemDataMessage msg)
    {
        writer.WriteStartObject();
        if (!string.IsNullOrEmpty(msg.Description))
            writer.WriteString("Desc", msg.Description);

        var items = msg.Children.OfType<SecsGemItem>().ToList();
        if (items.Count > 0)
        {
            writer.WritePropertyName("Items");
            writer.WriteStartArray();
            foreach (var item in items)
                WriteItem(writer, item);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteItem(Utf8JsonWriter writer, SecsGemItem item)
    {
        writer.WriteStartObject();
        writer.WriteString("Format", item.FormatType.ToString());
        if (!string.IsNullOrEmpty(item.Description))
            writer.WriteString("Desc", item.Description);

        var values = item.GetStringValues().ToList();
        if (values.Count > 0 && values.Any(v => !string.IsNullOrEmpty(v)))
        {
            writer.WritePropertyName("Values");
            writer.WriteStartArray();
            foreach (var v in values)
                writer.WriteStringValue(v);
            writer.WriteEndArray();
        }

        var children = item.Children.OfType<SecsGemItem>().ToList();
        if (children.Count > 0)
        {
            writer.WritePropertyName("Items");
            writer.WriteStartArray();
            foreach (var child in children)
                WriteItem(writer, child);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    public ObservableCollection<SecsGemTransaction> Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            var transactions = new ObservableCollection<SecsGemTransaction>();

            if (root.TryGetProperty("Transactions", out var txns))
            {
                foreach (var txn in txns.EnumerateArray())
                    transactions.Add(ReadTransaction(txn));
            }

            logger.LogInformation("Library \"{Path}\" loaded successfully", path);
            return transactions;
        }
        catch (JsonException ex)
        {
            logger.LogError("Failed to load library from \"{Path}\": file is invalid", path);
            return [];
        }
    }

    private static SecsGemTransaction ReadTransaction(JsonElement element)
    {
        var tx = new SecsGemTransaction
        {
            Name = element.GetProperty("Name").GetString() ?? "",
            Description = element.TryGetProperty("Desc", out var desc) ? desc.GetString() ?? "" : ""
        };

        var stream = element.GetProperty("Stream").GetByte();
        var function = element.GetProperty("Function").GetByte();
        var reply = element.GetProperty("Reply").GetBoolean();

        if (element.TryGetProperty("Primary", out var primary))
            ReadMessage(primary, tx, isPrimary: true, stream, function, reply);

        if (reply && element.TryGetProperty("Secondary", out var secondary))
            ReadMessage(secondary, tx, isPrimary: false, stream, (byte)(function + 1), false);

        return tx;
    }

    private static void ReadMessage(JsonElement element, SecsGemTransaction tx, bool isPrimary, byte stream, byte function, bool reply)
    {
        var msg = new SecsGemDataMessage
        {
            Description = element.TryGetProperty("Desc", out var desc) ? desc.GetString() ?? "" : "",
            Reply = reply,
            Stream = stream,
            Function = function,
            IsPrimary = isPrimary
        };

        if (isPrimary)
            tx.PrimaryMessage = msg;
        else
            tx.ReplyMessage = msg;

        if (element.TryGetProperty("Items", out var items))
        {
            foreach (var itemEl in items.EnumerateArray())
            {
                var item = ReadItem(itemEl);
                item.SetParent(msg);
            }
        }
    }

    private static SecsGemItem ReadItem(JsonElement element)
    {
        var formatType = SecsGemItemFormatType.ASCII;
        if (element.TryGetProperty("Format", out var formatEl))
        {
            var formatStr = formatEl.GetString() ?? "";
            if (!Enum.TryParse(formatStr, out formatType))
                formatType = SecsGemItemFormatType.ASCII;
        }

        var item = SecsGemItem.Create(formatType);
        item.Description = element.TryGetProperty("Desc", out var desc) ? desc.GetString() ?? "" : "";

        if (element.TryGetProperty("Values", out var values))
        {
            var strings = new string[values.GetArrayLength()];
            var i = 0;
            foreach (var v in values.EnumerateArray())
                strings[i++] = v.GetString() ?? "";
            item.SetValuesFromStrings(strings);
        }

        if (element.TryGetProperty("Items", out var children))
        {
            foreach (var childEl in children.EnumerateArray())
            {
                var child = ReadItem(childEl);
                child.SetParent(item);
            }
        }

        return item;
    }
}
