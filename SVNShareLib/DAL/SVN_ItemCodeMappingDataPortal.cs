using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace SVNShareLib.DAL
{
    public class SVN_ItemCodeMappingDataPortal
    {
        string connectionString;
        public SVN_ItemCodeMappingDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<SVN_ItemCodeMappingUI> GetDataByCustomerCode(List<string> customer_code)
        {
            // Kiểm tra nếu danh sách trống thì trả về list rỗng để tránh lỗi câu lệnh SQL
            if (customer_code == null || customer_code.Count == 0)
            {
                return null;
            }

            try
            {
                string sql = "SELECT * FROM SVN_ItemCodeMapping WHERE customer_code IN @customer_code";

                // Truyền trực tiếp danh sách vào object parameter của Dapper
                var param = new { customer_code = customer_code };
                int timeOut = 1000;

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var dataUI = connection.Query<SVN_ItemCodeMappingUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return dataUI.ToList();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
