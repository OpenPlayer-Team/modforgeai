using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace WinUIMvvmApp;

/// <summary>
/// Converte dimensione in byte in formato leggibile (KB, MB, GB)
/// </summary>
public class BytesToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converte DateTime in stringa formattata
/// </summary>
public class DateTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.ToString("dd/MM/yyyy HH:mm");
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converte intero in Visibility (Visible se > 0, Collapsed altrimenti)
/// Parametro opzionale: se "zero", inverti la logica
/// </summary>
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int intValue)
        {
            bool isZeroMode = parameter?.ToString() == "zero";
            
            if (isZeroMode)
            {
                return intValue == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converte booleano in Visibility (inverso: True -> Collapsed, False -> Visible)
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converte booleano in FontWeight (True -> Bold, False -> Normal)
/// </summary>
public class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue && boolValue)
        {
            return Microsoft.UI.Text.FontWeights.Bold;
        }
        return Microsoft.UI.Text.FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converte booleano in Symbol per l'orientamento dello split
/// True (Verticale) -> ColumnSplit, False (Orizzontale) -> RowSplit
/// </summary>
public class SplitOrientationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isVertical)
        {
            return isVertical ? Symbol.ColumnSplit : Symbol.RowSplit;
        }
        return Symbol.Split;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converte booleano in testo descrittivo per l'orientamento
/// </summary>
public class OrientationTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isVertical)
        {
            return isVertical ? "Verticale" : "Orizzontale";
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converte booleano in testo di stato sincronizzazione
/// </summary>
public class SyncStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isSyncEnabled)
        {
            return isSyncEnabled ? "🔄 Sync ON" : "🔀 Sync OFF";
        }
        return "🔀 Sync OFF";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converte intero in visibilità per stato ricerca
/// 0 = Collapsed, >0 = Visible
/// </summary>
public class SearchStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
