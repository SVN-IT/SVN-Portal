using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;
using Dapper;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_product_productDataPortal
    {
        string connectionString;
        public SVN_product_productDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_product_productUI>> ReadList()
        {
            List<SVN_product_productUI> dataUI = new List<SVN_product_productUI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select pp.id, pp.default_code, pt.name AS product_name from SVN_product_product pp" +
                        " left join SVN_product_template_1 pt on pp.product_tmpl_id = pt.id";
                    param = new object();
                    var data = await conn.QueryAsync<SVN_product_productUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }
    }
}
