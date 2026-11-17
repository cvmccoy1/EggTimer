using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EggTimer.Controls;

/// <summary>Integer spin box: digits-only text box with up/down buttons, arrow keys and mouse wheel.</summary>
public partial class NumericUpDown : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(NumericUpDown),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged, CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(int), typeof(NumericUpDown), new PropertyMetadata(0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(NumericUpDown), new PropertyMetadata(99, OnRangeChanged));

    public NumericUpDown()
    {
        InitializeComponent();
        UpdateText();
    }

    public event EventHandler? ValueChanged;

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var c = (NumericUpDown)d;
        return Math.Clamp((int)baseValue, c.Minimum, Math.Max(c.Minimum, c.Maximum));
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (NumericUpDown)d;
        c.UpdateText();
        c.ValueChanged?.Invoke(c, EventArgs.Empty);
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => d.CoerceValue(ValueProperty);

    private void UpdateText() => Box.Text = Value.ToString("00", CultureInfo.InvariantCulture);

    private void Commit()
    {
        if (int.TryParse(Box.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int v))
            Value = v;
        UpdateText(); // normalize formatting / revert junk
    }

    private void Step(int delta)
    {
        Commit();
        Value += delta;
    }

    private void Up_Click(object sender, RoutedEventArgs e) => Step(1);
    private void Down_Click(object sender, RoutedEventArgs e) => Step(-1);

    private void Box_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsAsciiDigit);

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up: Step(1); Box.SelectAll(); e.Handled = true; break;
            case Key.Down: Step(-1); Box.SelectAll(); e.Handled = true; break;
            case Key.Enter: Commit(); Box.SelectAll(); e.Handled = true; break;
            case Key.Space: e.Handled = true; break;
        }
    }

    private void Box_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsEnabled) return;
        Step(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    private void Box_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Commit();

    private void Box_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => Dispatcher.BeginInvoke(Box.SelectAll);
}
