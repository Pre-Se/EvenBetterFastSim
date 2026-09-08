using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MessagePack;
using Microsoft.Extensions.Logging;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Enums;

namespace EvenBetterFastSim.Services;

public class LibraryMessagePackService(ILogger<LibraryMessagePackService> logger)
{
    public void Save(string path, ObservableCollection<SecsGemTransaction> library)
    {
        var dto = new TransactionLibraryDto
        {
            Transactions = library.Select(ToDto).ToList()
        };
        var bytes = MessagePackSerializer.Serialize(dto);
        File.WriteAllBytes(path, bytes);
    }

    public ObservableCollection<SecsGemTransaction> Load(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var dto = MessagePackSerializer.Deserialize<TransactionLibraryDto>(bytes);
            var result = new ObservableCollection<SecsGemTransaction>();
            if (dto.Transactions is not null)
                foreach (var tx in dto.Transactions)
                    result.Add(FromDto(tx));
            logger.LogInformation("Library \"{Path}\" loaded successfully", path);
            return result;
        }
        catch (MessagePackSerializationException ex)
        {
            logger.LogError("Failed to load library from \"{Path}\": file is invalid", path);
            return [];
        }
    }

    private static TransactionDto ToDto(SecsGemTransaction tx) => new()
    {
        Name = tx.Name,
        Description = tx.Description ?? string.Empty,
        Stream = tx.PrimaryMessage.Stream,
        Function = tx.PrimaryMessage.Function,
        Reply = tx.PrimaryMessage.Reply,
        Primary = MessageToDto(tx.PrimaryMessage),
        Secondary = tx.PrimaryMessage.Reply ? MessageToDto(tx.ReplyMessage) : null
    };

    private static MessageDto MessageToDto(SecsGemDataMessage msg) => new()
    {
        Description = msg.Description ?? string.Empty,
        Items = msg.Children.OfType<SecsGemItem>().Select(ItemToDto).ToList()
    };

    private static ItemDto ItemToDto(SecsGemItem item) => new()
    {
        Format = (int)item.FormatType,
        Description = item.Description ?? string.Empty,
        Values = item.GetStringValues().ToList() is { Count: > 0 } v ? v : null,
        Items = item.Children.OfType<SecsGemItem>().Select(ItemToDto).ToList() is { Count: > 0 } c ? c : null
    };

    private static SecsGemTransaction FromDto(TransactionDto dto)
    {
        var tx = new SecsGemTransaction
        {
            Name = dto.Name,
            Description = dto.Description
        };

        var primary = new SecsGemDataMessage
        {
            Description = dto.Primary?.Description ?? string.Empty,
            Reply = dto.Reply,
            Stream = dto.Stream,
            Function = dto.Function,
            IsPrimary = true
        };
        tx.PrimaryMessage = primary;

        if (dto.Primary?.Items is not null)
            foreach (var itemDto in dto.Primary.Items)
                ReadItem(itemDto).SetParent(primary);

        switch (dto.Reply)
        {
            case true when dto.Secondary is not null:
            {
                var secondary = new SecsGemDataMessage
                {
                    Description = dto.Secondary.Description,
                    Reply = false,
                    Stream = dto.Stream,
                    Function = (byte)(dto.Function + 1),
                    IsPrimary = false
                };
                tx.ReplyMessage = secondary;

                if (dto.Secondary.Items is not null)
                    foreach (var itemDto in dto.Secondary.Items)
                        ReadItem(itemDto).SetParent(secondary);
                break;
            }
        }

        return tx;
    }

    private static SecsGemItem ReadItem(ItemDto dto)
    {
        var item = SecsGemItem.Create((SecsGemItemFormatType)dto.Format);
        item.Description = dto.Description;

        if (dto.Values is not null)
            item.SetValuesFromStrings(dto.Values);

        if (dto.Items is null) return item;
        foreach (var child in dto.Items)
            ReadItem(child).SetParent(item);

        return item;
    }
}

[MessagePackObject]
public class TransactionLibraryDto
{
    [Key(0)] public List<TransactionDto>? Transactions { get; set; }
}

[MessagePackObject]
public class TransactionDto
{
    [Key(0)] public string Name { get; set; } = string.Empty;
    [Key(1)] public string Description { get; set; } = string.Empty;
    [Key(2)] public byte Stream { get; set; }
    [Key(3)] public byte Function { get; set; }
    [Key(4)] public bool Reply { get; set; }
    [Key(5)] public MessageDto? Primary { get; set; }
    [Key(6)] public MessageDto? Secondary { get; set; }
}

[MessagePackObject]
public class MessageDto
{
    [Key(0)] public string Description { get; set; } = string.Empty;
    [Key(1)] public List<ItemDto>? Items { get; set; }
}

[MessagePackObject]
public class ItemDto
{
    [Key(0)] public int Format { get; set; }
    [Key(1)] public string Description { get; set; } = string.Empty;
    [Key(2)] public List<string>? Values { get; set; }
    [Key(3)] public List<ItemDto>? Items { get; set; }
}
