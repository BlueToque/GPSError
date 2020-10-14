using GPSError.GDAL;
using System.Collections.Generic;

namespace GPSError
{
    static class Extensions
    {
        public static List<PointDType> AddItem(this List<PointDType> list, PointDType item)
        {
            list.Add(item);
            return list;
        }

        public static bool IsNullOrEmpty(this string source) => string.IsNullOrEmpty(source);
    }
}
