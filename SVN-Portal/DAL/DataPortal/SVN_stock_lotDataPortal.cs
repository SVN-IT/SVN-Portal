using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;
using Dapper;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_stock_lotDataPortal
    {
        string connectionString;
        public SVN_stock_lotDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_Tem_InfoUI>> ReadListByProductID(int product_id, int countRows = 1)
        {
            List<SVN_Tem_InfoUI> dataUI = new List<SVN_Tem_InfoUI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select TOP(#countRows) pt.name AS item_name,stl.name AS lot_code,mpr.product_qty from SVN_stock_lot stl" +
                        " left join SVN_product_product pp on stl.product_id = pp.id" + 
                        " left join SVN_product_template_1 pt on pp.product_tmpl_id = pt.id" +
                        " left join SVN_mrp_production_1 mpr on stl.id = mpr.lot_producing_id" +
                        " where stl.product_id = @product_id and mpr.state = 'done'" +
                        " order by mpr.date_finished desc";
                    sql = sql.Replace("#countRows", countRows.ToString());
                    param = new { product_id = product_id };
                    var data = await conn.QueryAsync<SVN_Tem_InfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_Tem_InfoUI>> ReadListByProductName(string item_name, int countRows = 1)
        {
            List<SVN_Tem_InfoUI> dataUI = new List<SVN_Tem_InfoUI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select TOP(#countRows) pt.name AS item_name,stl.name AS lot_code,mpr.product_qty from SVN_stock_lot stl" +
                        " left join SVN_product_product pp on stl.product_id = pp.id" +
                        " left join SVN_product_template_1 pt on pp.product_tmpl_id = pt.id" +
                        " left join SVN_mrp_production_1 mpr on stl.id = mpr.lot_producing_id" +
                        " where item_name like @item_name and mpr.state = 'done'" +
                        " order by mpr.date_finished desc";
                    sql = sql.Replace("#countRows", countRows.ToString());
                    param = new { item_name = item_name };
                    var data = await conn.QueryAsync<SVN_Tem_InfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
