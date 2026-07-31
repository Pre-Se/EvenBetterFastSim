using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace EvenBetterFastSim.WPF.WPF_Controls;

/// <summary>
/// Interaction logic for NumericUpDown.xaml
/// </summary>
public partial class NumericUpDown
{
    public int Minvalue { private get; set; }
    public int Maxvalue { private get; set; }

    public static readonly DependencyProperty NudValueDp = DependencyProperty
        .Register(nameof(NudValue), typeof(int), typeof(NumericUpDown));

    public int NudValue
    {
        get => (int)GetValue(NudValueDp);
        set => SetValue(NudValueDp, value);
    }
    public NumericUpDown()
    {
            InitializeComponent();
    }

    private void NUDButtonUP_Click(object sender, RoutedEventArgs e)
    {
        var number = NUDTextBox.Text != "" ? Convert.ToInt32(NUDTextBox.Text) : 0;
            if (number < Maxvalue)
                NudValue = number + 1;
    }

    private void NUDButtonDown_Click(object sender, RoutedEventArgs e)
    {
        var number = NUDTextBox.Text != "" ? Convert.ToInt32(NUDTextBox.Text) : 0;
            if (number > Minvalue)
                NudValue = number - 1;
    }

    private void NUDTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                NUDButtonUP.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(NUDButtonUP,
                        [true]);
                break;
            case Key.Down:
                NUDButtonDown.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(NUDButtonDown,
                        [true]);
                break;
        }
    }

    private void NUDTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(NUDButtonUP,
                        [false]);
                break;
            case Key.Down:
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(NUDButtonDown,
                        [false]);
                break;
        }
    }

    private void NUDTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (NUDTextBox.Text != "")
                if (!int.TryParse(NUDTextBox.Text, out var number))
                    NUDTextBox.Text = NudValue.ToString();
                else
                {
                    if (number > Maxvalue) number = Maxvalue;
                    if (number < Minvalue) number = Minvalue;
                    NudValue = number;
                    NUDTextBox.Text = NudValue.ToString();
                }
        else NudValue = 0;
    }

}