using OSGeo.GDAL;
using System;

namespace GPSError.GDAL
{
    public class GDALException : ApplicationException
    {
        #region Constructors

        public GDALException() : base(Gdal.GetLastErrorMsg()) => GDALErrorMessage = Gdal.GetLastErrorMsg();

        public GDALException(string message) : base(message) => GDALErrorMessage = Gdal.GetLastErrorMsg();

        public GDALException(string message, Exception ex) : base(message, ex) => GDALErrorMessage = Gdal.GetLastErrorMsg();

        public GDALException(Exception ex) : base(Gdal.GetLastErrorMsg(), ex) => GDALErrorMessage = Gdal.GetLastErrorMsg();

        #endregion

        public string GDALErrorMessage { get; protected set; }
    }
}
