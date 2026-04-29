using System.Globalization;

namespace TipidMateMauiApp.Converters
{
    public class BoolToAmountColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isIncome)
                return isIncome ? Color.FromArgb("#06D6A0") : Color.FromArgb("#FF6B6B");
            return Color.FromArgb("#2D2D35");
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isIncome)
                return isIncome ? Color.FromArgb("#E8FDF6") : Color.FromArgb("#FFF5F5");
            return Colors.White;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class NullToAddEditConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value == null ? "➕ Add Entry" : "✏️ Edit Entry";
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}