using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DatabaseDock.Extensions
{
    public static class EnumerableExtensions
    {
        public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source)
        {
            return new ObservableCollection<T>(source);
        }
    }
}
