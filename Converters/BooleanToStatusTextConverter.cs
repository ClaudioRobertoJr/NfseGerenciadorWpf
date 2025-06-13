using System;
using System.Globalization;
using System.Windows.Data;

namespace NfseGerenciador.Converters
{
    public class BooleanToStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "Paga" : "Pendente";
            }
            return "Pendente"; // Valor padrão ou para entradas inesperadas
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // A conversão de volta geralmente não é necessária para exibição.
            throw new NotImplementedException();
        }
    }
}