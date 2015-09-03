using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Microsoft.Research.ScientificDataSet.NetCDF4
{
    public static partial class NetCDF
    {
        [DllImport("netcdf4.dll", CallingConvention=CallingConvention.Cdecl)]
        public static extern int nc_open(string path, CreateMode mode, out int ncidp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_create(string path, CreateMode mode, out int ncidp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_close(int ncidp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_sync(int ncid);
        [DllImport("netcdf4.dll")]
        public static extern int nc_enddef(int ncid);
        [DllImport("netcdf4.dll")]
        public static extern int nc_redef(int ncid);
        [DllImport("netcdf4.dll")]
        public static extern string nc_strerror(int ncerror);

        [DllImport("netcdf4.dll")]
        public static extern int nc_inq(int ncid, out int ndims, out int nvars, out int ngatts, out int unlimdimid);

        [DllImport("netcdf4.dll")]
        public static extern int nc_def_var(int ncid, string name, NcType xtype, int ndims, int[] dimids, out int varidp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_var(int ncid, int varid, StringBuilder name, out NcType type, out int ndims, int[] dimids, out int natts);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_varids(int ncid, out int nvars, int[] varids);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_vartype(int ncid, int varid, out NcType xtypep);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_varnatts(int ncid, int varid, out int nattsp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_varid(int ncid, string name, out int varidp);

        [DllImport("netcdf4.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nc_inq_ndims(int ncid, out int ndims);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_nvars(int ncid, out int nvars);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_varname(int ncid, int varid, StringBuilder name);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_varndims(int ncid, int varid, out int ndims);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_vardimid(int ncid, int varid, int[] dimids);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_var_fill(int ncid, int varid, out int no_fill, out object fill_value);


        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_natts(int ncid, out int ngatts);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_unlimdim(int ncid, out int unlimdimid);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_format(int ncid, out int format);

        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_attname(int ncid, int varid, int attnum, StringBuilder name);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_att(int ncid, int varid, string name, out NcType type, out int length);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_att_text(int ncid, int varid, string name, StringBuilder value);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_att_schar(int ncid, int varid, string name, sbyte[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_att_short(int ncid, int varid, string name, short[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_att_int(int ncid, int varid, string name, int[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_att_float(int ncid, int varid, string name, float[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_att_double(int ncid, int varid, string name, double[] data);

        [DllImport("netcdf4.dll")]
        public static extern int nc_put_att_text(int ncid, int varid, string name, int len, string tp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_att_double(int ncid, int varid, string name, NcType type, int len, double[] tp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_att_int(int ncid, int varid, string name, NcType type, int len, int[] tp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_att_short(int ncid, int varid, string name, NcType type, int len, short[] tp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_att_float(int ncid, int varid, string name, NcType type, int len, float[] tp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_att_byte(int ncid, int varid, string name, NcType type, int len, sbyte[] tp);

        [DllImport("netcdf4.dll")]
        public static extern int nc_def_dim(int ncid, string name, int len, out int dimidp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_dim(int ncid, int dimid, StringBuilder name, out int length);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_dimname(int ncid, int dimid, StringBuilder name);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_dimid(int ncid, string name, out int dimid);
        [DllImport("netcdf4.dll")]
        public static extern int nc_inq_dimlen(int ncid, int dimid, out int length);


        [DllImport("netcdf4.dll")]
        public static extern int nc_get_var_text(int ncid, int varid, byte[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_var_schar(int ncid, int varid, sbyte[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_var_short(int ncid, int varid, short[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_var_int(int ncid, int varid, int[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_var_long(int ncid, int varid, long[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_var_float(int ncid, int varid, float[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_var_double(int ncid, int varid, double[] data);

        [DllImport("netcdf4.dll")]
        public static extern int nc_put_vara_double(int ncid, int varid, int[] start, int[] count, double[] dp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_vara_float(int ncid, int varid, int[] start, int[] count, float[] fp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_vara_short(int ncid, int varid, int[] start, int[] count, short[] sp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_vara_int(int ncid, int varid, int[] start, int[] count, int[] ip);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_vara_long(int ncid, int varid, int[] start, int[] count, long[] lp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_vara_ubyte(int ncid, int varid, int[] start, int[] count, byte[] bp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_vara_schar(int ncid, int varid, int[] start, int[] count, sbyte[] cp);
        [DllImport("netcdf4.dll")]
        public static extern int nc_put_vara_string(int ncid, int varid, int[] start, int[] count, string[] sp);


        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_text(int ncid, int varid, int[] start, int[] count, byte[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_schar(int ncid, int varid, int[] start, int[] count, sbyte[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_short(int ncid, int varid, int[] start, int[] count, short[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_ubyte(int ncid, int varid, int[] start, int[] count, byte[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_long(int ncid, int varid, int[] start, int[] count, long[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_int(int ncid, int varid, int[] start, int[] count, int[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_float(int ncid, int varid, int[] start, int[] count, float[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_double(int ncid, int varid, int[] start, int[] count, double[] data);
        [DllImport("netcdf4.dll")]
        public static extern int nc_get_vara_string(int ncid, int varid, int[] start, int[] count, string[] data);
    }


}
