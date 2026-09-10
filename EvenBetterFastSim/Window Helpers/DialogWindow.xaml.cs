using System.Windows.Input;
using EvenBetterFastSim.WPF.ViewModels.Responders;
using Wpf.Ui.Controls;

namespace EvenBetterFastSim.Window_Helpers;

public partial class DialogWindow : FluentWindow
{
    public DialogWindow()
    {
        InitializeComponent();

        // The node condition / response editor needs more room than the default form dialogs.
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is NodeResponderViewModel)
            {
                Width = 760;
                Height = 680;
                MinWidth = 560;
                MinHeight = 460;
            }
        };
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
