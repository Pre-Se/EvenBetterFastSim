using System.Linq;
using EvenBetterFastSim.Services;
using EvenBetterFastSim.WPF.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Enums;
using Xunit;

namespace EvenBetterFastSim.Tests;

/// <summary>
/// Covers the "binary values are not saved" report: entering binary data for a Binary item
/// in the Modify-Item dialog must persist onto the underlying item.
/// </summary>
public class SecsGemItemBinaryTests
{
    private const string Hex = "DEADBEEF";

    [Fact]
    public void BinaryItem_ManualHexEntry_IsSaved()
    {
        var ppbody = SecsGemItem.Create(SecsGemItemFormatType.Binary);
        ppbody.Description = "Process program body";

        var (vm, lib) = Editor.Open(ppbody);

        // User keeps type = Binary and types hex in "Manual Entry", then clicks OK.
        vm.ItemValues = Hex;
        vm.AcceptButtonClickCommand.Execute(null);

        var saved = (SecsGemItem)lib.SelectedItem!;
        Assert.Equal(SecsGemItemFormatType.Binary, saved.FormatType);
        Assert.Contains(Hex, Editor.ToHex(saved));
    }

    [Fact]
    public void BinaryItem_EmptyEntry_StaysEmpty_AndDoesNotThrow()
    {
        var item = SecsGemItem.Create(SecsGemItemFormatType.Binary);

        var (vm, lib) = Editor.Open(item);
        vm.ItemValues = string.Empty;
        vm.AcceptButtonClickCommand.Execute(null);

        var saved = (SecsGemItem)lib.SelectedItem!;
        // Format byte 0x21 + length 0x00, no value bytes.
        Assert.Equal("2100", Editor.ToHex(saved));
    }

    /// <summary>
    /// End-to-end through the real load path: the S7F3 PPBODY loaded from the shipped
    /// <c>DefaultLibrary.msgpack</c> must be Binary (the fix) and must persist entered binary data.
    /// </summary>
    [Fact]
    public void Ppbody_LoadedFromLibraryMsgpack_IsBinary_AndSavesHex()
    {
        var library = new LibraryMessagePackService(NullLogger<LibraryMessagePackService>.Instance)
            .Load(TestPaths.DefaultLibraryMsgpack);

        var ppbody = library
            .Select(tx => Editor.FindByDescription(tx.PrimaryMessage.Children, "Process program body"))
            .FirstOrDefault(x => x is not null);

        Assert.NotNull(ppbody);
        Assert.Equal(SecsGemItemFormatType.Binary, ppbody!.FormatType);

        var (vm, lib) = Editor.Open(ppbody);
        vm.ItemValues = Hex;
        vm.AcceptButtonClickCommand.Execute(null);

        Assert.Contains(Hex, Editor.ToHex((SecsGemItem)lib.SelectedItem!));
    }

    /// <summary>
    /// Documents the still-open editor limitation: switching an item's FormatType to one with a
    /// different backing type (ASCII&lt;string&gt; -> Binary&lt;byte&gt;) drops the value, because
    /// AcceptButtonClick relies on CopyFrom, which cannot change an item's generic type. PPBODY is
    /// now defined as Binary so the S7F3 workflow avoids this; unskip when the editor is fixed to
    /// replace the item in the tree on a type change.
    /// </summary>
    [Fact]
    public void ChangingAsciiItemToBinary_ShouldPreserveValue()
    {
        var ascii = SecsGemItem.Create(SecsGemItemFormatType.ASCII);
        ascii.Description = "Process program body";

        var (vm, lib) = Editor.Open(ascii);
        vm.SecsGemItemCopy.FormatType = SecsGemItemFormatType.Binary; // user picks Binary in the dropdown
        vm.ItemValues = Hex;
        vm.AcceptButtonClickCommand.Execute(null);

        Assert.Contains(Hex, Editor.ToHex((SecsGemItem)lib.SelectedItem!));
    }
}
