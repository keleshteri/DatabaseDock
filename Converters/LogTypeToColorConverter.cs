using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DatabaseDock.Models;

namespace DatabaseDock.Converters
{
    public class LogTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogType logType)
            {
                return logType switch
                {
                    LogType.Info => new SolidColorBrush(Colors.Black),
                    LogType.Warning => new SolidColorBrush(Colors.Orange),
                    LogType.Error => new SolidColorBrush(Colors.Red),
                    LogType.DockerLog => new SolidColorBrush(Colors.Blue),
                    _ => new SolidColorBrush(Colors.Black)
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
