using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Research.ScientificDataSet.NetCDF4
{
    public partial class NetCDF
    {
        /// <summary>
        /// 'size' argument to ncdimdef for an unlimited dimension
        /// </summary>
        public const int NC_UNLIMITED = 0;

        /// <summary>
        /// attribute id to put/get a global attribute
        /// </summary>
        public const int NC_GLOBAL = -1;

        /// <summary>
        /// The netcdf external data types
        /// </summary>
        public enum NcType : int
        {
            /// <summary>signed 1 byte intege</summary>
            NC_BYTE = 1,
            /// <summary>ISO/ASCII character</summary>
            NC_CHAR = 2,
            /// <summary>signed 2 byte integer</summary>
            NC_SHORT = 3,
            /// <summary>signed 4 byte integer</summary>
            NC_INT = 4,
            /// <summary>single precision floating point number</summary>
            NC_FLOAT = 5,
            /// <summary>double precision floating point number</summary>
            NC_DOUBLE = 6,
            /// <summary>signed 8-byte int</summary>
            NC_INT64 = 10,
            /// <summary>string</summary>
            NC_STRING =	12	
        }

        public static Type GetCLRType(NcType ncType)
        {
            switch (ncType)
            {
                case NcType.NC_BYTE:
                    return typeof(byte);
                case NcType.NC_CHAR:
                    return typeof(sbyte);
                case NcType.NC_SHORT:
                    return typeof(short);
                case NcType.NC_INT:
                    return typeof(int);
                case NcType.NC_INT64:
                    return typeof(long);
                case NcType.NC_FLOAT:
                    return typeof(float);
                case NcType.NC_DOUBLE:
                    return typeof(double);
                case NcType.NC_STRING:
                    return typeof(string);
                default:
                    throw new ApplicationException("Unknown nc type");
            }
        }

        public static NcType GetNcType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Double:
                    return NcType.NC_DOUBLE;

                case TypeCode.Single:
                    return NcType.NC_FLOAT;

                case TypeCode.Int64:
                    return NcType.NC_INT64;

                case TypeCode.Int32:
                    return NcType.NC_INT;

                case TypeCode.Int16:
                    return NcType.NC_SHORT;

                case TypeCode.Byte:
                    return NcType.NC_BYTE;

                case TypeCode.SByte:
                    return NcType.NC_CHAR;

                case TypeCode.String:
                    return NcType.NC_STRING;

                case TypeCode.DateTime:
                    return NcType.NC_INT64;


                default:
                    throw new NotSupportedException("Not supported type of data.");
            }
        }

        public enum CreateMode : int
        {
            NC_NOWRITE = 0,
            /// <summary>read & write</summary>
            NC_WRITE = 0x0001,
            NC_CLOBBER = 0,
            /// <summary>Don't destroy existing file on create</summary>
            NC_NOCLOBBER = 0x0004,
            /// <summary>argument to ncsetfill to clear NC_NOFILL</summary>
            NC_FILL = 0,
            /// <summary>Don't fill data section an records</summary>
            NC_NOFILL = 0x0100,
            /// <summary>Use locking if available</summary>
            NC_LOCK = 0x0400,
            /// <summary>Share updates, limit cacheing</summary>
            NC_SHARE = 0x0800,
            NC_64BIT_OFFSET = 0x0200,
            /// <summary>Enforce strict netcdf-3 rules</summary>
            NC_CLASSIC = 0x0100,
            /// <summary>causes netCDF to create a HDF5/NetCDF-4 file</summary>
            NC_NETCDF4 = 0x1000
        }

        public enum ResultCode : int
        {
            /// <summary>No Error</summary>
            NC_NOERR = 0,
            /// <summary>Invalid dimension id or name</summary>
            NC_EBADDIM = -46,
            /// <summary>Attribute not found</summary>
            NC_ENOTATT = -43,
        }

        /// <summary>
        ///	Default fill values, used unless _FillValue attribute is set.
        /// These values are stuffed into newly allocated space as appropriate.
        /// The hope is that one might use these to notice that a particular datum
        /// has not been set.
        /// </summary>
        public static class FillValues
        {
            public const byte NC_FILL_BYTE = 255;
            public const char NC_FILL_CHAR = (char)0;
            public const short NC_FILL_SHORT = -32767;
            public const int NC_FILL_INT = -2147483647;
            public const float NC_FILL_FLOAT = 9.96921E+36f;    /* near 15 * 2^119 */
            public const double NC_FILL_DOUBLE = 9.969209968386869E+36;
        }


        ///<summary>These maximums are enforced by the interface, to facilitate writing
        ///applications and utilities.  However, nothing is statically allocated to
        ///these sizes internally.</summary>
        public enum Limits
        {
            /// <summary>max dimensions per file </summary>
            NC_MAX_DIMS = 10,
            /// <summary>max global or per variable attributes </summary>
            NC_MAX_ATTRS = 2000,
            /// <summary>max variables per file</summary>
            NC_MAX_VARS = 2000,
            /// <summary>max length of a name </summary>
            NC_MAX_NAME = 128,
            /// <summary>max per variable dimensions </summary>
            NC_MAX_VAR_DIMS = 10
        }
    }
}
