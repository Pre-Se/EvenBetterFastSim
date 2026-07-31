using System.Windows.Input;
using Wpf.Ui.Controls;

namespace EvenBetterFastSim.Window_Helpers;

public partial class DialogWindow : FluentWindow
{
    public DialogWindow()
    {
        InitializeComponent();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        switch (e.Key)
        {
            case Key.Return:
                if (FocusManager.GetFocusedElement(this) is System.Windows.Controls.TextBox { AcceptsReturn: true })
                    break;
                OkButton.Focus();
                if (OkButton.Command?.CanExecute(OkButton.CommandParameter) == true)
                    OkButton.Command.Execute(OkButton.CommandParameter);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }
}
