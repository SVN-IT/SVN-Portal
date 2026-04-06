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
    public class mrp_bom_line_newDataPortal
    {
        string connectionString;
        public mrp_bom_line_newDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<mrp_bom_lineUI> GetDataByBomID(int bom_id)
        {
            try
            {
                List<mrp_bom_lineUI> data = new List<mrp_bom_lineUI>();
                string sql = "SELECT * FROM SVN_mrp_bom_line_new WHERE bom_id = @bom_id";
                var param = new { bom_id = bom_id };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var dataUI = connection.Query<mrp_bom_lineUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    data = dataUI.ToList();
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public int Insert(mrp_bom_lineUI model)
        {
            try
            {
                string sql = @"
                    INSERT INTO SVN_mrp_bom_line_new
                    (
                        product_id,
                        product_tmpl_id,
                        company_id,
                        product_uom_id,
                        sequence,
                        bom_id,
                        operation_id,
                        create_uid,
                        write_uid,
                        product_qty,
                        manual_consumption,
                        create_date,
                        write_date,
                        cost_share,
                        standard_qty,
                        loss_rate
                    )
                    VALUES
                    (
                        @product_id,
                        @product_tmpl_id,
                        @company_id,
                        @product_uom_id,
                        @sequence,
                        @bom_id,
                        @operation_id,
                        @create_uid,
                        @write_uid,
                        @product_qty,
                        @manual_consumption,
                        @create_date,
                        @write_date,
                        @cost_share,
                        @standard_qty,
                        @loss_rate
                    );
                    SELECT CAST(SCOPE_IDENTITY() as int);
                    ";

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    return connection.ExecuteScalar<int>(sql, model);
                }
            }
            catch
            {
                return 0;
            }
        }

        public bool Update(mrp_bom_lineUI model)
        {
            try
            {
                string sql = @"
                    UPDATE SVN_mrp_bom_line_new SET
                        product_id = @product_id,
                        product_tmpl_id = @product_tmpl_id,
                        company_id = @company_id,
                        product_uom_id = @product_uom_id,
                        sequence = @sequence,
                        bom_id = @bom_id,
                        operation_id = @operation_id,
                        write_uid = @write_uid,
                        product_qty = @product_qty,
                        manual_consumption = @manual_consumption,
                        write_date = @write_date,
                        cost_share = @cost_share,
                        standard_qty = @standard_qty,
                        loss_rate = @loss_rate
                    WHERE id = @id
                    ";

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    int rows = connection.Execute(sql, model);
                    return rows > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool Delete(int bom_id)
        {
            try
            {
                string sql = "DELETE FROM SVN_mrp_bom_line_new WHERE bom_id = @bom_id";

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    int rows = connection.Execute(sql, new { bom_id });
                    return rows > 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
