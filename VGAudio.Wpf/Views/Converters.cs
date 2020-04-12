using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using VGAudio.Containers.NintendoWare.Structures;
using VGAudio.Wpf.Audio;

namespace VGAudio.Wpf.Views
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }

            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DoubleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return (double)value == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TrackTypeEnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string param = parameter as string;
            if (param == null)
                return DependencyProperty.UnsetValue;
            if (Enum.IsDefined(value.GetType(), value) == false)
                return DependencyProperty.UnsetValue;


            object paramValue = Enum.Parse(value.GetType(), param);
            return paramValue.Equals(value);
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string param = parameter as string;
            if (parameter == null)
                return DependencyProperty.UnsetValue;

            if (!(bool)value)
            {
                return DependencyProperty.UnsetValue;
            }
            return Enum.Parse(typeof(BrstmTrackType), param);
        }
    }

    public class SeekTableTypeEnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string param = parameter as string;
            if (param == null)
                return DependencyProperty.UnsetValue;
            if (Enum.IsDefined(value.GetType(), value) == false)
                return DependencyProperty.UnsetValue;


            object paramValue = Enum.Parse(value.GetType(), param);
            return paramValue.Equals(value);
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string param = parameter as string;
            if (parameter == null)
                return DependencyProperty.UnsetValue;

            if (!(bool)value)
            {
                return DependencyProperty.UnsetValue;
            }
            return Enum.Parse(typeof(BrstmSeekTableType), param);
        }
    }

    public class BrstmToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }

            return (FileType)value == FileType.Brstm ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BcstmToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }

            return (FileType)value == FileType.Bcstm ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
