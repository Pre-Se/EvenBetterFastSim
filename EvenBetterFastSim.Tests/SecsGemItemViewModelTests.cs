using SecsGemBaseItems.Data_Containers;
using Xunit;

namespace EvenBetterFastSim.Tests;

public class SecsGemItemViewModelTests
{
    /// <summary>
    /// Defensive path: the Modify command opens this editor only for a selected SECS/GEM item, but the view model
    /// can be constructed with any selection. OK must then just close the dialog; it used to throw a
    /// NullReferenceException.
    /// </summary>
    [Fact]
    public void Accept_WithoutSelectedSecsGemItem_JustCloses()
    {
        var message = new SecsGemDataMessage();
        var (vm, lib) = Editor.OpenWithSelection(message);
        var closed = false;
        vm.CloseAction = () => closed = true;

        vm.AcceptButtonClickCommand.Execute(null);

        Assert.True(closed);
        Assert.Same(message, lib.SelectedItem);
    }
}
