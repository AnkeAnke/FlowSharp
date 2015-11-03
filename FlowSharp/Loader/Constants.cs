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
            ///<summary>,No Error</summary>
            NC_NOERR = 0,
            ///<summary> Not a netcdf id</summary>
            NC_EBADID = -33,
            ///<summary> Too many netcdfs open</summary>
            NC_ENFILE = -34,
            ///<summary> netcdf file exists && NC_NOCLOBBER</summary>
            NC_EEXIST = -35,
            ///<summary> Invalid Argument</summary>
            NC_EINVAL = -36,
            ///<summary> Write to read only</summary>
            NC_EPERM = -37,
            ///<summary> Operation not allowed in data mode</summary>
            NC_ENOTINDEFINE = -38,
            ///<summary> Operation not allowed in define mode</summary>
            NC_EINDEFINE = -39,
            ///<summary> Index exceeds dimension bound</summary>
            NC_EINVALCOORDS = -40,
            ///<summary> NC_MAX_DIMS exceeded</summary>
            NC_EMAXDIMS = -41,
            ///<summary> String match to name in use</summary>
            NC_ENAMEINUSE = -42,
            ///<summary> Attribute not found</summary>
            NC_ENOTATT = -43,
            ///<summary> NC_MAX_ATTRS exceeded</summary>
            NC_EMAXATTS = -44,
            ///<summary> Not a netcdf data type</summary>
            NC_EBADTYPE = -45,
            ///<summary> Invalid dimension id or name</summary>
            NC_EBADDIM = -46,
            ///<summary> NC_UNLIMITED in the wrong index</summary>
            NC_EUNLIMPOS = -47,
            ///<summary> NC_MAX_VARS exceeded</summary>
            NC_EMAXVARS = -48,
            ///<summary> Variable not found</summary>
            NC_ENOTVAR = -49,
            ///<summary> Action prohibited on NC_GLOBAL varid</summary>
            NC_EGLOBAL = -50,
            ///<summary> Not a netcdf file</summary>
            NC_ENOTNC = -51,
            ///<summary> In Fortran, string too short</summary>
            NC_ESTS = -52,
            ///<summary> NC_MAX_NAME exceeded</summary>
            NC_EMAXNAME = -53,
            ///<summary> NC_UNLIMITED size already in use</summary>
            NC_EUNLIMIT = -54,
            ///<summary> nc_rec op when there are no record vars</summary>
            NC_ENORECVARS = -55,
            ///<summary> Attempt to convert between text & numbers</summary>
            NC_ECHAR = -56,
            ///<summary> Edge+start exceeds dimension bound</summary>
            NC_EEDGE = -57,
            ///<summary> Illegal stride</summary>
            NC_ESTRIDE = -58,
            ///<summary> Attribute or variable name contains illegal characters</summary>
            NC_EBADNAME = -59,
            ///<summary> Math result not representable</summary>
            NC_ERANGE = -60,
            ///<summary> Memory allocation (malloc) failure</summary>
            NC_ENOMEM = -61,
            ///<summary> One or more variable sizes violate format constraints</summary>
            NC_EVARSIZE = -62,
            ///<summary> Invalid dimension size</summary>
            NC_EDIMSIZE = -63,
            ///<summary> File likely truncated or possibly corrupted</summary>
            NC_ETRUNC = -64,
            NC_EHDFERR = -101,
            NC_ECANTREAD = -102,
            NC_ECANTWRITE = -103,
            NC_ECANTCREATE = -104,
            NC_EFILEMETA = -105,
            NC_EDIMMETA = -106,
            NC_EATTMETA = -107,
            NC_EVARMETA = -108,
            NC_ENOCOMPOUND = -109,
            NC_EATTEXISTS = -110,
            ///<summary> Attempting netcdf-4 operation on netcdf-3 file.</summary>
            NC_ENOTNC4 = -111,
            ///<summary> Attempting netcdf-4 operation on strict nc3 netcdf-4 file.</summary>
            NC_ESTRICTNC3 = -112,
            ///<summary> Bad group id. Bad!</summary>
            NC_EBADGRPID = -113,
            ///<summary> Bad type id.</summary>
            NC_EBADTYPEID = -114,
            ///<summary> Bad field id.</summary>
            NC_EBADFIELDID = -115,
            NC_EUNKNAME = -116,
            ///<summary>Operation not permitted</summary>
            EPERM = 1,
            ///<summary>No such file or directory</summary>
            ENOENT = 2,
            ///<summary>No such process</summary>
            ESRCH = 3,
            ///<summary>Interrupted system call</summary>
            EINTR = 4,
            ///<summary>Input/output error</summary>
            EIO = 5,
            ///<summary>Device not configured</summary>
            ENXIO = 6,
            ///<summary>Argument list too long</summary>
            E2BIG = 7,
            ///<summary>Exec format error</summary>
            ENOEXEC = 8,
            ///<summary>Bad file descriptor</summary>
            EBADF = 9,
            ///<summary>No child processes</summary>
            ECHILD = 10,
            ///<summary>Resource deadlock avoided</summary>
            EDEADLK = 11,
            ///<summary>Cannot allocate memory</summary>
            ENOMEM = 12,
            ///<summary>Permission denied</summary>
            EACCES = 13,
            ///<summary>Bad address</summary>
            EFAULT = 14,
            ///<summary>Block device required</summary>
            ENOTBLK = 15,
            ///<summary>Device busy</summary>
            EBUSY = 16,
            ///<summary>File exists</summary>
            EEXIST = 17,
            ///<summary>Cross-device link</summary>
            EXDEV = 18,
            ///<summary>Operation not supported by device</summary>
            ENODEV = 19,
            ///<summary>Not a directory</summary>
            ENOTDIR = 20,
            ///<summary>Is a directory</summary>
            EISDIR = 21,
            ///<summary>Invalid argument</summary>
            EINVAL = 22,
            ///<summary>Too many open files in system</summary>
            ENFILE = 23,
            ///<summary>Too many open files</summary>
            EMFILE = 24,
            ///<summary>Inappropriate ioctl for device</summary>
            ENOTTY = 25,
            ///<summary>Text file busy</summary>
            ETXTBSY = 26,
            ///<summary>File too large</summary>
            EFBIG = 27,
            ///<summary>No space left on device</summary>
            ENOSPC = 28,
            ///<summary>Illegal seek</summary>
            ESPIPE = 29,
            ///<summary>Read-only file system</summary>
            EROFS = 30,
            ///<summary>Too many links</summary>
            EMLINK = 31,
            ///<summary>Broken pipe</summary>
            EPIPE = 32,
            ///<summary>Numerical argument out of domain</summary>
            EDOM = 33,
            ///<summary>Result too large</summary>
            ERANGE = 34,
            ///<summary>Resource temporarily unavailable or operation would block</summary>
            EAGAIN_OR_EWOULDBLOCK = 35,
            ///<summary>Operation now in progress</summary>
            EINPROGRESS = 36,
            ///<summary>Operation already in progress</summary>
            EALREADY = 37,
            ///<summary>Socket operation on non-socket</summary>
            ENOTSOCK = 38,
            ///<summary>Destination address required</summary>
            EDESTADDRREQ = 39,
            ///<summary>Message too long</summary>
            EMSGSIZE = 40,
            ///<summary>Protocol wrong type for socket</summary>
            EPROTOTYPE = 41,
            ///<summary>Protocol not available</summary>
            ENOPROTOOPT = 42,
            ///<summary>Protocol not supported</summary>
            EPROTONOSUPPORT = 43,
            ///<summary>Socket type not supported</summary>
            ESOCKTNOSUPPORT = 44,
            ///<summary>Operation not supported on socket</summary>
            EOPNOTSUPP = 45,
            ///<summary>Protocol family not supported</summary>
            EPFNOSUPPORT = 46,
            ///<summary>Address family not supported by protocol family</summary>
            EAFNOSUPPORT = 47,
            ///<summary>Address already in use</summary>
            EADDRINUSE = 48,
            ///<summary>Can't assign requested address</summary>
            EADDRNOTAVAIL = 49,
            ///<summary>Network is down</summary>
            ENETDOWN = 50,
            ///<summary>Network is unreachable</summary>
            ENETUNREACH = 51,
            ///<summary>Network dropped connection on reset</summary>
            ENETRESET = 52,
            ///<summary>Software caused connection abort</summary>
            ECONNABORTED = 53,
            ///<summary>Connection reset by peer</summary>
            ECONNRESET = 54,
            ///<summary>No buffer space available</summary>
            ENOBUFS = 55,
            ///<summary>Socket is already connected</summary>
            EISCONN = 56,
            ///<summary>Socket is not connected</summary>
            ENOTCONN = 57,
            ///<summary>Can't send after socket shutdown</summary>
            ESHUTDOWN = 58,
            ///<summary>Too many references: can't splice</summary>
            ETOOMANYREFS = 59,
            ///<summary>Connection timed out</summary>
            ETIMEDOUT = 60,
            ///<summary>Connection refused</summary>
            ECONNREFUSED = 61,
            ///<summary>Too many levels of symbolic links</summary>
            ELOOP = 62,
            ///<summary>File name too long</summary>
            ENAMETOOLONG = 63,
            ///<summary>Host is down</summary>
            EHOSTDOWN = 64,
            ///<summary>No route to host</summary>
            EHOSTUNREACH = 65,
            ///<summary>Directory not empty</summary>
            ENOTEMPTY = 66,
            ///<summary>Too many processes</summary>
            EPROCLIM = 67,
            ///<summary>Too many users</summary>
            EUSERS = 68,
            ///<summary>Disc quota exceeded</summary>
            EDQUOT = 69,
            ///<summary>Stale NFS file handle</summary>
            ESTALE = 70,
            ///<summary>Too many levels of remote in path</summary>
            EREMOTE = 71,
            ///<summary>RPC struct is bad</summary>
            EBADRPC = 72,
            ///<summary>RPC version wrong</summary>
            ERPCMISMATCH = 73,
            ///<summary>RPC prog. not avail</summary>
            EPROGUNAVAIL = 74,
            ///<summary>Program version wrong</summary>
            EPROGMISMATCH = 75,
            ///<summary>Bad procedure for program</summary>
            EPROCUNAVAIL = 76,
            ///<summary>No locks available</summary>
            ENOLCK = 77,
            ///<summary>Function not implemented</summary>
            ENOSYS = 78,
            ///<summary>Inappropriate file type or format</summary>
            EFTYPE = 79
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

        /// <summary>
        /// Endianess of a variable.
        /// </summary>
        public enum NcEndian : int
        {
            /// <summary>Not created yet.</summary>
            NC_ENDIAN_NATIVE = 0,
            /// <summary>Little endian.</summary>
            NC_ENDIAN_LITTLE = 1,
            /// <summary>Big Endian</summary>
            NC_ENDIAN_BIG = 2
        }
    }
}
