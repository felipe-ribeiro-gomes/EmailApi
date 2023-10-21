using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;

namespace EmailApi.Extensions
{
	public static class DataTableExtension
	{
        public static IList<dynamic> ToDynamicList(this DataTable dt)
        {
            List<dynamic> data = new List<dynamic>();
            foreach (DataRow row in dt.Rows)
            {
                dynamic item = GetItem(row);
                data.Add(item);
            }
            return data;
        }

        private static dynamic GetItem(DataRow dr)
        {
            dynamic obj = new ExpandoObject();
            foreach (DataColumn column in dr.Table.Columns)
                ((IDictionary<string, object>)obj)[column.ColumnName] = (dr[column.ColumnName] is DBNull ? null : dr[column.ColumnName]);
            return obj;
        }
    }
}