using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NfseGerenciador.Converters
{
    public class ColorBrightnessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush originalBrush && parameter is string factorString)
            {
                if (double.TryParse(factorString, NumberStyles.Any, CultureInfo.InvariantCulture, out double factor))
                {
                    Color originalColor = originalBrush.Color;
                    // Ajusta o brilho de forma simplificada
                    byte r = (byte)Math.Max(0, Math.Min(255, originalColor.R * factor));
                    byte g = (byte)Math.Max(0, Math.Min(255, originalColor.G * factor));
                    byte b = (byte)Math.Max(0, Math.Min(255, originalColor.B * factor));
                    return new SolidColorBrush(Color.FromArgb(originalColor.A, r, g, b));
                }
            }
            return value; // Retorna o valor original se a conversão falhar
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}