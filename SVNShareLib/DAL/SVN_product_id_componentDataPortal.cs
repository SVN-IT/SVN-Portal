using Dapper;
using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DAL
{
    public class SVN_product_id_componentDataPortal
    {
        string connectionString;
        public SVN_product_id_componentDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        //Viết hàm lấy 1 đối tượng theo product_id bằng dapper
        public SVN_product_id_component GetByProductId(string product_id)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var result = connection.QueryFirstOrDefault<DTO.SVN_product_id_component>("SELECT * FROM SVN_product_id_component WHERE product_id = @product_id", new { product_id });
                return result;
            }
        }
    }
}
